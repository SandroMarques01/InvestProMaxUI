using InvestProMax.Domain.Entidades;

namespace InvestProMax.Application.Common.Interfaces;

public interface ITokenService
{
    /// <summary>Gera o access token JWT (vida curta) para o usuário informado.</summary>
    TokenGerado GerarAccessToken(Usuario usuario);

    /// <summary>Tempo de vida do access token, em minutos — exposto para a Application montar a resposta.</summary>
    int AccessTokenExpiracaoMinutos { get; }
}

public sealed record TokenGerado(string Token, DateTime ExpiraEmUtc);
