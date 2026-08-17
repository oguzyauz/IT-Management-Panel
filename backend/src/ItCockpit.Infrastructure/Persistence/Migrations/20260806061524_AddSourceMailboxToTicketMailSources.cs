using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItCockpit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceMailboxToTicketMailSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceMailbox",
                table: "TicketMailSources",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMailSources_SourceMailbox",
                table: "TicketMailSources",
                column: "SourceMailbox");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketMailSources_SourceMailbox",
                table: "TicketMailSources");

            migrationBuilder.DropColumn(
                name: "SourceMailbox",
                table: "TicketMailSources");
        }
    }
}
