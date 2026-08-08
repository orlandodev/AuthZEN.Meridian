using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Meridian.DataAccess.Migrations.Policy
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AmountLimits",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmountLimits", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ManagerOfs",
                columns: table => new
                {
                    ManagerUserId = table.Column<string>(type: "text", nullable: false),
                    ReportUserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerOfs", x => new { x.ManagerUserId, x.ReportUserId });
                });

            migrationBuilder.CreateTable(
                name: "RoleAssignments",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignments", x => x.UserId);
                });

            migrationBuilder.InsertData(
                table: "AmountLimits",
                columns: new[] { "Key", "Value" },
                values: new object[] { "expense.approve.manager_limit", 5000m });

            migrationBuilder.InsertData(
                table: "ManagerOfs",
                columns: new[] { "ManagerUserId", "ReportUserId" },
                values: new object[,]
                {
                    { "u-nadia", "u-emma" },
                    { "u-nadia", "u-mateo" }
                });

            migrationBuilder.InsertData(
                table: "RoleAssignments",
                columns: new[] { "UserId", "Department", "Role" },
                values: new object[,]
                {
                    { "u-emma", "Sales", "employee" },
                    { "u-finn", "Finance", "finance" },
                    { "u-mateo", "Sales", "employee" },
                    { "u-nadia", "Sales", "manager" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmountLimits");

            migrationBuilder.DropTable(
                name: "ManagerOfs");

            migrationBuilder.DropTable(
                name: "RoleAssignments");
        }
    }
}
