using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuildArena.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildLevelAndXp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentXP",
                table: "Guilds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "Guilds",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentXP",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Guilds");
        }
    }
}
