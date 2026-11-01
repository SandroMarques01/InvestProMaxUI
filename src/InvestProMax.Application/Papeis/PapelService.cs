using InvestProMax.Application.Common;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Dividendos;
using InvestProMax.Application.Segmentos;
using InvestProMax.Application.Transacoes;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Papeis;

public interface IPapelService
{
    Task<PaginaResultado<PapelResponse>> ListarAsync(PapelFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default);
    Task<PapelResponse> ObterAsync(Guid id, CancellationToken ct = default);
    Task<PapelResponse> CriarAsync(CriarPapelRequest request, CancellationToken ct = default);
    Task<PapelResponse> AtualizarAsync(Guid id, AtualizarPapelRequest request, CancellationToken ct = default);
    Task RemoverAsync(Guid id, CancellationToken ct = default);
    Task<PapelResponse> TrocarAsync(Guid origemId, TrocarPapelRequest request, CancellationToken ct = default);
    Task<PapelResponse> DesdobrarAsync(Guid id, DesdobramentoRequest request, CancellationToken ct = default);
    Task<PapeisGeralResponse> ObterPapeisGeralAsync(TipoPapel? tipoPapel = null, bool? somenteAtivos = true, DateOnly? dataFinal = null, CancellationToken ct = default);
    Task<PapelDetalheResponse> ObterDetalheAsync(Guid id, CancellationToken ct = default);
}

