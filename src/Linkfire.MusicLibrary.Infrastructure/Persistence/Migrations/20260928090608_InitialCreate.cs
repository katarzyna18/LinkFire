using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Linkfire.MusicLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Libraries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Libraries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Libraries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedAlbums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LibraryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProviderAlbumId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ArtistName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AlbumName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CoverUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AlbumUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedAlbums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedAlbums_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Libraries_UserId",
                table: "Libraries",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedAlbums_LibraryId_Provider_ProviderAlbumId",
                table: "SavedAlbums",
                columns: new[] { "LibraryId", "Provider", "ProviderAlbumId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedAlbums");

            migrationBuilder.DropTable(
                name: "Libraries");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
