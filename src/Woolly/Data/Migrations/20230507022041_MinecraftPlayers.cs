using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Woolly.Data.Migrations
{
    /// <inheritdoc />
    public partial class MinecraftPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "minecraft_role_id",
                table: "guilds",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "minecraft_players",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guild_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    discord_user_id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    minecraft_username = table.Column<string>(type: "text", nullable: false),
                    minecraft_uuid = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_minecraft_players", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "minecraft_player_minecraft_server",
                columns: table => new
                {
                    players_id = table.Column<int>(type: "integer", nullable: false),
                    servers_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_minecraft_player_minecraft_server", x => new { x.players_id, x.servers_id });
                    table.ForeignKey(
                        name: "fk_minecraft_player_minecraft_server_minecraft_players_players",
                        column: x => x.players_id,
                        principalTable: "minecraft_players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_minecraft_player_minecraft_server_minecraft_servers_servers",
                        column: x => x.servers_id,
                        principalTable: "minecraft_servers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_minecraft_player_minecraft_server_servers_id",
                table: "minecraft_player_minecraft_server",
                column: "servers_id");

            migrationBuilder.CreateIndex(
                name: "ix_minecraft_players_guild_id_minecraft_uuid",
                table: "minecraft_players",
                columns: new[] { "guild_id", "minecraft_uuid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "minecraft_player_minecraft_server");

            migrationBuilder.DropTable(
                name: "minecraft_players");

            migrationBuilder.DropColumn(
                name: "minecraft_role_id",
                table: "guilds");
        }
    }
}
