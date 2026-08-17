export type TicketStatus =
  | 'New'
  | 'Unassigned'
  | 'Assigned'
  | 'InProgress'
  | 'Completed'
  | 'Archived';

export type TicketType = 'Incident' | 'ServiceRequest';

export type AgingLevel = 'Normal' | 'NeedsUpdate' | 'LongOpen' | 'LongOpenCritical';

export type WorkMode = 'Office' | 'HomeOffice' | 'Leave';

export type ScheduleStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected';

export type ScheduleDecision = 'Approved' | 'Rejected';

export type ReminderStatus = 'Pending' | 'Sent' | 'Failed';

export type ParseWarningSeverity = 'Info' | 'Warning' | 'Error';

/** Giriş ekranının hangi modda açılacağı. */
export interface SetupStatusDto {
  needsInitialSetup: boolean;
  adminEmail?: string | null;
  authProvider: string;
}

export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  user: UserDto;
  mustChangePassword: boolean;
}

/**
 * Oturum açmış kullanıcının kendisi. UserDto'dan ayrıdır: hesap durumu yalnızca
 * kişinin kendisine gösterilir, atama listelerinde herkese değil.
 */
export interface CurrentUserDto extends UserDto {
  mustChangePassword: boolean;
}

/** Yönetim ekranı satırı. Parola özeti hiçbir zaman gelmez. */
export interface ManagedUserDto {
  id: string;
  email: string;
  displayName: string;
  title?: string | null;
  roles: string[];
  isActive: boolean;
  hasPassword: boolean;
  mustChangePassword: boolean;
  isLockedOut: boolean;
  lastLoginAtUtc?: string | null;
}

export interface MailboxAuthStatus {
  mailboxAddress: string;
  authorized: boolean;
}

export interface GmailSetupStatus {
  provider: string;
  credentialsPath: string;
  credentialsFound: boolean;
  credentialsValid: boolean;
  problem?: string | null;
  clientType?: string | null;
  clientIdMasked?: string | null;
  tokenStorePath: string;
  mailboxes: MailboxAuthStatus[];
  alreadyAuthorized: boolean;
  readyToAuthorize: boolean;
  nextStep: string;
}

export interface GmailSyncStateDto {
  mailboxAddress: string;
  lastHistoryId?: string | null;
  lastSyncCompletedAtUtc?: string | null;
  lastSyncStatus?: string | null;
  lastError?: string | null;
  messagesSeen: number;
  ticketsCreated: number;
  duplicatesSkipped: number;
  mailsRejected: number;
}

export interface AppSettingDto {
  key: string;
  value: string;
  dataType: string;
  category: string;
  description?: string | null;
}

export interface UserDto {
  id: string;
  email: string;
  displayName: string;
  title?: string | null;
  teamId?: string | null;
  teamName?: string | null;
  roles: string[];
}

export interface TicketListItemDto {
  id: string;
  externalTicketNumber: string;
  ticketType: TicketType;
  requesterName: string;
  applicationName: string;
  priority: number;
  status: TicketStatus;
  assigneeUserId?: string | null;
  assigneeName?: string | null;
  originalSentAtUtc: string;
  updatedAtUtc: string;
  daysOpen: number;
  daysSinceUpdate: number;
  aging: AgingLevel;
  hasParseWarning: boolean;
  /** Kişiye özel mail olduğu için sistem tarafından atandı; müdür ataması değil. */
  autoAssigned: boolean;
  /** Mailden değil, panelden elle girildi. */
  createdManually: boolean;
  /** Ticket'ın maili hangi posta kutusunda okundu. Elle eklenenlerde boştur. */
  sourceMailboxes: string[];
  externalUrl?: string | null;
}

export interface CreateTicketRequest {
  externalTicketNumber: string;
  requesterName: string;
  applicationName: string;
  priority: number;
  originalSentAtUtc: string;
  description?: string;
  categoryPath?: string;
  externalReference?: string;
  externalUrl?: string;
  assigneeUserId?: string | null;
}

export interface TicketNoteDto {
  id: string;
  authorUserId: string;
  authorName: string;
  body: string;
  createdAtUtc: string;
}

export interface TicketStatusHistoryDto {
  id: string;
  fromStatus?: TicketStatus | null;
  toStatus: TicketStatus;
  changedByUserId?: string | null;
  changedByName: string;
  changedAtUtc: string;
  note?: string | null;
}

export interface TicketAssignmentDto {
  id: string;
  assignedToUserId: string;
  assignedToName: string;
  assignedByUserId: string;
  assignedByName: string;
  assignedAtUtc: string;
  unassignedAtUtc?: string | null;
  note?: string | null;
}

export interface TicketMailSourceDto {
  id: string;
  sourceMailbox: string;
  gmailMessageId: string;
  subject: string;
  originalSender: string;
  originalRecipients: string[];
  forwardedBy?: string | null;
  isForwarded: boolean;
  originalSentAtUtc: string;
  receivedAtUtc: string;
  ingestedAtUtc: string;
}

export interface ParseWarningDto {
  id: string;
  ticketId?: string | null;
  ticketNumber?: string | null;
  gmailMessageId: string;
  code: string;
  severity: ParseWarningSeverity;
  message: string;
  fieldName?: string | null;
  subjectValue?: string | null;
  bodyValue?: string | null;
  isAcknowledged: boolean;
  createdAtUtc: string;
}

