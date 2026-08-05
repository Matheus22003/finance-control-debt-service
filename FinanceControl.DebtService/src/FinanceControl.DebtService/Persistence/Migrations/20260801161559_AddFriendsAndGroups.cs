using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceControl.DebtService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendsAndGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "debts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "debt_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debt_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    requester_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    addressee_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    addressee_display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    addressee_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    pair_key = table.Column<string>(type: "character varying(65)", maxLength: 65, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_friendships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "debt_group_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    debt_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debt_group_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_debt_group_members_debt_groups_debt_group_id",
                        column: x => x.debt_group_id,
                        principalTable: "debt_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_debts_group_id",
                table: "debts",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_debt_group_members_user_id",
                table: "debt_group_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_debt_group_members_group_user",
                table: "debt_group_members",
                columns: new[] { "debt_group_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_debt_groups_created_by",
                table: "debt_groups",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_friendships_addressee_status",
                table: "friendships",
                columns: new[] { "addressee_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_friendships_requester_status",
                table: "friendships",
                columns: new[] { "requester_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_friendships_pair_key",
                table: "friendships",
                column: "pair_key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_debts_debt_groups_group_id",
                table: "debts",
                column: "group_id",
                principalTable: "debt_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_debts_debt_groups_group_id",
                table: "debts");

            migrationBuilder.DropTable(
                name: "debt_group_members");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropTable(
                name: "debt_groups");

            migrationBuilder.DropIndex(
                name: "ix_debts_group_id",
                table: "debts");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "debts");
        }
    }
}
