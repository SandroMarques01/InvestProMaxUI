using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly InvestProMaxDbContext _db;
    private readonly IDbContextFactory<SupabaseDbContext>? _supabaseFactory;

    public RefreshTokenRepository(InvestProMaxDbContext db, IDbContextFactory<SupabaseDbContext>? supabaseFactory = null)
    {
        _db = db;
        _supabaseFactory = supabaseFactory;
    }

    public async Task AdicionarAsync(RefreshToken token, CancellationToken ct = default)
    {
        await _db.RefreshTokens.AddAsync(token, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default) =>
        _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

    public async Task AtualizarAsync(RefreshToken token, CancellationToken ct = default)
    {
        _db.RefreshTokens.Update(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevogarTodosDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(r => r.UsuarioId == usuarioId && r.RevogadoEmUtc == null && r.ExpiraEmUtc > agora)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RevogadoEmUtc, agora), ct);

        if (_supabaseFactory is not null)
        {
            try
            {
                await using var supabase = await _supabaseFactory.CreateDbContextAsync(ct);
                await supabase.RefreshTokens
                    .Where(r => r.UsuarioId == usuarioId && r.RevogadoEmUtc == null && r.ExpiraEmUtc > agora)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.RevogadoEmUtc, agora), ct);
            }
            catch
            {
                // Falha na base secundária não interrompe a operação na base principal.
            }
        }
    }
}
