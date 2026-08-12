using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceControl.DebtService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSimplifiedSettlementPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "settlement_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settlement_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    settlement_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    debt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    debt_share_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_allocations", x => x.id);
                    table.CheckConstraint("ck_settlement_allocations_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "FK_settlement_allocations_settlement_plans_settlement_plan_id",
                        column: x => x.settlement_plan_id,
                        principalTable: "settlement_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "settlement_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    settlement_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_person_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    to_identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_person_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_transfers", x => x.id);
                    table.CheckConstraint("ck_settlement_transfers_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "FK_settlement_transfers_settlement_plans_settlement_plan_id",
                        column: x => x.settlement_plan_id,
                        principalTable: "settlement_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_settlement_allocations_debt_id",
                table: "settlement_allocations",
                column: "debt_id");

            migrationBuilder.CreateIndex(
                name: "ix_settlement_allocations_debt_share_id",
                table: "settlement_allocations",
                column: "debt_share_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_allocations_settlement_plan_id",
                table: "settlement_allocations",
                column: "settlement_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_settlement_plans_created_by_user_id",
                table: "settlement_plans",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_settlement_plans_group_status",
                table: "settlement_plans",
                columns: new[] { "group_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_settlement_transfers_from_user_id",
                table: "settlement_transfers",
                column: "from_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_transfers_settlement_plan_id",
                table: "settlement_transfers",
                column: "settlement_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_settlement_transfers_to_user_status",
                table: "settlement_transfers",
                columns: new[] { "to_user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "settlement_allocations");

            migrationBuilder.DropTable(
                name: "settlement_transfers");

            migrationBuilder.DropTable(
                name: "settlement_plans");
        }
    }
}
