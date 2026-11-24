using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly InvestProMaxDbContext _db;

    public UsuarioRepository(InvestProMaxDbContext db) => _db = db;

    public Task<Usuario?> ObterPorNomeUsuarioAsync(string nomeUsuario, CancellationToken ct = default)
    {
        var identificador = nomeUsuario.Trim().ToLowerInvariant();
        return _db.Usuarios.FirstOrDefaultAsync(
            u => u.NomeUsuario.ToLower() == identificador || u.Email.ToLower() == identificador,
            ct);
    }

    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AtualizarAsync(Usuario usuario, CancellationToken ct = default)
    {
        _db.Usuarios.Update(usuario);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AdicionarAsync(Usuario usuario, CancellationToken ct = default)
    {
        await _db.Usuarios.AddAsync(usuario, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> ExisteNomeUsuarioAsync(string nomeUsuario, CancellationToken ct = default)
    {
        var normalizado = nomeUsuario.Trim().ToLowerInvariant();
        return _db.Usuarios.AnyAsync(u => u.NomeUsuario.ToLower() == normalizado, ct);
    }

    public Task<bool> ExisteEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizado = email.Trim().ToLowerInvariant();
        return _db.Usuarios.AnyAsync(u => u.Email.ToLower() == normalizado, ct);
    }

    public async Task<IReadOnlyList<Guid>> ListarIdsAtivosAsync(CancellationToken ct = default) =>
        await _db.Usuarios.Where(u => u.Ativo).Select(u => u.Id).ToListAsync(ct);
}
