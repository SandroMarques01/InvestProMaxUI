using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class MovimentoAluguelConfiguration : IEntityTypeConfiguration<MovimentoAluguel>
{
    public void Configure(EntityTypeBuilder<MovimentoAluguel> builder)
    {
        builder.ToTable("MovimentosAluguel");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Quantidade).HasPrecision(18, 6);
        builder.Property(m => m.Descricao).HasMaxLength(500);
        builder.Property(m => m.OrigemImportacaoHash).HasMaxLength(64);

        builder.HasIndex(m => new { m.PapelId, m.Data });
        builder.HasIndex(m => m.OrigemImportacaoHash);

        builder.HasOne(m => m.Papel).WithMany(p => p.MovimentosAluguel).HasForeignKey(m => m.PapelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
