using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class ContaConfiguration : IEntityTypeConfiguration<Conta>
{
    public void Configure(EntityTypeBuilder<Conta> builder)
    {
        builder.ToTable("Contas");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nome).IsRequired().HasMaxLength(200);
        builder.Property(c => c.ValorEstimado).HasPrecision(18, 2);
        builder.Property(c => c.Categoria).HasMaxLength(100);
        builder.Property(c => c.Descricao).HasMaxLength(1000);
        builder.Property(c => c.Recorrencia).HasConversion<string>().HasMaxLength(20);

        builder.HasMany(c => c.Faturas).WithOne(f => f.Conta).HasForeignKey(f => f.ContaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
