using Marketplace.SaaS.Accelerator.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.SaaS.Accelerator.DataAccess.Migrations
{
    /// <summary>
    /// Adds reporting columns (ExternalRequestId, Dimension, Quantity, MarketplaceUsageEventId)
    /// to MeteredAuditLogs, grows RequestJson/ResponseJson to varchar(max), and creates indexes
    /// for caller correlation, date-range queries, and channel/status filtering. New columns are
    /// nullable; existing rows are not backfilled.
    /// </summary>
    [DbContext(typeof(SaasKitContext))]
    [Migration("20260526120000_MeteredAuditLogs_Reporting")]
    public partial class MeteredAuditLogs_Reporting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RequestJson",
                table: "MeteredAuditLogs",
                type: "varchar(max)",
                unicode: false,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldUnicode: false,
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ResponseJson",
                table: "MeteredAuditLogs",
                type: "varchar(max)",
                unicode: false,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldUnicode: false,
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalRequestId",
                table: "MeteredAuditLogs",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dimension",
                table: "MeteredAuditLogs",
                type: "varchar(150)",
                unicode: false,
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Quantity",
                table: "MeteredAuditLogs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<System.Guid>(
                name: "MarketplaceUsageEventId",
                table: "MeteredAuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeteredAuditLogs_ExternalRequestId",
                table: "MeteredAuditLogs",
                column: "ExternalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MeteredAuditLogs_CreatedDate",
                table: "MeteredAuditLogs",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_MeteredAuditLogs_RunBy_CreatedDate",
                table: "MeteredAuditLogs",
                columns: new[] { "RunBy", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MeteredAuditLogs_StatusCode_CreatedDate",
                table: "MeteredAuditLogs",
                columns: new[] { "StatusCode", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MeteredAuditLogs_MarketplaceUsageEventId",
                table: "MeteredAuditLogs",
                column: "MarketplaceUsageEventId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeteredAuditLogs_MarketplaceUsageEventId",
                table: "MeteredAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_MeteredAuditLogs_StatusCode_CreatedDate",
                table: "MeteredAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_MeteredAuditLogs_RunBy_CreatedDate",
                table: "MeteredAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_MeteredAuditLogs_CreatedDate",
                table: "MeteredAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_MeteredAuditLogs_ExternalRequestId",
                table: "MeteredAuditLogs");

            migrationBuilder.DropColumn(
                name: "MarketplaceUsageEventId",
                table: "MeteredAuditLogs");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "MeteredAuditLogs");

            migrationBuilder.DropColumn(
                name: "Dimension",
                table: "MeteredAuditLogs");

            migrationBuilder.DropColumn(
                name: "ExternalRequestId",
                table: "MeteredAuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "ResponseJson",
                table: "MeteredAuditLogs",
                type: "varchar(500)",
                unicode: false,
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(max)",
                oldUnicode: false,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestJson",
                table: "MeteredAuditLogs",
                type: "varchar(500)",
                unicode: false,
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(max)",
                oldUnicode: false,
                oldNullable: true);
        }
    }
}
