using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class DividendoConfiguration : IEntityTypeConfiguration<Dividendo>
{
    public void Configure(EntityTypeBuilder<Dividendo> builder)
    {
        builder.ToTable("Dividendos");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.ValorRecebido).HasPrecision(18, 6);
        builder.Property(d => d.Quantidade).HasPrecision(18, 6);
        builder.Property(d => d.Descricao).HasMaxLength(1000);
        builder.Property(d => d.TipoDividendo).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.OrigemImportacaoHash).HasMaxLength(64);

        builder.HasIndex(d => new { d.PapelId, d.Data });
        builder.HasIndex(d => d.OrigemImportacaoHash); // consultado pela importação B3 (regra 5)
    }
}
