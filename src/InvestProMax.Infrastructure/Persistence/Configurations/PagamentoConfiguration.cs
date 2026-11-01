using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("Pagamentos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ValorPago).HasPrecision(18, 2);
        builder.Property(p => p.FormaPagamento).HasMaxLength(50);
        builder.Property(p => p.Descricao).HasMaxLength(1000);

        // Uma Fatura é quitada por, no máximo, um Pagamento (relação 1:1 — seção 5).
        builder.HasIndex(p => p.FaturaId).IsUnique();
    }
}
