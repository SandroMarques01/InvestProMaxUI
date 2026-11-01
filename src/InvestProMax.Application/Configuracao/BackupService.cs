using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Papeis;
using InvestProMax.Application.Segmentos;
using InvestProMax.Application.Transacoes;
using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Configuracao;

public interface IBackupService
{
    Task<byte[]> ExportarAsync(CancellationToken ct = default);
    Task<RestaurarBackupResponse> RestaurarAsync(Stream planilha, CancellationToken ct = default);
}

/// <summary>
/// Regra 9. ASSUNÇÃO SINALIZADA: "restauração completa" foi implementada como upsert (por Id) —
/// registros do backup que já existem são atualizados, os que não existem são criados — em vez
/// de apagar tudo antes de importar. Uma restauração destrutiva é mais arriscada por padrão;
/// quem realmente quiser substituir tudo pode chamar DELETE /configuracao/zerar-base antes.
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly IBackupExcelWriter _writer;
    private readonly IBackupExcelReader _reader;
    private readonly IPapelRepository _papelRepository;
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IDividendoRepository _dividendoRepository;
    private readonly IMovimentoAluguelRepository _movimentoAluguelRepository;
    private readonly ISegmentoRepository _segmentoRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public BackupService(
        IBackupExcelWriter writer,
        IBackupExcelReader reader,
        IPapelRepository papelRepository,
        ITransacaoRepository transacaoRepository,
        IDividendoRepository dividendoRepository,
        IMovimentoAluguelRepository movimentoAluguelRepository,
        ISegmentoRepository segmentoRepository,
        IUsuarioLogado usuarioLogado)
    {
        _writer = writer;
        _reader = reader;
        _papelRepository = papelRepository;
        _transacaoRepository = transacaoRepository;
        _dividendoRepository = dividendoRepository;
        _movimentoAluguelRepository = movimentoAluguelRepository;
        _segmentoRepository = segmentoRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<byte[]> ExportarAsync(CancellationToken ct = default)
    {
        var papeis = await _papelRepository.ListarTodosDoUsuarioAsync(_usuarioLogado.UsuarioId, ct);

        var linhasPapel = papeis.Select(p => new LinhaBackupPapel(
            p.Id, p.Codigo, p.Nome, p.TipoPapel, p.CotacaoAtual, p.Descricao, p.Ativo,
            p.PapelSegmentos.Where(ps => ps.Segmento is not null).Select(ps => ps.Segmento!.Nome).ToList())).ToList();

        var linhasTransacao = papeis.SelectMany(p => p.Transacoes.Select(t => new LinhaBackupTransacao(
            t.Id, p.Codigo, t.TipoTransacao, t.Quantidade, t.ValorUnitario, t.Data, t.Descricao, t.Historico))).ToList();

        var linhasDividendo = papeis.SelectMany(p => p.Dividendos.Select(d => new LinhaBackupDividendo(
            d.Id, p.Codigo, d.TipoDividendo, d.ValorRecebido, d.Quantidade, d.Data, d.Descricao, d.Historico))).ToList();

        var linhasAluguel = papeis.SelectMany(p => p.MovimentosAluguel.Select(m => new LinhaBackupAluguel(
            m.Id, p.Codigo, m.Tipo, m.Quantidade, m.Data, m.Descricao))).ToList();

        return _writer.Gerar(new BackupDados(linhasPapel, linhasTransacao, linhasDividendo, linhasAluguel));
    }

    public async Task<RestaurarBackupResponse> RestaurarAsync(Stream planilha, CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;
        var dados = _reader.Ler(planilha);

        var papeisCriados = 0;
        var papeisAtualizados = 0;
        var papelIdPorCodigo = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var linha in dados.Papeis)
        {
            var papel = await _papelRepository.ObterPorIdAsync(linha.Id, usuarioId, ct);
            if (papel is null)
            {
                papel = new Papel { Id = linha.Id, UsuarioId = usuarioId, Codigo = linha.Codigo };
                papel.Nome = linha.Nome;
                papel.TipoPapel = linha.TipoPapel;
                papel.CotacaoAtual = linha.CotacaoAtual;
                papel.Descricao = linha.Descricao;
                papel.Ativo = linha.Ativo;
                await AplicarSegmentosAsync(papel, linha.SegmentoNomes, ct);
                await _papelRepository.AdicionarAsync(papel, ct);
                papeisCriados++;
            }
            else
            {
                papel.Nome = linha.Nome;
                papel.TipoPapel = linha.TipoPapel;
                papel.CotacaoAtual = linha.CotacaoAtual;
                papel.Descricao = linha.Descricao;
                papel.Ativo = linha.Ativo;
                await AplicarSegmentosAsync(papel, linha.SegmentoNomes, ct);
                await _papelRepository.AtualizarAsync(papel, ct);
                papeisAtualizados++;
            }

            papelIdPorCodigo[linha.Codigo] = papel.Id;
        }

        var (transacoesCriadas, transacoesAtualizadas) = await RestaurarTransacoesAsync(dados.Transacoes, papelIdPorCodigo, usuarioId, ct);
        var (dividendosCriados, dividendosAtualizados) = await RestaurarDividendosAsync(dados.Dividendos, papelIdPorCodigo, usuarioId, ct);

        var (alugueisCriados, alugueisAtualizados) = await RestaurarAlugueisAsync(dados.Alugueis ?? [], papelIdPorCodigo, usuarioId, ct);

        return new RestaurarBackupResponse(
            papeisCriados, papeisAtualizados, transacoesCriadas, transacoesAtualizadas, dividendosCriados, dividendosAtualizados,
            alugueisCriados, alugueisAtualizados);
    }

    private async Task<(int Criadas, int Atualizadas)> RestaurarTransacoesAsync(
        IReadOnlyList<LinhaBackupTransacao> linhas, Dictionary<string, Guid> papelIdPorCodigo, Guid usuarioId, CancellationToken ct)
    {
        var criadas = 0;
        var atualizadas = 0;

        foreach (var linha in linhas)
        {
            var papelId = ObterPapelIdOuFalhar(papelIdPorCodigo, linha.PapelCodigo);
            var transacao = await _transacaoRepository.ObterPorIdAsync(linha.Id, usuarioId, ct);

            if (transacao is null)
            {
                await _transacaoRepository.AdicionarAsync(new Transacao
                {
                    Id = linha.Id, PapelId = papelId, TipoTransacao = linha.TipoTransacao, Quantidade = linha.Quantidade,
                    ValorUnitario = linha.ValorUnitario, Data = linha.Data, Descricao = linha.Descricao, Historico = linha.Historico
                }, ct);
                criadas++;
            }
            else
            {
                transacao.PapelId = papelId;
                transacao.TipoTransacao = linha.TipoTransacao;
                transacao.Quantidade = linha.Quantidade;
                transacao.ValorUnitario = linha.ValorUnitario;
                transacao.Data = linha.Data;
                transacao.Descricao = linha.Descricao;
                transacao.Historico = linha.Historico;
                await _transacaoRepository.AtualizarAsync(transacao, ct);
                atualizadas++;
            }
        }

        return (criadas, atualizadas);
    }

    private async Task<(int Criados, int Atualizados)> RestaurarAlugueisAsync(
        IReadOnlyList<LinhaBackupAluguel> linhas, Dictionary<string, Guid> papelIdPorCodigo, Guid usuarioId, CancellationToken ct)
    {
        var novos = new List<MovimentoAluguel>();
        var existentes = new List<MovimentoAluguel>();

        foreach (var linha in linhas)
        {
            var papelId = ObterPapelIdOuFalhar(papelIdPorCodigo, linha.PapelCodigo);
            var movimento = await _movimentoAluguelRepository.ObterPorIdAsync(linha.Id, usuarioId, ct);

            if (movimento is null)
            {
                novos.Add(new MovimentoAluguel
                {
                    Id = linha.Id, PapelId = papelId, Tipo = linha.Tipo, Quantidade = linha.Quantidade,
                    Data = linha.Data, Descricao = linha.Descricao
                });
            }
            else
            {
                movimento.PapelId = papelId;
                movimento.Tipo = linha.Tipo;
                movimento.Quantidade = linha.Quantidade;
                movimento.Data = linha.Data;
                movimento.Descricao = linha.Descricao;
                existentes.Add(movimento);
            }
        }

        if (novos.Count > 0)
        {
            await _movimentoAluguelRepository.AdicionarEmLoteAsync(novos, ct);
        }
        if (existentes.Count > 0)
        {
            await _movimentoAluguelRepository.AtualizarEmLoteAsync(existentes, ct);
        }

        return (novos.Count, existentes.Count);
    }

    private async Task<(int Criados, int Atualizados)> RestaurarDividendosAsync(
        IReadOnlyList<LinhaBackupDividendo> linhas, Dictionary<string, Guid> papelIdPorCodigo, Guid usuarioId, CancellationToken ct)
    {
        var criados = 0;
        var atualizados = 0;

        foreach (var linha in linhas)
        {
            var papelId = ObterPapelIdOuFalhar(papelIdPorCodigo, linha.PapelCodigo);
            var dividendo = await _dividendoRepository.ObterPorIdAsync(linha.Id, usuarioId, ct);

            if (dividendo is null)
            {
                await _dividendoRepository.AdicionarAsync(new Dividendo
                {
                    Id = linha.Id, PapelId = papelId, TipoDividendo = linha.TipoDividendo, ValorRecebido = linha.ValorRecebido,
                    Quantidade = linha.Quantidade, Data = linha.Data, Descricao = linha.Descricao, Historico = linha.Historico
                }, ct);
                criados++;
            }
            else
            {
                dividendo.PapelId = papelId;
                dividendo.TipoDividendo = linha.TipoDividendo;
                dividendo.ValorRecebido = linha.ValorRecebido;
                dividendo.Quantidade = linha.Quantidade;
                dividendo.Data = linha.Data;
                dividendo.Descricao = linha.Descricao;
                dividendo.Historico = linha.Historico;
                await _dividendoRepository.AtualizarAsync(dividendo, ct);
                atualizados++;
            }
        }

        return (criados, atualizados);
    }

    private static Guid ObterPapelIdOuFalhar(Dictionary<string, Guid> papelIdPorCodigo, string papelCodigo) =>
        papelIdPorCodigo.TryGetValue(papelCodigo, out var id)
            ? id
            : throw new RegraDeNegocioException(
                $"A planilha de backup referencia o papel '{papelCodigo}', que não está na aba Papel do mesmo arquivo.");

    private async Task AplicarSegmentosAsync(Papel papel, IReadOnlyList<string> segmentoNomes, CancellationToken ct)
    {
        papel.PapelSegmentos.Clear();
        foreach (var nome in segmentoNomes)
        {
            var segmento = await _segmentoRepository.ObterPorNomeAsync(nome, ct);
            if (segmento is null)
            {
                segmento = new Segmento { Nome = nome, Ativo = true };
                await _segmentoRepository.AdicionarAsync(segmento, ct);
            }
            papel.PapelSegmentos.Add(new PapelSegmento { PapelId = papel.Id, SegmentoId = segmento.Id });
        }
    }
}
