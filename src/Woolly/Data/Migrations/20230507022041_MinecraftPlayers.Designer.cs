﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Woolly.Data;

#nullable disable

namespace Woolly.Data.Migrations
{
    [DbContext(typeof(WoollyContext))]
    [Migration("20230507022041_MinecraftPlayers")]
    partial class MinecraftPlayers
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("MinecraftPlayerMinecraftServer", b =>
                {
                    b.Property<int>("PlayersId")
                        .HasColumnType("integer")
                        .HasColumnName("players_id");

                    b.Property<int>("ServersId")
                        .HasColumnType("integer")
                        .HasColumnName("servers_id");

                    b.HasKey("PlayersId", "ServersId")
                        .HasName("pk_minecraft_player_minecraft_server");

                    b.HasIndex("ServersId")
                        .HasDatabaseName("ix_minecraft_player_minecraft_server_servers_id");

                    b.ToTable("minecraft_player_minecraft_server", (string)null);
                });

            modelBuilder.Entity("Woolly.Data.Models.Guild", b =>
                {
                    b.Property<ulong>("Id")
                        .HasColumnType("numeric(20,0)")
                        .HasColumnName("id");

                    b.Property<ulong?>("MinecraftRoleId")
                        .HasColumnType("numeric(20,0)")
                        .HasColumnName("minecraft_role_id");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.HasKey("Id")
                        .HasName("pk_guilds");

                    b.ToTable("guilds", (string)null);
                });

            modelBuilder.Entity("Woolly.Data.Models.MinecraftPlayer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<ulong>("DiscordUserId")
                        .HasColumnType("numeric(20,0)")
                        .HasColumnName("discord_user_id");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("numeric(20,0)")
                        .HasColumnName("guild_id");

                    b.Property<string>("MinecraftUsername")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("minecraft_username");

                    b.Property<string>("MinecraftUuid")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("minecraft_uuid");

                    b.HasKey("Id")
                        .HasName("pk_minecraft_players");

                    b.HasIndex("GuildId", "MinecraftUuid")
                        .IsUnique()
                        .HasDatabaseName("ix_minecraft_players_guild_id_minecraft_uuid");

                    b.ToTable("minecraft_players", (string)null);
                });

            modelBuilder.Entity("Woolly.Data.Models.MinecraftServer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<ulong>("GuildId")
                        .HasColumnType("numeric(20,0)")
                        .HasColumnName("guild_id");

                    b.Property<string>("Host")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("host");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<int>("PingPort")
                        .HasColumnType("integer")
                        .HasColumnName("ping_port");

                    b.Property<string>("RconPassword")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("rcon_password");

                    b.Property<int>("RconPort")
                        .HasColumnType("integer")
                        .HasColumnName("rcon_port");

                    b.HasKey("Id")
                        .HasName("pk_minecraft_servers");

                    b.HasIndex("GuildId", "Name")
                        .IsUnique()
                        .HasDatabaseName("ix_minecraft_servers_guild_id_name");

                    b.ToTable("minecraft_servers", (string)null);
                });

            modelBuilder.Entity("MinecraftPlayerMinecraftServer", b =>
                {
                    b.HasOne("Woolly.Data.Models.MinecraftPlayer", null)
                        .WithMany()
                        .HasForeignKey("PlayersId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_minecraft_player_minecraft_server_minecraft_players_players");

                    b.HasOne("Woolly.Data.Models.MinecraftServer", null)
                        .WithMany()
                        .HasForeignKey("ServersId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_minecraft_player_minecraft_server_minecraft_servers_servers");
                });
#pragma warning restore 612, 618
        }
    }
}