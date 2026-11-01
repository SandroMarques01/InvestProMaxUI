using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Contas;

namespace InvestProMax.Application.Faturas;

public sealed record ResultadoJobFaturas(int UsuariosProcessados, int FaturasCriadas, int FaturasReclassificadasAtrasadas);

/// <summary>
/// Seção 9: "um hosted service roda diariamente e aplica as regras 7 e 8 para todos os
/// usuários". Esta classe é o caso de uso em si (testável sem precisar de um
/// BackgroundService de verdade); o hosted service (na Api) só decide quando chamá-la.
/// </summary>
public interface IFaturaGeracaoJobService
{
    Task<ResultadoJobFaturas> ExecutarParaTodosUsuariosAsync(CancellationToken ct = default);
}

public sealed class FaturaGeracaoJobService : IFaturaGeracaoJobService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IContaRepository _contaRepository;
    private readonly IFaturaRepository _faturaRepository;

    public FaturaGeracaoJobService(IUsuarioRepository usuarioRepository, IContaRepository contaRepository, IFaturaRepository faturaRepository)
    {
        _usuarioRepository = usuarioRepository;
        _contaRepository = contaRepository;
        _faturaRepository = faturaRepository;
    }

    public async Task<ResultadoJobFaturas> ExecutarParaTodosUsuariosAsync(CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var competencia = new DateOnly(hoje.Year, hoje.Month, 1);
        var usuarioIds = await _usuarioRepository.ListarIdsAtivosAsync(ct);

        var totalCriadas = 0;
        var totalAtrasadas = 0;

        foreach (var usuarioId in usuarioIds)
        {
            totalCriadas += await GeradorDeFaturas.GerarParaUsuarioAsync(usuarioId, competencia, _contaRepository, _faturaRepository, ct);
            totalAtrasadas += await GeradorDeFaturas.AtualizarAtrasadasParaUsuarioAsync(usuarioId, hoje, _faturaRepository, ct);
        }

        return new ResultadoJobFaturas(usuarioIds.Count, totalCriadas, totalAtrasadas);
    }
}
