using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestProMax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FluxoCadastroGoogleTelegramAprovacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AprovadoEmUtc",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoValidacaoTelegram",
                table: "Usuarios",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CodigoValidacaoTelegramExpiraEmUtc",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CriadoEmUtc",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleSubject",
                table: "Usuarios",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCadastro",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_CodigoValidacaoTelegram",
                table: "Usuarios",
                column: "CodigoValidacaoTelegram");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_GoogleSubject",
                table: "Usuarios",
                column: "GoogleSubject");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_CodigoValidacaoTelegram",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_GoogleSubject",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "AprovadoEmUtc",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CodigoValidacaoTelegram",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CodigoValidacaoTelegramExpiraEmUtc",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CriadoEmUtc",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "GoogleSubject",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "StatusCadastro",
                table: "Usuarios");
        }
    }
}
