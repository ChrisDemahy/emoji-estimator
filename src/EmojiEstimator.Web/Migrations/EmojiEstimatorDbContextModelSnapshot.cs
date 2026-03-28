using System;
using EmojiEstimator.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace EmojiEstimator.Web.Migrations
{
    [DbContext(typeof(EmojiEstimatorDbContext))]
    partial class EmojiEstimatorDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
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
