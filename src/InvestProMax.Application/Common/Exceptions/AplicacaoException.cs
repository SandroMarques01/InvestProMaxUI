namespace InvestProMax.Application.Common.Exceptions;

/// <summary>
/// Base para exceções previstas da camada Application. O middleware de tratamento de erro
/// na Api (seção 8) as traduz para ProblemDetails (RFC 7807) com o status code apropriado.
/// </summary>
public abstract class AplicacaoException : Exception
{
    protected AplicacaoException(string mensagem) : base(mensagem) { }
}

/// <summary>Credenciais inválidas no login, refresh token inválido/expirado, ou senha atual incorreta.</summary>
public sealed class NaoAutorizadoException : AplicacaoException
{
    public NaoAutorizadoException(string mensagem = "Credenciais inválidas.") : base(mensagem) { }
}

/// <summary>Recurso não encontrado ou não pertence ao usuário autenticado (isolamento por usuário — regra 10).</summary>
public sealed class NaoEncontradoException : AplicacaoException
{
    public NaoEncontradoException(string mensagem = "Recurso não encontrado.") : base(mensagem) { }
}

/// <summary>Violação de uma regra de negócio (ex.: índice único de Inventario, dados inconsistentes).</summary>
public sealed class RegraDeNegocioException : AplicacaoException
{
    public RegraDeNegocioException(string mensagem) : base(mensagem) { }
}
