using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TicketsSistemas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarColaboradoresYAsignacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActualizadoPorId",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AsignadoAId",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Colaboradores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombreCompleto = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    EsAdministrador = table.Column<bool>(type: "boolean", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Creado = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Actualizado = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Colaboradores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ActualizadoPorId",
                table: "Tickets",
                column: "ActualizadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AsignadoAId",
                table: "Tickets",
                column: "AsignadoAId");

            migrationBuilder.CreateIndex(
                name: "IX_Colaboradores_Email",
                table: "Colaboradores",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Colaboradores_ActualizadoPorId",
                table: "Tickets",
                column: "ActualizadoPorId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Colaboradores_AsignadoAId",
                table: "Tickets",
                column: "AsignadoAId",
                principalTable: "Colaboradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Colaboradores_ActualizadoPorId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Colaboradores_AsignadoAId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "Colaboradores");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ActualizadoPorId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_AsignadoAId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ActualizadoPorId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AsignadoAId",
                table: "Tickets");
        }
    }
}
