using System;
using EmojiEstimator.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmojiEstimator.Web.Migrations
{
    [DbContext(typeof(EmojiEstimatorDbContext))]
    [Migration("20260327000000_InitialRepositoryScans")]
    public partial class InitialRepositoryScans : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepositoryScans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryOwner = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RepositoryName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedKey = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: true),
                    FailureMessage = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryScans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryScans_ExpiresAtUtc",
                table: "RepositoryScans",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryScans_NormalizedKey",
                table: "RepositoryScans",
                column: "NormalizedKey",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryScans");
        }

        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

            modelBuilder.Entity("EmojiEstimator.Web.Data.RepositoryScan", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("INTEGER");

                b.Property<DateTime?>("CompletedAtUtc")
                    .HasColumnType("TEXT");

                b.Property<DateTime>("CreatedAtUtc")
                    .HasColumnType("TEXT");

                b.Property<DateTime?>("ExpiresAtUtc")
                    .HasColumnType("TEXT");

                b.Property<string>("FailureMessage")
                    .HasMaxLength(2048)
                    .HasColumnType("TEXT");

                b.Property<string>("NormalizedKey")
                    .IsRequired()
                    .HasMaxLength(320)
                    .HasColumnType("TEXT");

                b.Property<string>("RepositoryName")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("TEXT");

                b.Property<string>("RepositoryOwner")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("TEXT");

                b.Property<string>("ResultJson")
                    .HasColumnType("TEXT");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(32)
                    .HasColumnType("TEXT");

                b.Property<DateTime>("UpdatedAtUtc")
                    .HasColumnType("TEXT");

                b.HasKey("Id");

                b.HasIndex("ExpiresAtUtc");

                b.HasIndex("NormalizedKey")
                    .IsUnique();

                b.ToTable("RepositoryScans");
            });
#pragma warning restore 612, 618
        }
    }
}
