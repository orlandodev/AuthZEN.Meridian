using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Meridian.DataAccess.Migrations.Reporting
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepartmentSpendSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false),
                    Period = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentSpendSummaries", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "DepartmentSpendSummaries",
                columns: new[] { "Id", "Currency", "Department", "Period", "TotalAmount" },
                values: new object[,]
                {
                    { new Guid("c0000000-0000-0000-0000-000000000001"), "USD", "Sales", "2025-01", 8042.50m },
                    { new Guid("c0000000-0000-0000-0000-000000000002"), "USD", "Finance", "2025-01", 0m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentSpendSummaries");
        }
    }
}
