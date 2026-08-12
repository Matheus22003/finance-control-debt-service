using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceControl.DebtService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "confirmation_required_from_user_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "confirmed_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recorded_by_user_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rejected_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE payments
                SET recorded_by_user_id = '7f805b46-0b56-4a5d-86eb-d4f53c92db93',
                    status = 'CONFIRMED',
                    confirmed_at = created_at;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "recorded_by_user_id",
                table: "payments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_confirmation_user_status",
                table: "payments",
                columns: new[] { "confirmation_required_from_user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payments_confirmation_user_status",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "confirmation_required_from_user_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "confirmed_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "recorded_by_user_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "status",
                table: "payments");
        }
    }
}
