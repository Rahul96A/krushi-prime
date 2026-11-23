using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KrushiPrime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnlinePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CashfreeAppId",
                table: "Tenants",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CashfreeSecretEnc",
                table: "Tenants",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NttMerchId",
                table: "Tenants",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NttPasswordEnc",
                table: "Tenants",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NttProductId",
                table: "Tenants",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NttRequestKeyEnc",
                table: "Tenants",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NttResponseKeyEnc",
                table: "Tenants",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaymentLiveMode",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PaymentProvider",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PaymentLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    LiveMode = table.Column<bool>(type: "bit", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    SalesInvoiceId = table.Column<int>(type: "int", nullable: true),
                    Plan = table.Column<int>(type: "int", nullable: true),
                    PlanMonths = table.Column<int>(type: "int", nullable: true),
                    PayerName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PayerMobile = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProviderRef = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProviderUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProviderToken = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ProviderTxnId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecordedPaymentId = table.Column<int>(type: "int", nullable: true),
                    LastMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLinks_ProviderRef",
                table: "PaymentLinks",
                column: "ProviderRef");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLinks_PublicId",
                table: "PaymentLinks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLinks_TenantId_Status",
                table: "PaymentLinks",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentLinks");

            migrationBuilder.DropColumn(
                name: "CashfreeAppId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CashfreeSecretEnc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "NttMerchId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "NttPasswordEnc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "NttProductId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "NttRequestKeyEnc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "NttResponseKeyEnc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PaymentLiveMode",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PaymentProvider",
                table: "Tenants");
        }
    }
}
