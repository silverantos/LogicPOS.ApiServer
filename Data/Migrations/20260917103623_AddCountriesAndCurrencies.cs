using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogicPOS.ApiServer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCountriesAndCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiCountries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<uint>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Designation = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Code2 = table.Column<string>(type: "TEXT", nullable: true),
                    Code3 = table.Column<string>(type: "TEXT", nullable: true),
                    Capital = table.Column<string>(type: "TEXT", nullable: true),
                    TLD = table.Column<string>(type: "TEXT", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", nullable: true),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: true),
                    FiscalNumberRegex = table.Column<string>(type: "TEXT", nullable: true),
                    ZipCodeRegex = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCountries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiCurrencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<uint>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Designation = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Acronym = table.Column<string>(type: "TEXT", nullable: true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Entity = table.Column<string>(type: "TEXT", nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCurrencies", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiCountries");

            migrationBuilder.DropTable(
                name: "ApiCurrencies");
        }
    }
}
