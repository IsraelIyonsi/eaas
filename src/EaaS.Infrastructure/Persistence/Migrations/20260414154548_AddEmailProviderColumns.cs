using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EaaS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailProviderColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "preferred_email_provider_key",
                table: "tenants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_key",
                table: "emails",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_message_id",
                table: "emails",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "tenants",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "preferred_email_provider_key",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "preferred_email_provider_key",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "provider_key",
                table: "emails");

            migrationBuilder.DropColumn(
                name: "provider_message_id",
                table: "emails");
        }
    }
}
