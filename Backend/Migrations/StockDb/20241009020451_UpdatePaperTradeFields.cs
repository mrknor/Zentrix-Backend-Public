using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations.StockDb
{
    /// <inheritdoc />
    public partial class UpdatePaperTradeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "PaperTrades",
                newName: "OpenTime");

            migrationBuilder.AddColumn<decimal>(
                name: "ClosePrice",
                table: "PaperTrades",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CloseTime",
                table: "PaperTrades",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OpenPrice",
                table: "PaperTrades",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitLoss",
                table: "PaperTrades",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PaperTrades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosePrice",
                table: "PaperTrades");

            migrationBuilder.DropColumn(
                name: "CloseTime",
                table: "PaperTrades");

            migrationBuilder.DropColumn(
                name: "OpenPrice",
                table: "PaperTrades");

            migrationBuilder.DropColumn(
                name: "ProfitLoss",
                table: "PaperTrades");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PaperTrades");

            migrationBuilder.RenameColumn(
                name: "OpenTime",
                table: "PaperTrades",
                newName: "CreatedAt");
        }
    }
}
