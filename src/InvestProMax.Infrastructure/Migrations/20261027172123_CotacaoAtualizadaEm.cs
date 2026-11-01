using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestProMax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CotacaoAtualizadaEm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CotacaoAtualizadaEm",
                table: "Papeis",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FonteCotacao",
                table: "Papeis",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CotacaoAtualizadaEm",
                table: "Papeis");

            migrationBuilder.DropColumn(
                name: "FonteCotacao",
                table: "Papeis");
        }
    }
}
