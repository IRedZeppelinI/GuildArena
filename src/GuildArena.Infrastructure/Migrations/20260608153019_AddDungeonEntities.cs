using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GuildArena.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDungeonEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveDungeonRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<int>(type: "integer", nullable: false),
                    DungeonDefinitionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CurrentStageIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveDungeonRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActiveDungeonRuns_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildDungeonRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<int>(type: "integer", nullable: false),
                    DungeonDefinitionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompletionCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildDungeonRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildDungeonRecords_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DungeonHeroStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActiveDungeonRunId = table.Column<int>(type: "integer", nullable: false),
                    HeroId = table.Column<int>(type: "integer", nullable: false),
                    CurrentHP = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DungeonHeroStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DungeonHeroStates_ActiveDungeonRuns_ActiveDungeonRunId",
                        column: x => x.ActiveDungeonRunId,
                        principalTable: "ActiveDungeonRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DungeonHeroStates_Heroes_HeroId",
                        column: x => x.HeroId,
                        principalTable: "Heroes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveDungeonRuns_GuildId",
                table: "ActiveDungeonRuns",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DungeonHeroStates_ActiveDungeonRunId",
                table: "DungeonHeroStates",
                column: "ActiveDungeonRunId");

            migrationBuilder.CreateIndex(
                name: "IX_DungeonHeroStates_HeroId",
                table: "DungeonHeroStates",
                column: "HeroId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildDungeonRecords_GuildId",
                table: "GuildDungeonRecords",
                column: "GuildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DungeonHeroStates");

            migrationBuilder.DropTable(
                name: "GuildDungeonRecords");

            migrationBuilder.DropTable(
                name: "ActiveDungeonRuns");
        }
    }
}
