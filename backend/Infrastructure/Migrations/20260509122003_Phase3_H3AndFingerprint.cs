using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Synapse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_H3AndFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceFingerprint",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastKnownBssid",
                table: "Users",
                type: "character varying(17)",
                maxLength: 17,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastKnownIp",
                table: "Users",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastKnownH3",
                table: "UserProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastKnownH3CapturedAt",
                table: "UserProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "H3Index",
                table: "Businesses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_H3Index",
                table: "Businesses",
                column: "H3Index");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Businesses_H3Index",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "DeviceFingerprint",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastKnownBssid",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastKnownIp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastKnownH3",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LastKnownH3CapturedAt",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "H3Index",
                table: "Businesses");
        }
    }
}
