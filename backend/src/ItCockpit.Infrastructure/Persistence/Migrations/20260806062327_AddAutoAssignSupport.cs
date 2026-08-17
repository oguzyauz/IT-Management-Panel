using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItCockpit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoAssignSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoAssigned",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssignedByUserId",
                table: "TicketAssignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoAssigned",
                table: "Tickets");

            migrationBuilder.AlterColumn<Guid>(
                name: "AssignedByUserId",
                table: "TicketAssignments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
