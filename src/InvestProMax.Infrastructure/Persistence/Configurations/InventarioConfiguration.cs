using InvestProMax.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestProMax.Infrastructure.Persistence.Configurations;

public class InventarioConfiguration : IEntityTypeConfiguration<Inventario>
{
    public void Configure(EntityTypeBuilder<Inventario> builder)
    {
        builder.ToTable("Inventarios");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Valor).HasPrecision(18, 2);
        builder.Property(i => i.Descricao).HasMaxLength(1000);

        // Ano/Mes: shadow properties (não existem na classe de domínio) computadas pelo SQL
        // Server a partir de Data, só para permitir o índice único exigido pela especificação
        // (seção 5: "índice único em (AtivoId, Ano, Mes)"). Ver observação na entidade Inventario.
        builder.Property<int>("Ano")
            .HasComputedColumnSql("DATEPART(YEAR, [Data])", stored: true);
        builder.Property<int>("Mes")
            .HasComputedColumnSql("DATEPART(MONTH, [Data])", stored: true);

        builder.HasIndex(i => i.AtivoId, "IX_Inventarios_AtivoId"); // consulta comum: histórico por ativo
        builder.HasIndex("AtivoId", "Ano", "Mes")
            .IsUnique()
            .HasDatabaseName("IX_Inventarios_AtivoId_Ano_Mes");
    }
}
