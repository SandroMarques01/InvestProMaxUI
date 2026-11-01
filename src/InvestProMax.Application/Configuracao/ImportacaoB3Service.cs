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

    public ImportacaoB3Service(
        IExtratoB3Reader reader,
        IPapelRepository papelRepository,
        ITransacaoRepository transacaoRepository,
        IDividendoRepository dividendoRepository,
        IMovimentoAluguelRepository movimentoAluguelRepository,
        IUsuarioLogado usuarioLogado)
    {
        _reader = reader;
        _papelRepository = papelRepository;
        _transacaoRepository = transacaoRepository;
        _dividendoRepository = dividendoRepository;
        _movimentoAluguelRepository = movimentoAluguelRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<ImportarB3Response> ImportarAsync(Stream planilha, CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;

        // Ordem cronológica (estável): o pareamento saída/devolução de aluguel depende da ordem,
        // e as planilhas da B3 podem vir em ordem decrescente de data.
        var linhas = _reader.Ler(planilha)
            .Select((linha, indice) => (linha, indice))
            .OrderBy(x => x.linha.Data).ThenBy(x => x.indice)
            .Select(x => x.linha)
            .ToList();

        var hashesTransacao = await _transacaoRepository.ListarHashesImportadosAsync(usuarioId, ct);
        var hashesDividendo = await _dividendoRepository.ListarHashesImportadosAsync(usuarioId, ct);
        var hashesAluguel = await _movimentoAluguelRepository.ListarHashesImportadosAsync(usuarioId, ct);

        var papeisPorCodigo = new Dictionary<string, Papel>(StringComparer.OrdinalIgnoreCase);
        var emAluguelPorCodigo = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var novasTransacoes = new List<Transacao>();
        var novosDividendos = new List<Dividendo>();
        var novosMovimentosAluguel = new List<MovimentoAluguel>();
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

            var jaImportada = classificacao.Tipo == TipoRegistroImportado.Transacao
                ? hashesTransacao.Contains(hash) || (ehTransferenciaLiquidacao && hashesAluguel.Contains(hash))
                : hashesDividendo.Contains(hash);
            if (jaImportada)
            {
                duplicadas++;
                continue;
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

        if (novasTransacoes.Count > 0)
        {
            await _transacaoRepository.AdicionarEmLoteAsync(novasTransacoes, ct);
        }
        if (novosDividendos.Count > 0)
        {
            await _dividendoRepository.AdicionarEmLoteAsync(novosDividendos, ct);
        }
        if (novosMovimentosAluguel.Count > 0)
        {
            await _movimentoAluguelRepository.AdicionarEmLoteAsync(novosMovimentosAluguel, ct);
        }

        return new ImportarB3Response(
            papeisCriados, novasTransacoes.Count, novosDividendos.Count, duplicadas, naoReconhecidas,
            novosMovimentosAluguel.Count);
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
