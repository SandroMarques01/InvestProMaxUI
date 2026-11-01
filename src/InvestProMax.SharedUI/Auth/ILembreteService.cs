namespace InvestProMax.SharedUI.Auth;

/// <summary>
/// Interface sugerida pela seção 9 da especificação: a SharedUI só declara o contrato de
/// lembrete de vencimento de fatura — cada head decide como notificar (MAUI: notificação
/// local do SO; Web: possivelmente nenhuma implementação ainda, ou e-mail no futuro). Nenhuma
/// tela desta fase chama isto ainda — fica pronta para os heads da Fase 7 injetarem sua
/// própria implementação.
/// </summary>
public interface ILembreteService
{
    Task AgendarLembreteAsync(Guid faturaId, string contaNome, DateOnly dataVencimento, decimal valorPrevisto);
    Task CancelarLembreteAsync(Guid faturaId);
}