export interface TicketDetailDto {
  id: string;
  externalTicketNumber: string;
  ticketType: TicketType;
  requesterName: string;
  applicationName: string;
  description: string;
  priority: number;
  categoryPath?: string | null;
  externalReference?: string | null;
  sourceRequestId?: string | null;
  originalSentAtUtc: string;
  externalUrl?: string | null;
  status: TicketStatus;
  allowedNextStatuses: TicketStatus[];
  assigneeUserId?: string | null;
  assigneeName?: string | null;
  autoAssigned: boolean;
  createdManually: boolean;
  assignedAtUtc?: string | null;
  completedAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  daysOpen: number;
  daysSinceUpdate: number;
  aging: AgingLevel;
  notes: TicketNoteDto[];
  statusHistory: TicketStatusHistoryDto[];
  assignments: TicketAssignmentDto[];
  mailSources: TicketMailSourceDto[];
  parseWarnings: ParseWarningDto[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface DashboardMetricsDto {
  totalOpenTickets: number;
  unassignedTickets: number;
  inProgressTickets: number;
  staleTickets: number;
  inOfficeToday: number;
  homeOfficeToday: number;
  onLeaveToday: number;
  missingScheduleSubmissions: number;
}

export interface AgingThresholdsDto {
  staleAfterDays: number;
  oldAfterDays: number;
  criticalAfterDays: number;
}

export interface TeamMemberDayStatusDto {
  userId: string;
  displayName: string;
  title?: string | null;
  mode?: WorkMode | null;
  hasSubmittedWeek: boolean;
}

export interface TodayTeamStatusDto {
  date: string;
  isHoliday: boolean;
  holidayName?: string | null;
  members: TeamMemberDayStatusDto[];
}

export interface EmployeeWorkloadDto {
  userId: string;
  displayName: string;
  title?: string | null;
  openTicketCount: number;
  inProgressCount: number;
  staleCount: number;
  todayMode?: WorkMode | null;
}

export interface WeeklyScheduleCellDto {
  date: string;
  mode?: WorkMode | null;
  isManagerOverride: boolean;
  isHoliday: boolean;
  holidayName?: string | null;
}

export interface WeeklyScheduleRowDto {
  userId: string;
  displayName: string;
  weekId?: string | null;
  status: ScheduleStatus;
  hasRuleViolation: boolean;
  ruleViolationNote?: string | null;
  cells: WeeklyScheduleCellDto[];
}

export interface WeeklyScheduleMatrixDto {
  weekStartDate: string;
  days: string[];
  rows: WeeklyScheduleRowDto[];
}

export interface ReminderHistoryItemDto {
  id: string;
  recipientUserId: string;
  recipientName: string;
  sentByUserId: string;
  sentByName: string;
  subject: string;
  ticketCount: number;
  ticketNumbers: string[];
  status: ReminderStatus;
  errorMessage?: string | null;
  createdAtUtc: string;
  sentAtUtc?: string | null;
}

export interface TeamStatusUpdateDto {
  ticketId: string;
  externalTicketNumber: string;
  applicationName: string;
  changedByUserId: string;
  changedByName: string;
  fromStatus?: TicketStatus | null;
  toStatus: TicketStatus;
  changedAtUtc: string;
  note?: string | null;
}

export interface DashboardDto {
  metrics: DashboardMetricsDto;
  todayTeamStatus: TodayTeamStatusDto;
  unassignedTickets: TicketListItemDto[];
  attentionTickets: TicketListItemDto[];
  workload: EmployeeWorkloadDto[];
  weeklyMatrix: WeeklyScheduleMatrixDto;
  recentReminders: ReminderHistoryItemDto[];
  dataMismatchWarnings: ParseWarningDto[];
  recentTeamUpdates: TeamStatusUpdateDto[];
  agingThresholds: AgingThresholdsDto;
}

export interface ScheduleRulesDto {
  requiredOfficeDays: number;
  requiredHomeOfficeDays: number;
}

export interface MyWeekDayDto {
  date: string;
  mode?: WorkMode | null;
  isHoliday: boolean;
  holidayName?: string | null;
  isManagerOverride: boolean;
  overrideNote?: string | null;
}

export interface ScheduleDecisionDto {
  id: string;
  decision: ScheduleDecision;
  decidedByUserId: string;
  decidedByName: string;
  decidedAtUtc: string;
  comment?: string | null;
}

export interface MyWeekDto {
  weekId?: string | null;
  weekStartDate: string;
  status: ScheduleStatus;
  isLocked: boolean;
  lockDeadlineUtc?: string | null;
  hasRuleViolation: boolean;
  ruleViolationNote?: string | null;
  rules: ScheduleRulesDto;
  days: MyWeekDayDto[];
  decisions: ScheduleDecisionDto[];
}

export interface ReminderTemplateDto {
  id: string;
  code: string;
  name: string;
  subjectTemplate: string;
  bodyTemplate: string;
  isDefault: boolean;
}

export interface ReminderPreviewDto {
  recipientUserId: string;
  recipientName: string;
  recipientEmail: string;
  subject: string;
  body: string;
  cc: string[];
  tickets: TicketListItemDto[];
  lastReminderSentAtUtc?: string | null;
  templateId?: string | null;
  providerName: string;
}

export interface IngestionRunResultDto {
  provider: string;
  messagesSeen: number;
  ticketsCreated: number;
  duplicatesSkipped: number;
  mailsRejected: number;
  warningsRaised: number;
  createdTicketNumbers: string[];
  rejectReasons: string[];
  startedAtUtc: string;
  completedAtUtc: string;
}
