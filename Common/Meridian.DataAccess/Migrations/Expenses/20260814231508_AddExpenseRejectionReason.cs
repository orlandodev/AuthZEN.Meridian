using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meridian.DataAccess.Migrations.Expenses
{
    /// <inheritdoc />
    public partial class AddExpenseRejectionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Expenses",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Expenses",
                keyColumn: "Id",
                keyValue: new Guid("e0000000-0000-0000-0000-000000000001"),
                column: "RejectionReason",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expenses",
                keyColumn: "Id",
                keyValue: new Guid("e0000000-0000-0000-0000-000000000002"),
                column: "RejectionReason",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expenses",
                keyColumn: "Id",
                keyValue: new Guid("e0000000-0000-0000-0000-000000000003"),
                column: "RejectionReason",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Expenses");
        }
    }
}