public sealed class PapelService : IPapelService
{
    private readonly IPapelRepository _papelRepository;
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IMovimentoAluguelRepository _movimentoAluguelRepository;
    private readonly IDividendoRepository _dividendoRepository;
    private readonly ISegmentoRepository _segmentoRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public PapelService(
        IPapelRepository papelRepository,
        ITransacaoRepository transacaoRepository,
        IMovimentoAluguelRepository movimentoAluguelRepository,
        IDividendoRepository dividendoRepository,
        ISegmentoRepository segmentoRepository,
        IUsuarioLogado usuarioLogado)
    {
        _papelRepository = papelRepository;
        _transacaoRepository = transacaoRepository;
        _movimentoAluguelRepository = movimentoAluguelRepository;
        _dividendoRepository = dividendoRepository;
        _segmentoRepository = segmentoRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<PaginaResultado<PapelResponse>> ListarAsync(
        PapelFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default)
    {
        var (itens, total) = await _papelRepository.ListarAsync(
            _usuarioLogado.UsuarioId, filtro, paginacao.Skip, paginacao.TamanhoEfetivo, ct);

        return new PaginaResultado<PapelResponse>(
            itens.Select(ParaResponse).ToList(), total, paginacao.PaginaEfetiva, paginacao.TamanhoEfetivo);
    }

    public async Task<PapelResponse> ObterAsync(Guid id, CancellationToken ct = default) =>
        ParaResponse(await ObterOuFalharAsync(id, ct));

    public async Task<PapelResponse> CriarAsync(CriarPapelRequest request, CancellationToken ct = default)
    {
        if (await _papelRepository.ExisteCodigoAsync(_usuarioLogado.UsuarioId, request.Codigo, ct: ct))
        {
            throw new RegraDeNegocioException($"Já existe um papel com o código '{request.Codigo}'.");
        }

        await ValidarSegmentosAsync(request.SegmentoIds, ct);

        var papel = new Papel
        {
            UsuarioId = _usuarioLogado.UsuarioId,
            Codigo = request.Codigo,
            Nome = request.Nome,
            TipoPapel = request.TipoPapel,
            CotacaoAtual = request.CotacaoAtual,
            Descricao = request.Descricao,
            Ativo = true
        };
        AplicarSegmentos(papel, request.SegmentoIds);

        await _papelRepository.AdicionarAsync(papel, ct);
        return ParaResponse(papel);
    }

    public async Task<PapelResponse> AtualizarAsync(Guid id, AtualizarPapelRequest request, CancellationToken ct = default)
    {
        var papel = await ObterOuFalharAsync(id, ct);
        await ValidarSegmentosAsync(request.SegmentoIds, ct);

        papel.Nome = request.Nome;
        papel.TipoPapel = request.TipoPapel;
        papel.CotacaoAtual = request.CotacaoAtual;
        papel.Descricao = request.Descricao;
        AplicarSegmentos(papel, request.SegmentoIds);

        await _papelRepository.AtualizarAsync(papel, ct);
        return ParaResponse(papel);
    }

    public async Task RemoverAsync(Guid id, CancellationToken ct = default)
    {
        var papel = await ObterOuFalharAsync(id, ct);

        if (papel.Transacoes.Count > 0 || papel.Dividendos.Count > 0 || papel.MovimentosAluguel.Count > 0)
        {
            throw new RegraDeNegocioException(
                "Este papel tem transações ou dividendos lançados e não pode ser removido. " +
                "Use a troca de papel se o objetivo é substituí-lo por outro código.");
        }

        await _papelRepository.RemoverAsync(papel, ct);
    }

    /// <summary>
    /// Regra 3: realoca todas as Transacao e Dividendo do papel de origem para o destino, e
    /// desativa a origem. Usado quando um ticker muda de código na B3.
    /// </summary>
    public async Task<PapelResponse> TrocarAsync(Guid origemId, TrocarPapelRequest request, CancellationToken ct = default)
    {
        if (origemId == request.PapelDestinoId)
        {
            throw new RegraDeNegocioException("O papel de destino não pode ser o mesmo que o de origem.");
        }

        var origem = await ObterOuFalharAsync(origemId, ct);
        var destino = await ObterOuFalharAsync(request.PapelDestinoId, ct);

        var transacoesOrigem = await _transacaoRepository.ListarPorPapelAsync(origem.Id, ct);
        foreach (var transacao in transacoesOrigem)
        {
            transacao.PapelId = destino.Id;
        }
        await _transacaoRepository.AtualizarEmLoteAsync(transacoesOrigem, ct);

        var dividendosOrigem = await _dividendoRepository.ListarPorPapelAsync(origem.Id, ct);
        foreach (var dividendo in dividendosOrigem)
        {
            dividendo.PapelId = destino.Id;
        }
        await _dividendoRepository.AtualizarEmLoteAsync(dividendosOrigem, ct);

        var movimentosOrigem = await _movimentoAluguelRepository.ListarPorPapelAsync(origem.Id, ct);
        foreach (var movimento in movimentosOrigem)
        {
            movimento.PapelId = destino.Id;
        }
        await _movimentoAluguelRepository.AtualizarEmLoteAsync(movimentosOrigem, ct);

        origem.Ativo = false;
        await _papelRepository.AtualizarAsync(origem, ct);

        // Reflete a posição migrada — regra 1/2 recalculadas com o histórico já realocado.
        var transacoesDestino = await _transacaoRepository.ListarPorPapelAsync(destino.Id, ct);
        destino.Ativo = CalculadoraPosicao.Calcular(transacoesDestino).QuantidadeAtual > 0;
        await _papelRepository.AtualizarAsync(destino, ct);

        return ParaResponse(await ObterOuFalharAsync(destino.Id, ct));
    }

    /// <summary>
    /// Regra 4: recalcula quantidade/valor unitário das Transacao e Dividendo anteriores à
    /// data do evento, aplicando o fator, e marca os registros ajustados com Historico = true.
    /// ASSUNÇÃO SINALIZADA (ver DesdobramentoRequest): os registros existentes são ajustados
    /// em memória (não duplicados) — a especificação fala em "recalcula" os registros
    /// anteriores, não em criar novos.
    /// </summary>
    public async Task<PapelResponse> DesdobrarAsync(Guid id, DesdobramentoRequest request, CancellationToken ct = default)
    {
        var papel = await ObterOuFalharAsync(id, ct);

        var multiplicadorQuantidade = request.Tipo == TipoEventoPapel.Desdobramento ? request.Fator : 1 / request.Fator;
        var marcador = $"[{(request.Tipo == TipoEventoPapel.Desdobramento ? "Desdobramento" : "Grupamento")} {request.Data:dd/MM/yyyy}]";

        var todasTransacoes = (await _transacaoRepository.ListarPorPapelAsync(papel.Id, ct))
            .Where(t => t.Data < request.Data).ToList();
        var todosDividendos = (await _dividendoRepository.ListarPorPapelAsync(papel.Id, ct))
            .Where(d => d.Data < request.Data).ToList();

        // Idempotência: lançamentos já ajustados por este evento carregam o marcador e são pulados.
        var transacoes = todasTransacoes.Where(t => t.Descricao?.Contains(marcador) != true).ToList();
        var dividendos = todosDividendos.Where(d => d.Descricao?.Contains(marcador) != true).ToList();
        if (transacoes.Count == 0 && dividendos.Count == 0 && (todasTransacoes.Count + todosDividendos.Count) > 0)
        {
            throw new RegraDeNegocioException($"Este evento já foi aplicado ao papel {papel.Codigo} {marcador}.");
        }

        foreach (var transacao in transacoes)
        {
            transacao.Quantidade *= multiplicadorQuantidade;
            transacao.ValorUnitario /= multiplicadorQuantidade;
            transacao.Historico = true;
            transacao.Descricao = string.IsNullOrWhiteSpace(transacao.Descricao) ? marcador : $"{transacao.Descricao} {marcador}";
        }
        await _transacaoRepository.AtualizarEmLoteAsync(transacoes, ct);

        // Dividendo: só a Quantidade de cotas/ações que gerou o provento muda — o valor em
        // dinheiro efetivamente recebido (ValorRecebido) é fato histórico e não é recalculado.
        foreach (var dividendo in dividendos)
        {
            dividendo.Quantidade *= multiplicadorQuantidade;
            dividendo.Historico = true;
            dividendo.Descricao = string.IsNullOrWhiteSpace(dividendo.Descricao) ? marcador : $"{dividendo.Descricao} {marcador}";
        }
        await _dividendoRepository.AtualizarEmLoteAsync(dividendos, ct);

        return ParaResponse(await ObterOuFalharAsync(papel.Id, ct));
    }

    private async Task ValidarSegmentosAsync(IReadOnlyList<Guid>? segmentoIds, CancellationToken ct)
    {
        if (segmentoIds is { Count: > 0 } && !await _segmentoRepository.ExistemTodosAsync(segmentoIds, ct))
        {
            throw new RegraDeNegocioException("Um ou mais segmentos informados não existem.");
        }
    }

    private static void AplicarSegmentos(Papel papel, IReadOnlyList<Guid>? segmentoIds)
    {
        papel.PapelSegmentos.Clear();
        foreach (var segmentoId in segmentoIds ?? [])
        {
            papel.PapelSegmentos.Add(new PapelSegmento { PapelId = papel.Id, SegmentoId = segmentoId });
        }
    }

    private async Task<Papel> ObterOuFalharAsync(Guid id, CancellationToken ct) =>
        await _papelRepository.ObterPorIdAsync(id, _usuarioLogado.UsuarioId, ct)
            ?? throw new NaoEncontradoException("Papel não encontrado.");

    private static PapelResponse ParaResponse(Papel papel)
    {
        var posicao = CalculadoraPosicao.Calcular(papel.Transacoes);

        return new PapelResponse(
            papel.Id, papel.Codigo, papel.Nome, papel.TipoPapel, papel.CotacaoAtual, papel.Descricao, papel.Ativo,
            posicao.QuantidadeAtual, posicao.PrecoMedio,
            papel.PapelSegmentos.Select(ps => ps.SegmentoId).ToList(),
            CalculadoraAluguel.QuantidadeEmAluguel(papel, posicao.QuantidadeAtual),
            papel.CotacaoAtualizadaEm,
            papel.FonteCotacao);
    }

    /// <summary>
    /// Fórmulas idênticas às de Papel/Detalhes do MinhasFinancas (PapelController.Detalhes),
    /// inclusive as peculiaridades: "Evolução" soma Qtd × Valor também nas vendas, e o "$ Médio"
    /// do dividendo usa as primeiras transações (por data) até cobrir a quantidade do provento,
    /// dividindo pela quantidade do provento. Divisões por zero retornam 0 (o antigo retornava NaN).
    /// </summary>
    public async Task<PapelDetalheResponse> ObterDetalheAsync(Guid id, CancellationToken ct = default)
    {
        var papel = await ObterOuFalharAsync(id, ct);
        var cotacao = papel.CotacaoAtual;
        var transacoes = papel.Transacoes.OrderBy(t => t.Data).ThenBy(t => t.Id).ToList();

        decimal evolucao = 0;
        decimal evolucaoAtual = 0;
        var itensTransacao = new List<TransacaoDetalheItem>();
        foreach (var t in transacoes)
        {
            evolucao += t.Quantidade * t.ValorUnitario;
            evolucaoAtual += t.Quantidade * cotacao;
            itensTransacao.Add(new TransacaoDetalheItem(
                t.Id, t.Data, t.TipoTransacao, t.ValorUnitario, t.Quantidade, t.Quantidade * t.ValorUnitario,
                evolucao, cotacao, cotacao - t.ValorUnitario, evolucaoAtual, t.Descricao));
        }

        decimal evolucaoDividendos = 0;
        var itensDividendo = new List<DividendoDetalheItem>();
        foreach (var d in papel.Dividendos.OrderBy(d => d.Data).ThenBy(d => d.Id))
        {
            evolucaoDividendos += d.ValorRecebido;

            decimal somaQuantidade = 0;
            decimal somaCusto = 0;
            foreach (var t in transacoes)
            {
                if (somaQuantidade >= d.Quantidade)
                {
                    break;
                }
                somaQuantidade += t.Quantidade;
                somaCusto += t.Quantidade * t.ValorUnitario;
            }

            var precoMedio = d.Quantidade != 0 ? somaCusto / d.Quantidade : 0m;
            var custoBase = precoMedio * d.Quantidade;
            var valorUnitario = d.Quantidade != 0 ? d.ValorRecebido / d.Quantidade : 0m;

            itensDividendo.Add(new DividendoDetalheItem(
                d.Id, d.Data, d.TipoDividendo, d.ValorRecebido, d.Quantidade, valorUnitario, precoMedio,
                custoBase != 0 ? d.ValorRecebido / custoBase * 100m : 0m,
                cotacao,
                cotacao != 0 ? valorUnitario * 100m / cotacao : 0m,
                evolucaoDividendos,
                custoBase != 0 ? evolucaoDividendos * 100m / custoBase : 0m,
                d.Descricao));
        }

        return new PapelDetalheResponse(ParaResponse(papel), itensTransacao, itensDividendo);
    }

    public async Task<PapeisGeralResponse> ObterPapeisGeralAsync(
        TipoPapel? tipoPapel = null, bool? somenteAtivos = true, DateOnly? dataFinal = null, CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;
        var todosPapeis = await _papelRepository.ListarTodosDoUsuarioAsync(usuarioId, ct);

        if (tipoPapel.HasValue)
        {
            todosPapeis = todosPapeis.Where(p => p.TipoPapel == tipoPapel.Value).ToList();
        }

        var itensCalculados = new List<ItemIntermediario>();
        decimal dividendosTodosPapeis = 0;
        decimal investidoTodosPapeis = 0;

        foreach (var papel in todosPapeis)
        {
            var transacoes = papel.Transacoes.AsEnumerable();
            if (dataFinal.HasValue)
            {
                transacoes = transacoes.Where(t => t.Data <= dataFinal.Value);
            }

            var posicao = CalculadoraPosicao.Calcular(transacoes);
            var ehAtivo = posicao.QuantidadeAtual > 0;

            // DY total (sistema antigo): considera todos os papéis do tipo, antes do filtro de situação.
            dividendosTodosPapeis += papel.Dividendos
                .Where(d => !dataFinal.HasValue || d.Data <= dataFinal.Value).Sum(d => d.ValorRecebido);
            investidoTodosPapeis += posicao.QuantidadeAtual * posicao.PrecoMedio;

            if (somenteAtivos.HasValue)
            {
                if (somenteAtivos.Value && !ehAtivo)
                {
                    continue;
                }
                if (!somenteAtivos.Value && ehAtivo)
                {
                    continue;
                }
            }

            var dividendos = papel.Dividendos.AsEnumerable();
            if (dataFinal.HasValue)
            {
                dividendos = dividendos.Where(d => d.Data <= dataFinal.Value);
            }

            var emAluguel = CalculadoraAluguel.QuantidadeEmAluguel(papel, posicao.QuantidadeAtual, dataFinal);
            var totalDividendos = dividendos.Sum(d => d.ValorRecebido);
            var totalCompra = posicao.QuantidadeAtual * posicao.PrecoMedio;
            var totalAtual = posicao.QuantidadeAtual * papel.CotacaoAtual;
            var ganhoUnitario = papel.CotacaoAtual - posicao.PrecoMedio;
            var ganhoTotal = totalAtual - totalCompra;
            var valorizacaoPercentual = totalCompra > 0 ? ((totalAtual / totalCompra) * 100m) - 100m : 0m;
            var percentualDividendos = totalCompra > 0 ? (totalDividendos / totalCompra) * 100m : 0m;

            itensCalculados.Add(new ItemIntermediario(
                papel.Id,
                papel.Codigo,
                papel.Nome,
                papel.TipoPapel,
                posicao.QuantidadeAtual,
                posicao.PrecoMedio,
                totalCompra,
                papel.CotacaoAtual,
                totalAtual,
                valorizacaoPercentual,
                ganhoUnitario,
                ganhoTotal,
                totalDividendos,
                percentualDividendos,
                ehAtivo,
                emAluguel,
                papel.CotacaoAtualizadaEm));
        }

        var totalInvestidoGeral = itensCalculados.Sum(i => i.TotalCompra);
        var totalAtualGeral = itensCalculados.Sum(i => i.TotalAtual);
        var totalDividendosGeral = itensCalculados.Sum(i => i.TotalDividendos);
        var ganhoTotalGeral = totalAtualGeral - totalInvestidoGeral;
        var valorizacaoGeral = totalInvestidoGeral > 0 ? ((totalAtualGeral / totalInvestidoGeral) * 100m) - 100m : 0m;

        var itensFinais = itensCalculados.Select(i => new PapelGeralItemResponse(
            i.Id,
            i.Codigo,
            i.Nome,
            i.TipoPapel,
            i.QuantidadeTotal,
            i.PrecoMedio,
            i.TotalCompra,
            totalInvestidoGeral > 0 ? (i.TotalCompra / totalInvestidoGeral) * 100m : 0m,
            i.CotacaoAtual,
            i.TotalAtual,
            totalAtualGeral > 0 ? (i.TotalAtual / totalAtualGeral) * 100m : 0m,
            i.ValorizacaoPercentual,
            i.GanhoUnitario,
            i.GanhoTotal,
            i.TotalDividendos,
            i.PercentualDividendos,
            i.Ativo,
            i.QuantidadeEmAluguel,
            i.CotacaoAtualizadaEm
        )).OrderBy(i => i.Codigo).ToList();

        return new PapeisGeralResponse(
            itensFinais,
            totalInvestidoGeral,
            totalAtualGeral,
            totalDividendosGeral,
            ganhoTotalGeral,
            valorizacaoGeral,
            investidoTodosPapeis > 0 ? dividendosTodosPapeis / investidoTodosPapeis * 100m : 0m);
    }

    private sealed record ItemIntermediario(
        Guid Id,
        string Codigo,
        string Nome,
        TipoPapel TipoPapel,
        decimal QuantidadeTotal,
        decimal PrecoMedio,
        decimal TotalCompra,
        decimal CotacaoAtual,
        decimal TotalAtual,
        decimal ValorizacaoPercentual,
        decimal GanhoUnitario,
        decimal GanhoTotal,
        decimal TotalDividendos,
        decimal PercentualDividendos,
        bool Ativo,
        decimal QuantidadeEmAluguel,
        DateTime? CotacaoAtualizadaEm);
}
