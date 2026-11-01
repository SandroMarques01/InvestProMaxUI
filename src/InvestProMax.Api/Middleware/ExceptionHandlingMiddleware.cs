using System.Net;
using FluentValidation;
using InvestProMax.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace InvestProMax.Api.Middleware;

/// <summary>
/// Traduz exceções da Application (seção 8: "Erros devem seguir o formato ProblemDetails")
/// em respostas RFC 7807 consistentes, para não vazar stack trace nem detalhes internos ao cliente.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await TratarAsync(context, ex);
        }
    }

    private async Task TratarAsync(HttpContext context, Exception ex)
    {
        var (status, titulo) = ex switch
        {
            ValidationException => (HttpStatusCode.BadRequest, "Um ou mais campos são inválidos."),
            BadHttpRequestException badRequest => ((HttpStatusCode)badRequest.StatusCode, badRequest.Message),
            NaoAutorizadoException => (HttpStatusCode.Unauthorized, ex.Message),
            NaoEncontradoException => (HttpStatusCode.NotFound, ex.Message),
            RegraDeNegocioException => (HttpStatusCode.UnprocessableEntity, ex.Message),
            InvalidDataException => (HttpStatusCode.BadRequest, ex.Message),
            _ => (HttpStatusCode.InternalServerError, "Ocorreu um erro inesperado.")
        };

        if (status == HttpStatusCode.InternalServerError)
        {
            // Erros não previstos são logados com detalhe completo; o cliente só recebe a mensagem genérica acima.
            _logger.LogError(ex, "Erro não tratado ao processar {Metodo} {Caminho}", context.Request.Method, context.Request.Path);
        }

        var problemDetails = new ProblemDetails
        {
            Status = (int)status,
            Title = titulo,
            Instance = context.Request.Path
        };

        if (ex is ValidationException validationException)
        {
            problemDetails.Extensions["erros"] = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
    }
}
