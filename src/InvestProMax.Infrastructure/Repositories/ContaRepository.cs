using InvestProMax.Application.Contas;
using InvestProMax.Domain.Entidades;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class ContaRepository : IContaRepository
{
    private readonly InvestProMaxDbContext _db;
    private readonly SupabaseDbContext? _supabaseDb;
    private readonly LocalDbContext? _localDb;

    public ContaRepository(
        InvestProMaxDbContext db,
        SupabaseDbContext? supabaseDb = null,
        LocalDbContext? localDb = null)
    {
        _db = db;
        _supabaseDb = supabaseDb;
        _localDb = localDb;
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

        foreach (var secundario in new InvestProMaxDbContext?[] { _supabaseDb, _localDb })
        {
            if (secundario is null || ReferenceEquals(secundario, _db))
            {
                continue;
            }

            try
            {
                await ZerarContasNoContextoAsync(secundario, usuarioId, ct);
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
        var faturaIds = contaIds.Count > 0
            ? await db.Faturas.Where(f => contaIds.Contains(f.ContaId)).Select(f => f.Id).ToListAsync(ct)
            : [];

        if (db.Database.IsRelational())
        {
            if (faturaIds.Count > 0)
            {
                await db.Pagamentos.Where(p => faturaIds.Contains(p.FaturaId)).ExecuteDeleteAsync(ct);
            }

            if (contaIds.Count > 0)
            {
                await db.Faturas.Where(f => contaIds.Contains(f.ContaId)).ExecuteDeleteAsync(ct);
            }

            await db.Contas.Where(c => c.UsuarioId == usuarioId).ExecuteDeleteAsync(ct);
            return;
        }

        if (faturaIds.Count > 0)
        {
            db.Pagamentos.RemoveRange(await db.Pagamentos.Where(p => faturaIds.Contains(p.FaturaId)).ToListAsync(ct));
        }

        if (contaIds.Count > 0)
        {
            db.Faturas.RemoveRange(await db.Faturas.Where(f => contaIds.Contains(f.ContaId)).ToListAsync(ct));
        }

        db.Contas.RemoveRange(await db.Contas.Where(c => c.UsuarioId == usuarioId).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
    }
}
