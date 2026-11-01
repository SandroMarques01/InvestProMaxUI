using InvestProMax.Application.Contas;
using InvestProMax.Domain.Entidades;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class ContaRepository : IContaRepository
{
    private readonly InvestProMaxDbContext _db;
    private readonly IDbContextFactory<SupabaseDbContext>? _supabaseFactory;

    public ContaRepository(InvestProMaxDbContext db, IDbContextFactory<SupabaseDbContext>? supabaseFactory = null)
    {
        _db = db;
        _supabaseFactory = supabaseFactory;
    }

    public Task<Conta?> ObterPorIdAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        _db.Contas.Include(c => c.Faturas).FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId, ct);

    public async Task<(IReadOnlyList<Conta> Itens, int Total)> ListarAsync(
        Guid usuarioId, ContaFiltro filtro, int skip, int tamanhoPagina, CancellationToken ct = default)
    {
        var query = _db.Contas.Where(c => c.UsuarioId == usuarioId);

        if (filtro.StatusAtivo is not null)
        {
            query = query.Where(c => c.StatusAtivo == filtro.StatusAtivo);
        }

        var total = await query.CountAsync(ct);
        var itens = await query.OrderBy(c => c.Nome).Skip(skip).Take(tamanhoPagina).ToListAsync(ct);

        return (itens, total);
    }

    public async Task<IReadOnlyList<Conta>> ListarAtivasDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        await _db.Contas.Where(c => c.UsuarioId == usuarioId && c.StatusAtivo).ToListAsync(ct);

    public async Task AdicionarAsync(Conta conta, CancellationToken ct = default)
    {
        await _db.Contas.AddAsync(conta, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Conta conta, CancellationToken ct = default)
    {
        _db.Contas.Update(conta);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Conta conta, CancellationToken ct = default)
    {
        _db.Contas.Remove(conta);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ZerarContasDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        await ZerarContasNoContextoAsync(_db, usuarioId, ct);

        if (_supabaseFactory is not null)
        {
            try
            {
                await using var supabase = await _supabaseFactory.CreateDbContextAsync(ct);
                await ZerarContasNoContextoAsync(supabase, usuarioId, ct);
            }
            catch
            {
                // Falha na base secundária não interrompe a operação na base principal.
            }
        }
    }

    private static async Task ZerarContasNoContextoAsync(InvestProMaxDbContext db, Guid usuarioId, CancellationToken ct)
    {
        var contaIds = await db.Contas.Where(c => c.UsuarioId == usuarioId).Select(c => c.Id).ToListAsync(ct);
        var faturaIds = await db.Faturas.Where(f => contaIds.Contains(f.ContaId)).Select(f => f.Id).ToListAsync(ct);

        await db.Pagamentos.Where(p => faturaIds.Contains(p.FaturaId)).ExecuteDeleteAsync(ct);
        await db.Faturas.Where(f => contaIds.Contains(f.ContaId)).ExecuteDeleteAsync(ct);
        await db.Contas.Where(c => c.UsuarioId == usuarioId).ExecuteDeleteAsync(ct);
    }
}
