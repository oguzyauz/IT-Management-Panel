using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItCockpit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GmailSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MailboxAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    LastHistoryId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastSyncStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MessagesSeen = table.Column<int>(type: "int", nullable: false),
                    TicketsCreated = table.Column<int>(type: "int", nullable: false),
                    DuplicatesSkipped = table.Column<int>(type: "int", nullable: false),
                    MailsRejected = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GmailSyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReminderTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubjectTemplate = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BodyTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkCalendar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCalendar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReminderDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TicketIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CcJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReminderDeliveries_ReminderTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ReminderTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalTicketNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TicketType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequesterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApplicationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CategoryPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceRequestId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OriginalSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExternalUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_Users_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkScheduleWeeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HasRuleViolation = table.Column<bool>(type: "bit", nullable: false),
                    RuleViolationNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkScheduleWeeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkScheduleWeeks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnassignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAssignments_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketAssignments_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TicketAssignments_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TicketMailSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GmailMessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GmailThreadId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalSender = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    OriginalRecipients = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ForwardedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    IsForwarded = table.Column<bool>(type: "bit", nullable: false),
                    OriginalSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IngestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMailSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketMailSources_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketNotes_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketNotes_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TicketParseWarnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GmailMessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubjectValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BodyValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketParseWarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketParseWarnings_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketStatusHistory_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketStatusHistory_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkScheduleApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkScheduleWeekId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkScheduleApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkScheduleApprovals_WorkScheduleWeeks_WorkScheduleWeekId",
                        column: x => x.WorkScheduleWeekId,
                        principalTable: "WorkScheduleWeeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkScheduleDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkScheduleWeekId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsManagerOverride = table.Column<bool>(type: "bit", nullable: false),
                    OverriddenByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OverrideNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkScheduleDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkScheduleDays_WorkScheduleWeeks_WorkScheduleWeekId",
                        column: x => x.WorkScheduleWeekId,
                        principalTable: "WorkScheduleWeeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAtUtc",
                table: "AuditLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GmailSyncStates_MailboxAddress",
                table: "GmailSyncStates",
                column: "MailboxAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDeliveries_RecipientUserId_CreatedAtUtc",
                table: "ReminderDeliveries",
                columns: new[] { "RecipientUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDeliveries_SentByUserId",
                table: "ReminderDeliveries",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDeliveries_Status",
                table: "ReminderDeliveries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDeliveries_TemplateId",
                table: "ReminderDeliveries",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderTemplates_Code",
                table: "ReminderTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ManagerUserId",
                table: "Teams",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                table: "Teams",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignments_AssignedByUserId",
                table: "TicketAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignments_AssignedToUserId",
                table: "TicketAssignments",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignments_TicketId_AssignedAtUtc",
                table: "TicketAssignments",
                columns: new[] { "TicketId", "AssignedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMailSources_GmailMessageId",
                table: "TicketMailSources",
                column: "GmailMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketMailSources_GmailThreadId",
                table: "TicketMailSources",
                column: "GmailThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMailSources_Subject_OriginalSentAtUtc",
                table: "TicketMailSources",
                columns: new[] { "Subject", "OriginalSentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMailSources_TicketId",
                table: "TicketMailSources",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketNotes_AuthorUserId",
                table: "TicketNotes",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketNotes_TicketId_CreatedAtUtc",
                table: "TicketNotes",
                columns: new[] { "TicketId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketParseWarnings_IsAcknowledged_Severity",
                table: "TicketParseWarnings",
                columns: new[] { "IsAcknowledged", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketParseWarnings_TicketId",
                table: "TicketParseWarnings",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssigneeUserId_Status",
                table: "Tickets",
                columns: new[] { "AssigneeUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ExternalTicketNumber",
                table: "Tickets",
                column: "ExternalTicketNumber",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SourceRequestId",
                table: "Tickets",
                column: "SourceRequestId",
                filter: "[SourceRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_OriginalSentAtUtc",
                table: "Tickets",
                columns: new[] { "Status", "OriginalSentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_UpdatedAtUtc",
                table: "Tickets",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TicketStatusHistory_ChangedByUserId",
                table: "TicketStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketStatusHistory_TicketId_ChangedAtUtc",
                table: "TicketStatusHistory",
                columns: new[] { "TicketId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TeamId",
                table: "Users",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCalendar_Date",
                table: "WorkCalendar",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkCalendar_Year",
                table: "WorkCalendar",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleApprovals_WorkScheduleWeekId",
                table: "WorkScheduleApprovals",
                column: "WorkScheduleWeekId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleDays_Date_Mode",
                table: "WorkScheduleDays",
                columns: new[] { "Date", "Mode" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleDays_WorkScheduleWeekId_Date",
                table: "WorkScheduleDays",
                columns: new[] { "WorkScheduleWeekId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleWeeks_UserId_WeekStartDate",
                table: "WorkScheduleWeeks",
                columns: new[] { "UserId", "WeekStartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleWeeks_WeekStartDate",
                table: "WorkScheduleWeeks",
                column: "WeekStartDate");

            migrationBuilder.AddForeignKey(
                name: "FK_ReminderDeliveries_Users_RecipientUserId",
                table: "ReminderDeliveries",
                column: "RecipientUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReminderDeliveries_Users_SentByUserId",
                table: "ReminderDeliveries",
                column: "SentByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Users_ManagerUserId",
                table: "Teams",
                column: "ManagerUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Users_ManagerUserId",
                table: "Teams");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "GmailSyncStates");

            migrationBuilder.DropTable(
                name: "ReminderDeliveries");

            migrationBuilder.DropTable(
                name: "TicketAssignments");

            migrationBuilder.DropTable(
                name: "TicketMailSources");

            migrationBuilder.DropTable(
                name: "TicketNotes");

            migrationBuilder.DropTable(
                name: "TicketParseWarnings");

            migrationBuilder.DropTable(
                name: "TicketStatusHistory");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "WorkCalendar");

            migrationBuilder.DropTable(
                name: "WorkScheduleApprovals");

            migrationBuilder.DropTable(
                name: "WorkScheduleDays");

            migrationBuilder.DropTable(
                name: "ReminderTemplates");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "WorkScheduleWeeks");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
