using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class TransacaoConfiguration : IEntityTypeConfiguration<Transacao>
{
    public void Configure(EntityTypeBuilder<Transacao> builder)
    {
        builder.ToTable("Transacoes");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Quantidade).HasPrecision(18, 6);
        builder.Property(t => t.ValorUnitario).HasPrecision(18, 6);
        builder.Property(t => t.Descricao).HasMaxLength(1000);
        builder.Property(t => t.TipoTransacao).HasConversion<string>().HasMaxLength(10);
        builder.Property(t => t.OrigemImportacaoHash).HasMaxLength(64);

        builder.HasIndex(t => new { t.PapelId, t.Data });
        builder.HasIndex(t => t.OrigemImportacaoHash); // consultado pela importação B3 (regra 5)
    }
}
