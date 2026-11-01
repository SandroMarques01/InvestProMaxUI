namespace InvestProMax.SharedUI.Api;

public sealed class ApiException : Exception
{
    public int? StatusCode { get; }

    public ApiException(int? statusCode, string message, Exception? innerException = null) 
        : base(message, innerException) => StatusCode = statusCode;

    public bool EhNaoAutorizado => StatusCode == 401;
    public bool EhNaoEncontrado => StatusCode == 404;
}
