using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class FaturaConfiguration : IEntityTypeConfiguration<Fatura>
{
    public void Configure(EntityTypeBuilder<Fatura> builder)
    {
        builder.ToTable("Faturas");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.ValorPrevisto).HasPrecision(18, 2);
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);

        // Regra 7: "gerar uma Fatura da competência corrente se ainda não existir uma" —
        // reforçado como constraint de banco, não só checagem em memória na Application.
        builder.HasIndex(f => new { f.ContaId, f.Competencia }).IsUnique();

        builder.HasOne(f => f.Pagamento).WithOne(p => p.Fatura).HasForeignKey<Pagamento>(p => p.FaturaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
