using InvestProMax.Application.Contas;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Faturas;

/// <summary>
/// Núcleo das regras 7 e 8, deliberadamente separado de <see cref="FaturaService"/> para ser
/// chamado tanto a partir de uma requisição autenticada (POST /faturas/gerar, escopado ao
/// usuário do token) quanto do job diário (seção 9), que não tem HttpContext/JWT e por isso
/// não pode depender de IUsuarioLogado — aqui o usuarioId é sempre um parâmetro explícito.
/// </summary>
internal static class GeradorDeFaturas
{
    /// <summary>
    /// Regra 7: gera uma Fatura Pendente para cada Conta ativa do usuário que ainda não tem
    /// fatura na competência informada. DataVencimento vem de Conta.DiaVencimento, ajustado
    /// para o último dia do mês quando o mês não tem esse dia (ex.: DiaVencimento = 31 em
    /// fevereiro cai no dia 28 ou 29).
    /// </summary>
    public static async Task<int> GerarParaUsuarioAsync(
        Guid usuarioId, DateOnly competencia, IContaRepository contaRepository, IFaturaRepository faturaRepository, CancellationToken ct)
    {
        var contasAtivas = await contaRepository.ListarAtivasDoUsuarioAsync(usuarioId, ct);
        var criadas = 0;

        foreach (var conta in contasAtivas)
        {
            var existente = await faturaRepository.ObterPorContaECompetenciaAsync(conta.Id, competencia, ct);
            if (existente is not null)
            {
                continue;
            }

            await faturaRepository.AdicionarAsync(new Fatura
            {
                ContaId = conta.Id,
                Competencia = competencia,
                DataVencimento = CalcularDataVencimento(competencia, conta.DiaVencimento),
                ValorPrevisto = conta.ValorEstimado,
                Status = StatusFatura.Pendente
            }, ct);
            criadas++;
        }

        return criadas;
    }

    public static DateOnly CalcularDataVencimento(DateOnly competencia, int diaVencimento)
    {
        var ultimoDiaDoMes = DateTime.DaysInMonth(competencia.Year, competencia.Month);
        var dia = Math.Min(diaVencimento, ultimoDiaDoMes);
        return new DateOnly(competencia.Year, competencia.Month, dia);
    }

    /// <summary>
    /// Regra 8, metade "job periódico": persiste a reclassificação Pendente → Atrasada para
    /// faturas vencidas. A outra metade (cálculo em tempo de leitura) fica em
    /// <see cref="EfetivarStatus"/>, usada por FaturaService.ListarAsync — ver comentário lá
    /// sobre por que as duas abordagens coexistem.
    /// </summary>
    public static async Task<int> AtualizarAtrasadasParaUsuarioAsync(
        Guid usuarioId, DateOnly hoje, IFaturaRepository faturaRepository, CancellationToken ct)
    {
        var pendentesVencidas = await faturaRepository.ListarPendentesVencidasAsync(usuarioId, hoje, ct);
        if (pendentesVencidas.Count == 0)
        {
            return 0;
        }

        foreach (var fatura in pendentesVencidas)
        {
            fatura.Status = StatusFatura.Atrasada;
        }
        await faturaRepository.AtualizarEmLoteAsync(pendentesVencidas, ct);

        return pendentesVencidas.Count;
    }

    /// <summary>Status "efetivo" para exibição: uma Fatura Pendente vencida é mostrada como
    /// Atrasada mesmo que o job diário ainda não tenha persistido a reclassificação.</summary>
    public static StatusFatura EfetivarStatus(Fatura fatura, DateOnly hoje) =>
        fatura.Status == StatusFatura.Pendente && fatura.DataVencimento < hoje ? StatusFatura.Atrasada : fatura.Status;
}
