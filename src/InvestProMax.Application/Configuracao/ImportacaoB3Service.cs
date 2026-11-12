using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Papeis;
using InvestProMax.Application.Transacoes;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Configuracao;

public interface IImportacaoB3Service
{
    Task<ImportarB3Response> ImportarAsync(Stream planilha, CancellationToken ct = default);
}

public sealed class ImportacaoB3Service : IImportacaoB3Service
{
    private const string TransferenciaLiquidacao = "Transferência - Liquidação";

    private readonly IExtratoB3Reader _reader;
    private readonly IPapelRepository _papelRepository;
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IDividendoRepository _dividendoRepository;
    private readonly IMovimentoAluguelRepository _movimentoAluguelRepository;
    private readonly IUsuarioLogado _usuarioLogado;
    private readonly IMongoBackupService? _mongoBackupService;

    public ImportacaoB3Service(
        IExtratoB3Reader reader,
        IPapelRepository papelRepository,
        ITransacaoRepository transacaoRepository,
        IDividendoRepository dividendoRepository,
        IMovimentoAluguelRepository movimentoAluguelRepository,
        IUsuarioLogado usuarioLogado,
        IMongoBackupService? mongoBackupService = null)
    {
        _reader = reader;
        _papelRepository = papelRepository;
        _transacaoRepository = transacaoRepository;
        _dividendoRepository = dividendoRepository;
        _movimentoAluguelRepository = movimentoAluguelRepository;
        _usuarioLogado = usuarioLogado;
        _mongoBackupService = mongoBackupService;
    }

