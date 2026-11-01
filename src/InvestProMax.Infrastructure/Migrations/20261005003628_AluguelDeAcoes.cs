using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestProMax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AluguelDeAcoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MovimentosAluguel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PapelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OrigemImportacaoHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentosAluguel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentosAluguel_Papeis_PapelId",
                        column: x => x.PapelId,
                        principalTable: "Papeis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosAluguel_OrigemImportacaoHash",
                table: "MovimentosAluguel",
                column: "OrigemImportacaoHash");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosAluguel_PapelId_Data",
                table: "MovimentosAluguel",
                columns: new[] { "PapelId", "Data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimentosAluguel");
        }
    }
}
