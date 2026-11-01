using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class PapelConfiguration : IEntityTypeConfiguration<Papel>
{
    public void Configure(EntityTypeBuilder<Papel> builder)
    {
        builder.ToTable("Papeis");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).IsRequired().HasMaxLength(20);
        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Descricao).HasMaxLength(1000);
        builder.Property(p => p.CotacaoAtual).HasPrecision(18, 6);
        builder.Property(p => p.CotacaoAtualizadaEm);
        builder.Property(p => p.FonteCotacao).HasMaxLength(30);
        builder.Property(p => p.TipoPapel).HasConversion<string>().HasMaxLength(20);

        // Um mesmo código de papel não deve se repetir para o mesmo usuário.
        builder.HasIndex(p => new { p.UsuarioId, p.Codigo }).IsUnique();

        builder.HasMany(p => p.Transacoes).WithOne(t => t.Papel).HasForeignKey(t => t.PapelId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Dividendos).WithOne(d => d.Papel).HasForeignKey(d => d.PapelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
