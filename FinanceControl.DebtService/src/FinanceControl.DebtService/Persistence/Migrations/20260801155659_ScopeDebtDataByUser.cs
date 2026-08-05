using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceControl.DebtService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeDebtDataByUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_people_email",
                table: "people");

            migrationBuilder.DropIndex(
                name: "ux_people_current_user",
                table: "people");

            migrationBuilder.AddColumn<Guid>(
                name: "linked_user_id",
                table: "people",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "people",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "debts",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE people
                SET owner_user_id = '7f805b46-0b56-4a5d-86eb-d4f53c92db93',
                    linked_user_id = CASE
                        WHEN is_current_user THEN '7f805b46-0b56-4a5d-86eb-d4f53c92db93'::uuid
                        ELSE NULL
                    END;

                UPDATE debts
                SET created_by_user_id = '7f805b46-0b56-4a5d-86eb-d4f53c92db93';
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "owner_user_id",
                table: "people",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "created_by_user_id",
                table: "debts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_people_linked_user_id",
                table: "people",
                column: "linked_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_people_owner_email",
                table: "people",
                columns: new[] { "owner_user_id", "email" });

            migrationBuilder.CreateIndex(
                name: "ux_people_current_user",
                table: "people",
                columns: new[] { "owner_user_id", "is_current_user" },
                unique: true,
                filter: "is_current_user = TRUE");

            migrationBuilder.CreateIndex(
                name: "ix_debts_created_by_user_id",
                table: "debts",
                column: "created_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_people_linked_user_id",
                table: "people");

            migrationBuilder.DropIndex(
                name: "ix_people_owner_email",
                table: "people");

            migrationBuilder.DropIndex(
                name: "ux_people_current_user",
                table: "people");

            migrationBuilder.DropIndex(
                name: "ix_debts_created_by_user_id",
                table: "debts");

            migrationBuilder.DropColumn(
                name: "linked_user_id",
                table: "people");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "people");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "debts");

            migrationBuilder.CreateIndex(
                name: "ix_people_email",
                table: "people",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ux_people_current_user",
                table: "people",
                column: "is_current_user",
                unique: true,
                filter: "is_current_user = TRUE");
        }
    }
}
