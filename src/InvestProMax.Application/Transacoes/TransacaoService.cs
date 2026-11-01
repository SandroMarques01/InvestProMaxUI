using InvestProMax.Application.Common;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Transacoes;

public interface ITransacaoService
{
    Task<PaginaResultado<TransacaoResponse>> ListarAsync(TransacaoFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default);
    Task<TransacaoResponse> ObterAsync(Guid id, CancellationToken ct = default);
    Task<TransacaoResponse> CriarAsync(CriarTransacaoRequest request, CancellationToken ct = default);
    Task<TransacaoResponse> AtualizarAsync(Guid id, AtualizarTransacaoRequest request, CancellationToken ct = default);
    Task RemoverAsync(Guid id, CancellationToken ct = default);
}

public sealed class TransacaoService : ITransacaoService
{
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IPapelRepository _papelRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public TransacaoService(
        ITransacaoRepository transacaoRepository, IPapelRepository papelRepository, IUsuarioLogado usuarioLogado)
    {
        _transacaoRepository = transacaoRepository;
        _papelRepository = papelRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<PaginaResultado<TransacaoResponse>> ListarAsync(
        TransacaoFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default)
    {
        var (itens, total) = await _transacaoRepository.ListarAsync(
            _usuarioLogado.UsuarioId, filtro, paginacao.Skip, paginacao.TamanhoEfetivo, ct);

        return new PaginaResultado<TransacaoResponse>(
            itens.Select(ParaResponse).ToList(), total, paginacao.PaginaEfetiva, paginacao.TamanhoEfetivo);
    }

    public async Task<TransacaoResponse> ObterAsync(Guid id, CancellationToken ct = default)
    {
        var transacao = await ObterOuFalharAsync(id, ct);
        return ParaResponse(transacao);
    }

    public async Task<TransacaoResponse> CriarAsync(CriarTransacaoRequest request, CancellationToken ct = default)
    {
        var papel = await _papelRepository.ObterPorIdAsync(request.PapelId, _usuarioLogado.UsuarioId, ct: ct)
            ?? throw new NaoEncontradoException("Papel não encontrado.");

        var transacao = new Transacao
        {
            PapelId = papel.Id,
            TipoTransacao = request.TipoTransacao,
            Quantidade = request.Quantidade,
            ValorUnitario = request.ValorUnitario,
            Data = request.Data,
            Descricao = request.Descricao,
            Historico = false
        };

        await _transacaoRepository.AdicionarAsync(transacao, ct);
        await RecalcularStatusDoPapelAsync(papel, ct);

        return ParaResponse(transacao);
    }

    public async Task<TransacaoResponse> AtualizarAsync(Guid id, AtualizarTransacaoRequest request, CancellationToken ct = default)
    {
        var transacao = await ObterOuFalharAsync(id, ct);

        transacao.TipoTransacao = request.TipoTransacao;
        transacao.Quantidade = request.Quantidade;
        transacao.ValorUnitario = request.ValorUnitario;
        transacao.Data = request.Data;
        transacao.Descricao = request.Descricao;

        await _transacaoRepository.AtualizarAsync(transacao, ct);

        var papel = await _papelRepository.ObterPorIdAsync(transacao.PapelId, _usuarioLogado.UsuarioId, ct: ct);
        if (papel is not null)
        {
            await RecalcularStatusDoPapelAsync(papel, ct);
        }

        return ParaResponse(transacao);
    }

    public async Task RemoverAsync(Guid id, CancellationToken ct = default)
    {
        var transacao = await ObterOuFalharAsync(id, ct);
        var papelId = transacao.PapelId;

        await _transacaoRepository.RemoverAsync(transacao, ct);

        var papel = await _papelRepository.ObterPorIdAsync(papelId, _usuarioLogado.UsuarioId, ct: ct);
        if (papel is not null)
        {
            await RecalcularStatusDoPapelAsync(papel, ct);
        }
    }

    /// <summary>
    /// Regra 2: recalcula a posição líquida do papel após qualquer mutação em suas transações
    /// e ajusta Papel.Ativo. ASSUNÇÃO SINALIZADA: a especificação só descreve a desativação
    /// (posição chega a zero → Ativo = false); a reativação automática quando a posição volta
    /// a ficar positiva não é mencionada, mas foi adotada aqui por simetria — do contrário o
    /// usuário teria que reativar manualmente o papel toda vez que comprasse de novo depois de
    /// zerar a posição, o que contradiz a ideia de "automaticamente" da regra.
    /// </summary>
    private async Task RecalcularStatusDoPapelAsync(Papel papel, CancellationToken ct)
    {
        var transacoes = await _transacaoRepository.ListarPorPapelAsync(papel.Id, ct);
        var posicao = CalculadoraPosicao.Calcular(transacoes);

        var novoStatus = posicao.QuantidadeAtual > 0;
        if (papel.Ativo != novoStatus)
        {
            papel.Ativo = novoStatus;
            await _papelRepository.AtualizarAsync(papel, ct);
        }
    }

    private async Task<Transacao> ObterOuFalharAsync(Guid id, CancellationToken ct) =>
        await _transacaoRepository.ObterPorIdAsync(id, _usuarioLogado.UsuarioId, ct)
            ?? throw new NaoEncontradoException("Transação não encontrada.");

    private static TransacaoResponse ParaResponse(Transacao t) => new(
        t.Id, t.PapelId, t.TipoTransacao, t.Quantidade, t.ValorUnitario, t.Data, t.Descricao, t.Historico);
}
