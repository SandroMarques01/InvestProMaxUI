using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Common.Exceptions;

public class CadastroPendenteException : Exception
{
    public StatusCadastroUsuario StatusCadastro { get; }
    public string? CodigoValidacaoTelegram { get; }
    public string? LinkTelegramBot { get; }

    public CadastroPendenteException(
        string message,
        StatusCadastroUsuario statusCadastro,
        string? codigoValidacaoTelegram = null,
        string? linkTelegramBot = null)
        : base(message)
    {
        StatusCadastro = statusCadastro;
        CodigoValidacaoTelegram = codigoValidacaoTelegram;
        LinkTelegramBot = linkTelegramBot;
    }
}
