using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketsSistemas.Api.Migrations
{
    /// <inheritdoc />
    public partial class QuitarPasswordHashUsarEntraId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Colaboradores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Colaboradores",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
