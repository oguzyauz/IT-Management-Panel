using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItCockpit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualTicketCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreatedManually",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedManually",
                table: "Tickets");
        }
    }
}
