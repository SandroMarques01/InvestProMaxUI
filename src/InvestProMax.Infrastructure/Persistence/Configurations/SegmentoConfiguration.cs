using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class SegmentoConfiguration : IEntityTypeConfiguration<Segmento>
{
    public void Configure(EntityTypeBuilder<Segmento> builder)
    {
        builder.ToTable("Segmentos");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Nome).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Descricao).HasMaxLength(1000);
    }
}

public class PapelSegmentoConfiguration : IEntityTypeConfiguration<PapelSegmento>
{
    public void Configure(EntityTypeBuilder<PapelSegmento> builder)
    {
        builder.ToTable("PapelSegmentos");
        builder.HasKey(ps => new { ps.PapelId, ps.SegmentoId });

        builder.HasOne(ps => ps.Papel).WithMany(p => p.PapelSegmentos).HasForeignKey(ps => ps.PapelId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ps => ps.Segmento).WithMany(s => s.PapelSegmentos).HasForeignKey(ps => ps.SegmentoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
