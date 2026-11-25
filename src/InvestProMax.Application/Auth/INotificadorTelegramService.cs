namespace InvestProMax.Application.Auth;

public interface INotificadorTelegramService
{
    string BotUsername { get; }
    string MontarLinkValidacao(string codigo);
    IReadOnlyList<long> ObterAdminsConfigurados();
    Task NotificarUsuarioAsync(long telegramId, string mensagem, CancellationToken ct = default);
}
