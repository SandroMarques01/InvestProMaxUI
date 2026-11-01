using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class AtivoConfiguration : IEntityTypeConfiguration<Ativo>
{
    public void Configure(EntityTypeBuilder<Ativo> builder)
    {
        builder.ToTable("Ativos");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Nome).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Descricao).HasMaxLength(1000);
        builder.Property(a => a.TipoAtivo).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.TipoOrigem).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.TipoPapelVinculado).HasConversion<string>().HasMaxLength(20);

        builder.HasMany(a => a.HistoricoInventario).WithOne(i => i.Ativo).HasForeignKey(i => i.AtivoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
