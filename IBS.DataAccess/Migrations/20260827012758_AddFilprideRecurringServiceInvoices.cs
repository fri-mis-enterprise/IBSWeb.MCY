using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IBS.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddFilprideRecurringServiceInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "recurring_service_invoice_id",
                table: "filpride_service_invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "filpride_recurring_service_invoices",
                columns: table => new
                {
                    recurring_service_invoice_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    company = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    customer_id = table.Column<int>(type: "integer", nullable: false),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    start_period = table.Column<DateOnly>(type: "date", nullable: false),
                    end_period = table.Column<DateOnly>(type: "date", nullable: false),
                    next_run_period = table.Column<DateOnly>(type: "date", nullable: true),
                    duration_in_months = table.Column<int>(type: "integer", nullable: false),
                    generated_count = table.Column<int>(type: "integer", nullable: false),
                    amount_per_month = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "varchar(100)", nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    edited_by = table.Column<string>(type: "varchar(50)", nullable: true),
                    edited_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    cancellation_remarks = table.Column<string>(type: "varchar(255)", nullable: true),
                    canceled_by = table.Column<string>(type: "varchar(50)", nullable: true),
                    canceled_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    voided_by = table.Column<string>(type: "varchar(50)", nullable: true),
                    voided_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    posted_by = table.Column<string>(type: "varchar(50)", nullable: true),
                    posted_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_filpride_recurring_service_invoices", x => x.recurring_service_invoice_id);
                    table.ForeignKey(
                        name: "fk_filpride_recurring_service_invoices_filpride_customers_cust",
                        column: x => x.customer_id,
                        principalTable: "filpride_customers",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_filpride_recurring_service_invoices_filpride_services_servi",
                        column: x => x.service_id,
                        principalTable: "filpride_services",
                        principalColumn: "service_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_filpride_service_invoices_recurring_service_invoice_id_peri",
                table: "filpride_service_invoices",
                columns: new[] { "recurring_service_invoice_id", "period" });

            migrationBuilder.CreateIndex(
                name: "ix_filpride_recurring_service_invoices_customer_id",
                table: "filpride_recurring_service_invoices",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_filpride_recurring_service_invoices_is_active_next_run_peri",
                table: "filpride_recurring_service_invoices",
                columns: new[] { "is_active", "next_run_period" });

            migrationBuilder.CreateIndex(
                name: "ix_filpride_recurring_service_invoices_service_id",
                table: "filpride_recurring_service_invoices",
                column: "service_id");

            migrationBuilder.AddForeignKey(
                name: "fk_filpride_service_invoices_filpride_recurring_service_invoic",
                table: "filpride_service_invoices",
                column: "recurring_service_invoice_id",
                principalTable: "filpride_recurring_service_invoices",
                principalColumn: "recurring_service_invoice_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_filpride_service_invoices_filpride_recurring_service_invoic",
                table: "filpride_service_invoices");

            migrationBuilder.DropTable(
                name: "filpride_recurring_service_invoices");

            migrationBuilder.DropIndex(
                name: "ix_filpride_service_invoices_recurring_service_invoice_id_peri",
                table: "filpride_service_invoices");

            migrationBuilder.DropColumn(
                name: "recurring_service_invoice_id",
                table: "filpride_service_invoices");
        }
    }
}
