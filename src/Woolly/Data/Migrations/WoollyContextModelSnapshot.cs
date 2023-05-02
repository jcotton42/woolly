﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Woolly.Data;

#nullable disable

namespace Woolly.Data.Migrations
{
    [DbContext(typeof(WoollyContext))]
    partial class WoollyContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Woolly.Data.Models.Guild", b =>
                {
                    b.Property<ulong>("Id")
                        .HasColumnType("numeric(20,0)")
                        .HasColumnName("id");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.HasKey("Id")
                        .HasName("pk_guilds");

                    b.ToTable("guilds", (string)null);
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
#pragma warning restore 612, 618
        }
    }
}
