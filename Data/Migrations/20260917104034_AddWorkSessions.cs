using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogicPOS.ApiServer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiWorkSessionPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Designation = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TerminalId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiWorkSessionPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiWorkSessionMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkSessionPeriodId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiWorkSessionMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiWorkSessionMovements_ApiWorkSessionPeriods_WorkSessionPeriodId",
                        column: x => x.WorkSessionPeriodId,
                        principalTable: "ApiWorkSessionPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiWorkSessionMovements_WorkSessionPeriodId",
                table: "ApiWorkSessionMovements",
                column: "WorkSessionPeriodId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiWorkSessionMovements");

            migrationBuilder.DropTable(
                name: "ApiWorkSessionPeriods");
        }
    }
}
