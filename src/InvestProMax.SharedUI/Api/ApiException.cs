using InvestProMax.SharedUI.Dtos;

namespace InvestProMax.SharedUI.Api;

public sealed class ApiException : Exception
{
    public int? StatusCode { get; }
    public StatusCadastroUsuario? StatusCadastro { get; }
    public string? CodigoValidacaoTelegram { get; }
    public string? LinkTelegramBot { get; }

    public ApiException(
        int? statusCode,
        string message,
        Exception? innerException = null,
        StatusCadastroUsuario? statusCadastro = null,
        string? codigoValidacaoTelegram = null,
        string? linkTelegramBot = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        StatusCadastro = statusCadastro;
        CodigoValidacaoTelegram = codigoValidacaoTelegram;
        LinkTelegramBot = linkTelegramBot;
    }

    public bool EhNaoAutorizado => StatusCode == 401;
    public bool EhNaoEncontrado => StatusCode == 404;
    public bool EhCadastroPendente => StatusCode == 403 && StatusCadastro.HasValue;
}
