using InvestProMax.Application.Common;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Contas;

public interface IContaService
{
    Task<PaginaResultado<ContaResponse>> ListarAsync(ContaFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default);
    Task<ContaResponse> ObterAsync(Guid id, CancellationToken ct = default);
    Task<ContaResponse> CriarAsync(CriarContaRequest request, CancellationToken ct = default);
    Task<ContaResponse> AtualizarAsync(Guid id, AtualizarContaRequest request, CancellationToken ct = default);
    Task RemoverAsync(Guid id, CancellationToken ct = default);
}

public sealed class ContaService : IContaService
{
    private readonly IContaRepository _repository;
    private readonly IUsuarioLogado _usuarioLogado;

    public ContaService(IContaRepository repository, IUsuarioLogado usuarioLogado)
    {
        _repository = repository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<PaginaResultado<ContaResponse>> ListarAsync(
        ContaFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default)
    {
        var (itens, total) = await _repository.ListarAsync(_usuarioLogado.UsuarioId, filtro, paginacao.Skip, paginacao.TamanhoEfetivo, ct);
        return new PaginaResultado<ContaResponse>(itens.Select(ParaResponse).ToList(), total, paginacao.PaginaEfetiva, paginacao.TamanhoEfetivo);
    }

    public async Task<ContaResponse> ObterAsync(Guid id, CancellationToken ct = default) =>
        ParaResponse(await ObterOuFalharAsync(id, ct));

    public async Task<ContaResponse> CriarAsync(CriarContaRequest request, CancellationToken ct = default)
    {
        var conta = new Conta
        {
            UsuarioId = _usuarioLogado.UsuarioId,
            Nome = request.Nome,
            ValorEstimado = request.ValorEstimado,
            DiaVencimento = request.DiaVencimento,
            Recorrencia = request.Recorrencia,
            NumeroParcelas = request.NumeroParcelas,
            Categoria = request.Categoria,
            Descricao = request.Descricao,
            StatusAtivo = true
        };

        await _repository.AdicionarAsync(conta, ct);
        return ParaResponse(conta);
    }

    public async Task<ContaResponse> AtualizarAsync(Guid id, AtualizarContaRequest request, CancellationToken ct = default)
    {
        var conta = await ObterOuFalharAsync(id, ct);

        conta.Nome = request.Nome;
        conta.ValorEstimado = request.ValorEstimado;
        conta.DiaVencimento = request.DiaVencimento;
        conta.Recorrencia = request.Recorrencia;
        conta.NumeroParcelas = request.NumeroParcelas;
        conta.Categoria = request.Categoria;
        conta.Descricao = request.Descricao;
        conta.StatusAtivo = request.StatusAtivo;

        await _repository.AtualizarAsync(conta, ct);
        return ParaResponse(conta);
    }

    /// <summary>Mesma lógica defensiva aplicada a Papel (Fase 2) e Ativo (Fase 4): uma Conta
    /// com Faturas geradas não pode ser removida, só desativada.</summary>
    public async Task RemoverAsync(Guid id, CancellationToken ct = default)
    {
        var conta = await ObterOuFalharAsync(id, ct);

        if (conta.Faturas.Count > 0)
        {
            throw new RegraDeNegocioException(
                "Esta conta já tem faturas geradas e não pode ser removida. Desative-a em vez disso (StatusAtivo = false).");
        }

        await _repository.RemoverAsync(conta, ct);
    }

    private async Task<Conta> ObterOuFalharAsync(Guid id, CancellationToken ct) =>
        await _repository.ObterPorIdAsync(id, _usuarioLogado.UsuarioId, ct)
            ?? throw new NaoEncontradoException("Conta não encontrada.");

    private static ContaResponse ParaResponse(Conta c) => new(
        c.Id, c.Nome, c.ValorEstimado, c.DiaVencimento, c.Recorrencia, c.NumeroParcelas, c.Categoria, c.Descricao, c.StatusAtivo);
}
