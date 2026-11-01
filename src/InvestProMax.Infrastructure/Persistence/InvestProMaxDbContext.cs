using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Infrastructure.Persistence;

public class InvestProMaxDbContext : DbContext
{
    public InvestProMaxDbContext(DbContextOptions<InvestProMaxDbContext> options) : base(options) { }
    protected InvestProMaxDbContext(DbContextOptions options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Papel> Papeis => Set<Papel>();
    public DbSet<Transacao> Transacoes => Set<Transacao>();
    public DbSet<Dividendo> Dividendos => Set<Dividendo>();
    public DbSet<MovimentoAluguel> MovimentosAluguel => Set<MovimentoAluguel>();
    public DbSet<Segmento> Segmentos => Set<Segmento>();
    public DbSet<PapelSegmento> PapelSegmentos => Set<PapelSegmento>();
    public DbSet<Ativo> Ativos => Set<Ativo>();
    public DbSet<Inventario> Inventarios => Set<Inventario>();
    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<Fatura> Faturas => Set<Fatura>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvestProMaxDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        if (Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            modelBuilder.Entity<Inventario>()
                .Property<int>("Ano")
                .HasComputedColumnSql("CAST(EXTRACT(YEAR FROM \"Data\") AS integer)", stored: true);

            modelBuilder.Entity<Inventario>()
                .Property<int>("Mes")
                .HasComputedColumnSql("CAST(EXTRACT(MONTH FROM \"Data\") AS integer)", stored: true);
        }
    }
}

public sealed class SupabaseDbContext : InvestProMaxDbContext
{
    public SupabaseDbContext(DbContextOptions<SupabaseDbContext> options) : base(options) { }
}

public sealed class LocalDbContext : InvestProMaxDbContext
{
    public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options) { }
}
