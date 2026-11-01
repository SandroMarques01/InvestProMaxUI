using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestProMax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UsuarioTelegramId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TelegramId",
                table: "Usuarios",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelegramId",
                table: "Usuarios");
        }
    }
}