    public async Task<ImportarB3Response> ImportarAsync(Stream planilha, CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;

        // Ordem cronológica (estável): o pareamento saída/devolução de aluguel e o cálculo de saldo
        // nos eventos de grupamento/desdobro dependem da ordem, e as planilhas da B3 podem vir em ordem decrescente de data.
        var linhas = _reader.Ler(planilha)
            .Select((linha, indice) => (linha, indice))
            .OrderBy(x => x.linha.Data)
            .ThenBy(x => ClassificadorMovimentacaoB3.Classificar(x.linha.Movimentacao).Tipo == TipoRegistroImportado.EventoPapel ? 1 : 0)
            .ThenBy(x => x.linha.Movimentacao.Trim() == TransferenciaLiquidacao
                         && ClassificadorMovimentacaoB3.DirecaoPorEntradaSaida(x.linha.EntradaSaida) != TipoTransacao.Compra
                         && x.linha.PrecoUnitario is null ? 0 : 1)
            .ThenBy(x => x.indice)
            .Select(x => x.linha)
            .ToList();

        var hashesTransacao = await _transacaoRepository.ListarHashesImportadosAsync(usuarioId, ct);
        var hashesDividendo = await _dividendoRepository.ListarHashesImportadosAsync(usuarioId, ct);
        var hashesAluguel = await _movimentoAluguelRepository.ListarHashesImportadosAsync(usuarioId, ct);

        var papeisPorCodigo = new Dictionary<string, Papel>(StringComparer.OrdinalIgnoreCase);
        var emAluguelPorCodigo = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var papeisComTransacoes = new HashSet<Guid>();
        var novasTransacoes = new List<Transacao>();
        var novosDividendos = new List<Dividendo>();
        var novosMovimentosAluguel = new List<MovimentoAluguel>();
        var totalTransacoesImportadas = 0;
        var totalDividendosImportados = 0;
        var totalAlugueisImportados = 0;
        var papeisCriados = 0;
        var duplicadas = 0;
        var naoReconhecidas = 0;

        foreach (var linha in linhas)
        {
            var classificacao = ClassificadorMovimentacaoB3.Classificar(linha.Movimentacao);
            if (classificacao.Tipo == TipoRegistroImportado.Ignorar
                || (linha.Movimentacao.Trim() == "Empréstimo" && !ClassificadorMovimentacaoB3.EhRemuneracaoDeAluguel(linha)))
            {
                naoReconhecidas++;
                continue;
            }

            var hash = HashDeLinhaImportada.CalcularParaLinhaB3(linha);
            var ehTransferenciaLiquidacao = classificacao.Tipo == TipoRegistroImportado.Transacao
                && linha.Movimentacao.Trim() == TransferenciaLiquidacao;

            if (classificacao.Tipo != TipoRegistroImportado.EventoPapel)
            {
                var jaImportada = classificacao.Tipo == TipoRegistroImportado.Transacao
                    ? hashesTransacao.Contains(hash) || (ehTransferenciaLiquidacao && hashesAluguel.Contains(hash))
                    : hashesDividendo.Contains(hash);
                if (jaImportada)
                {
                    duplicadas++;
                    continue;
                }
            }

            var (codigo, nome) = InferenciaPapel.SepararProduto(linha.Produto);
            if (!papeisPorCodigo.TryGetValue(codigo, out var papel))
            {
                papel = await _papelRepository.ObterPorCodigoAsync(usuarioId, codigo, ct);
                if (papel is null)
                {
                    papel = new Papel
                    {
                        UsuarioId = usuarioId,
                        Codigo = codigo,
                        Nome = nome,
                        TipoPapel = InferenciaPapel.InferirTipo(codigo),
                        Ativo = true
                    };
                    await _papelRepository.AdicionarAsync(papel, ct);
                    papeisCriados++;
                }
                papeisPorCodigo[codigo] = papel;
                emAluguelPorCodigo[codigo] = CalculadoraAluguel.QuantidadeEmAluguel(papel.MovimentosAluguel);
            }

            if (classificacao.Tipo == TipoRegistroImportado.EventoPapel)
            {
                await PersistirLotesPendentesAsync();

                papel = await _papelRepository.ObterPorIdAsync(papel.Id, usuarioId, ct) ?? papel;
                papeisPorCodigo[codigo] = papel;
                papeisComTransacoes.Add(papel.Id);

                var tipoEvento = classificacao.TipoEventoPapel!.Value;
                var tipoAcao = tipoEvento == TipoEventoPapel.Desdobramento ? "Desdobramento" : "Grupamento";
                var marcador = $"[{tipoAcao} {linha.Data:dd/MM/yyyy}]";

                var transacoesDoPapel = await _transacaoRepository.ListarPorPapelAsync(papel.Id, ct);
                if (transacoesDoPapel.Count == 0 && papel.Transacoes.Count > 0)
                {
                    transacoesDoPapel = papel.Transacoes.ToList();
                }

                var transacoesArtificiaisEvento = transacoesDoPapel
                    .Where(t => t.Data == linha.Data && PapelService.EhTransacaoArtificialDeEventoB3(t.Descricao, tipoEvento))
                    .ToList();

                var jaAplicado = transacoesDoPapel.Any(t =>
                    t.Data < linha.Data
                    && t.Descricao is not null
                    && t.Descricao.Contains(marcador, StringComparison.OrdinalIgnoreCase));

                if (jaAplicado)
                {
                    if (transacoesArtificiaisEvento.Count > 0)
                    {
                        await _transacaoRepository.RemoverEmLoteAsync(transacoesArtificiaisEvento, ct);
                        foreach (var artificial in transacoesArtificiaisEvento)
                        {
                            papel.Transacoes.Remove(artificial);
                        }
                    }

                    duplicadas++;
                    continue;
                }

                var transacoesAnteriores = transacoesDoPapel
                    .Where(t => t.Data < linha.Data && !PapelService.EhTransacaoArtificialDeEventoB3(t.Descricao, tipoEvento))
                    .ToList();

                var saldoAnterior = transacoesAnteriores.Sum(t =>
                    t.TipoTransacao == TipoTransacao.Compra ? t.Quantidade : -t.Quantidade);

                if (saldoAnterior > 0 && linha.Quantidade > 0)
                {
                    decimal multiplicadorQuantidade;
                    decimal fator;
                    if (tipoEvento == TipoEventoPapel.Desdobramento)
                    {
                        var novaQuantidadeTotal = saldoAnterior + linha.Quantidade;
                        multiplicadorQuantidade = novaQuantidadeTotal / saldoAnterior;
                        fator = multiplicadorQuantidade;
                    }
                    else
                    {
                        multiplicadorQuantidade = linha.Quantidade / saldoAnterior;
                        fator = saldoAnterior / linha.Quantidade;
                    }

                    var dividendosDoPapel = await _dividendoRepository.ListarPorPapelAsync(papel.Id, ct);
                    if (dividendosDoPapel.Count == 0 && papel.Dividendos.Count > 0)
                    {
                        dividendosDoPapel = papel.Dividendos.ToList();
                    }

                    var dividendosParaAjustar = dividendosDoPapel
                        .Where(d => d.Data < linha.Data && d.Descricao?.Contains(marcador, StringComparison.OrdinalIgnoreCase) != true)
                        .ToList();

                    var backupTransacoes = new List<BackupTransacaoEventoItem>(transacoesAnteriores.Count);
                    var backupDividendos = new List<BackupDividendoEventoItem>(dividendosParaAjustar.Count);

                    foreach (var t in transacoesAnteriores)
                    {
                        var qtdAnterior = t.Quantidade;
                        var valorAnterior = t.ValorUnitario;
                        var qtdAtual = qtdAnterior * multiplicadorQuantidade;
                        var valorAtual = valorAnterior / multiplicadorQuantidade;

                        backupTransacoes.Add(new BackupTransacaoEventoItem(
                            TransacaoId: t.Id,
                            PapelAnteriorId: papel.Id,
                            PapelAtualId: papel.Id,
                            CodigoAnterior: papel.Codigo,
                            CodigoAtual: papel.Codigo,
                            DataAcao: linha.Data,
                            TipoAcao: tipoAcao,
                            FatorMultiplicador: fator,
                            DataTransacao: t.Data,
                            TipoTransacao: t.TipoTransacao.ToString(),
                            QuantidadeAnterior: qtdAnterior,
                            QuantidadeAtual: qtdAtual,
                            ValorAnterior: valorAnterior,
                            ValorAtual: valorAtual,
                            Descricao: t.Descricao));

                        t.Quantidade = qtdAtual;
                        t.ValorUnitario = valorAtual;
                        t.Historico = true;
                        t.Descricao = string.IsNullOrWhiteSpace(t.Descricao) ? marcador : $"{t.Descricao} {marcador}";
                    }

                    foreach (var d in dividendosParaAjustar)
                    {
                        var qtdAnterior = d.Quantidade;
                        var qtdAtual = qtdAnterior * multiplicadorQuantidade;

                        backupDividendos.Add(new BackupDividendoEventoItem(
                            DividendoId: d.Id,
                            PapelAnteriorId: papel.Id,
                            PapelAtualId: papel.Id,
                            CodigoAnterior: papel.Codigo,
                            CodigoAtual: papel.Codigo,
                            DataAcao: linha.Data,
                            TipoAcao: tipoAcao,
                            FatorMultiplicador: fator,
                            DataDividendo: d.Data,
                            TipoDividendo: d.TipoDividendo.ToString(),
                            QuantidadeAnterior: qtdAnterior,
                            QuantidadeAtual: qtdAtual,
                            ValorAnterior: d.ValorRecebido,
                            ValorAtual: d.ValorRecebido,
                            Descricao: d.Descricao));

                        d.Quantidade = qtdAtual;
                        d.Historico = true;
                        d.Descricao = string.IsNullOrWhiteSpace(d.Descricao) ? marcador : $"{d.Descricao} {marcador}";
                    }

                    if (_mongoBackupService is not null)
                    {
                        await _mongoBackupService.RegistrarBackupEventoPapelAsync(backupTransacoes, backupDividendos, ct);
                    }

                    if (transacoesAnteriores.Count > 0)
                    {
                        await _transacaoRepository.AtualizarEmLoteAsync(transacoesAnteriores, ct);
                    }

                    if (dividendosParaAjustar.Count > 0)
                    {
                        await _dividendoRepository.AtualizarEmLoteAsync(dividendosParaAjustar, ct);
                    }
                }

                if (transacoesArtificiaisEvento.Count > 0)
                {
                    await _transacaoRepository.RemoverEmLoteAsync(transacoesArtificiaisEvento, ct);
                    foreach (var artificial in transacoesArtificiaisEvento)
                    {
                        papel.Transacoes.Remove(artificial);
                    }
                }

                continue;
            }

            if (classificacao.Tipo == TipoRegistroImportado.Transacao)
            {
                var quantidade = linha.Quantidade;

                if (ehTransferenciaLiquidacao)
                {
                    var emAluguel = emAluguelPorCodigo[codigo];
                    var entrada = ClassificadorMovimentacaoB3.DirecaoPorEntradaSaida(linha.EntradaSaida) == TipoTransacao.Compra;

                    // Saída para aluguel: débito sem preço. O papel continua na carteira — sem Transacao.
                    if (!entrada && linha.PrecoUnitario is null)
                    {
                        novosMovimentosAluguel.Add(NovoMovimento(papel, TipoMovimentoAluguel.Saida, quantidade, linha, hash));
                        hashesAluguel.Add(hash);
                        emAluguelPorCodigo[codigo] = emAluguel + quantidade;
                        continue;
                    }

                    // Devolução: crédito com preço enquanto há quantidade emprestada do mesmo papel.
                    // O que exceder o emprestado é compra de verdade.
                    if (entrada && linha.PrecoUnitario is not null && emAluguel > 0)
                    {
                        var devolvida = Math.Min(quantidade, emAluguel);
                        novosMovimentosAluguel.Add(NovoMovimento(papel, TipoMovimentoAluguel.Devolucao, devolvida, linha, hash));
                        hashesAluguel.Add(hash);
                        emAluguelPorCodigo[codigo] = emAluguel - devolvida;
                        quantidade -= devolvida;

                        if (quantidade <= 0)
                        {
                            continue;
                        }
                    }
                }

                var tipoTransacao = classificacao.TipoTransacao
                    ?? ClassificadorMovimentacaoB3.DirecaoPorEntradaSaida(linha.EntradaSaida);
                var valorUnitario = classificacao.ValorUnitarioZerado
                    ? 0
                    : linha.PrecoUnitario ?? (linha.Quantidade == 0 ? 0 : linha.ValorOperacao / linha.Quantidade);

                hashesTransacao.Add(hash);
                papeisComTransacoes.Add(papel.Id);
                novasTransacoes.Add(new Transacao
                {
                    PapelId = papel.Id,
                    TipoTransacao = tipoTransacao,
                    Quantidade = quantidade,
                    ValorUnitario = valorUnitario,
                    Data = linha.Data,
                    Descricao = $"Importado da B3: {linha.Movimentacao}",
                    OrigemImportacaoHash = hash
                });
            }
            else
            {
                hashesDividendo.Add(hash);
                novosDividendos.Add(new Dividendo
                {
                    PapelId = papel.Id,
                    TipoDividendo = classificacao.TipoDividendo!.Value,
                    ValorRecebido = linha.ValorOperacao,
                    Quantidade = linha.Quantidade,
                    Data = linha.Data,
                    Descricao = $"Importado da B3: {linha.Movimentacao}",
                    OrigemImportacaoHash = hash
                });
            }
        }

        await PersistirLotesPendentesAsync();

        foreach (var papelId in papeisComTransacoes)
        {
            var papelAtualizado = await _papelRepository.ObterPorIdAsync(papelId, usuarioId, ct);
            if (papelAtualizado is null)
            {
                continue;
            }

            var saldo = papelAtualizado.Transacoes.Sum(t =>
                t.TipoTransacao == TipoTransacao.Compra ? t.Quantidade : -t.Quantidade);
            var deveEstarAtivo = saldo > 0;
            if (papelAtualizado.Ativo != deveEstarAtivo)
            {
                papelAtualizado.Ativo = deveEstarAtivo;
                await _papelRepository.AtualizarAsync(papelAtualizado, ct);
            }
        }

        return new ImportarB3Response(
            papeisCriados, totalTransacoesImportadas, totalDividendosImportados, duplicadas, naoReconhecidas,
            totalAlugueisImportados);

        async Task PersistirLotesPendentesAsync()
        {
            if (novasTransacoes.Count > 0)
            {
                await _transacaoRepository.AdicionarEmLoteAsync(novasTransacoes, ct);
                totalTransacoesImportadas += novasTransacoes.Count;
                novasTransacoes.Clear();
            }
            if (novosDividendos.Count > 0)
            {
                await _dividendoRepository.AdicionarEmLoteAsync(novosDividendos, ct);
                totalDividendosImportados += novosDividendos.Count;
                novosDividendos.Clear();
            }
            if (novosMovimentosAluguel.Count > 0)
            {
                await _movimentoAluguelRepository.AdicionarEmLoteAsync(novosMovimentosAluguel, ct);
                totalAlugueisImportados += novosMovimentosAluguel.Count;
                novosMovimentosAluguel.Clear();
            }
        }
    }

    private static MovimentoAluguel NovoMovimento(
        Papel papel, TipoMovimentoAluguel tipo, decimal quantidade, LinhaExtratoB3 linha, string hash) => new()
    {
        PapelId = papel.Id,
        Tipo = tipo,
        Quantidade = quantidade,
        Data = linha.Data,
        Descricao = $"Importado da B3: {linha.Movimentacao}",
        OrigemImportacaoHash = hash
    };
}
