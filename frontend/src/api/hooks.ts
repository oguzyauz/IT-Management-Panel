import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';
import type {
  AppSettingDto,
  CreateTicketRequest,
  CurrentUserDto,
  DashboardDto,
  GmailSetupStatus,
  GmailSyncStateDto,
  LoginResponse,
  ManagedUserDto,
  SetupStatusDto,
  IngestionRunResultDto,
  MyWeekDto,
  PagedResult,
  ParseWarningDto,
  ReminderHistoryItemDto,
  ReminderPreviewDto,
  ReminderTemplateDto,
  TicketDetailDto,
  TicketListItemDto,
  TicketNoteDto,
  TicketStatus,
  TodayTeamStatusDto,
  UserDto,
  WeeklyScheduleMatrixDto,
  WorkMode,
} from './types';

export const queryKeys = {
  me: ['me'] as const,
  users: ['users'] as const,
  dashboard: ['dashboard'] as const,
  tickets: (params: unknown) => ['tickets', params] as const,
  ticket: (id: string) => ['ticket', id] as const,
  warnings: ['warnings'] as const,
  myWeek: (weekStart?: string) => ['my-week', weekStart ?? 'default'] as const,
  teamMatrix: (weekStart?: string) => ['team-matrix', weekStart ?? 'current'] as const,
  today: ['schedule-today'] as const,
  reminderHistory: ['reminder-history'] as const,
  reminderTemplates: ['reminder-templates'] as const,
};

// --- Kullanıcı --------------------------------------------------------------------------------

export const useMe = (enabled: boolean) =>
  useQuery({
    queryKey: queryKeys.me,
    queryFn: async () => (await api.get<CurrentUserDto>('/auth/me')).data,
    enabled,
    retry: false,
  });

export const useMockUsers = (enabled = true) =>
  useQuery({
    queryKey: ['mock-users'],
    queryFn: async () => (await api.get<UserDto[]>('/auth/mock-users')).data,
    enabled,
    retry: false,
  });

/** Giriş ekranı: ilk kurulum mu, parola girişi mi, geliştirme seçimi mi. */
export const useSetupStatus = () =>
  useQuery({
    queryKey: ['setup-status'],
    queryFn: async () => (await api.get<SetupStatusDto>('/auth/setup-status')).data,
    retry: false,
    staleTime: 0,
  });

export const useInitialSetup = () =>
  useMutation({
    mutationFn: async (vars: { email: string; password: string }) =>
      (await api.post<UserDto>('/auth/initial-setup', vars)).data,
  });

export const useLogin = () =>
  useMutation({
    mutationFn: async (vars: { email: string; password: string }) =>
      (await api.post<LoginResponse>('/auth/login', vars)).data,
  });

export const useChangePassword = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: { currentPassword: string; newPassword: string }) =>
      (await api.post('/auth/change-password', vars)).data,
    // "Parola değiştirmelisiniz" bayrağı düştü; me yeniden okunmazsa kullanıcı
    // parola ekranına geri yönlendirilmeye devam eder.
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.me }),
  });
};

// --- Kullanıcı yönetimi -----------------------------------------------------------------------

export const useManagedUsers = (enabled = true) =>
  useQuery({
    queryKey: ['managed-users'],
    queryFn: async () => (await api.get<ManagedUserDto[]>('/users/managed')).data,
    enabled,
  });

const useUserAdminMutation = <TVariables,>(fn: (vars: TVariables) => Promise<unknown>) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['managed-users'] });
      void qc.invalidateQueries({ queryKey: queryKeys.users });
    },
  });
};

export const useCreateUser = () =>
  useUserAdminMutation(async (vars: {
    email: string;
    displayName: string;
    title?: string;
    role: string;
    initialPassword: string;
  }) => (await api.post<ManagedUserDto>('/users', vars)).data);

export const useResetUserPassword = () =>
  useUserAdminMutation(async (vars: { userId: string; newPassword: string }) =>
    (await api.post(`/users/${vars.userId}/reset-password`, { newPassword: vars.newPassword })).data);

export const useSetUserActive = () =>
  useUserAdminMutation(async (vars: { userId: string; isActive: boolean }) =>
    (await api.post(`/users/${vars.userId}/active`, null, { params: { value: vars.isActive } })).data);

// --- Gmail kurulumu ---------------------------------------------------------------------------

export const useGmailStatus = (enabled = true) =>
  useQuery({
    queryKey: ['gmail-status'],
    queryFn: async () => (await api.get<GmailSetupStatus>('/ingestion/gmail-status')).data,
    enabled,
  });

export const useGmailSyncState = (enabled = true) =>
  useQuery({
    queryKey: ['gmail-sync-state'],
    queryFn: async () => (await api.get<GmailSyncStateDto[]>('/ingestion/state')).data,
    enabled,
  });

const useMailboxMutation = <TVariables,>(fn: (vars: TVariables) => Promise<unknown>) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['gmail-status'] });
      void qc.invalidateQueries({ queryKey: ['gmail-sync-state'] });
    },
  });
};

export const useAddMailbox = () =>
  useMailboxMutation(async (vars: { mailbox: string }) =>
    (await api.post<string[]>('/ingestion/mailboxes', vars)).data);

/** Okuma penceresini sıfırlar; sonraki okuma kutuyu baştan tarar. */
export const useRescanMailbox = () =>
  useMailboxMutation(async (vars: { mailbox: string }) =>
    (await api.post('/ingestion/mailboxes/rescan', null, { params: { mailbox: vars.mailbox } })).data);

export const useRemoveMailbox = () =>
  useMailboxMutation(async (vars: { mailbox: string }) =>
    (await api.delete<string[]>('/ingestion/mailboxes', { params: { mailbox: vars.mailbox } })).data);

/**
 * Gmail OAuth onayını başlatır. Sunucunun çalıştığı makinede tarayıcı açılır ve
 * kullanıcı onay verene kadar bu istek bekler — bu yüzden zaman aşımı uzun tutulur.
 */
export const useAuthorizeMailbox = () =>
  useMailboxMutation(async (vars: { mailbox: string }) =>
    (await api.post('/ingestion/authorize', null, {
      params: { mailbox: vars.mailbox },
      timeout: 5 * 60 * 1000,
    })).data);

// --- Ayarlar ----------------------------------------------------------------------------------

export const useAppSettings = (enabled = true) =>
  useQuery({
    queryKey: ['app-settings'],
    queryFn: async () => (await api.get<AppSettingDto[]>('/settings')).data,
    enabled,
  });

export const useUpdateAppSettings = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (values: Record<string, string>) =>
      (await api.put<AppSettingDto[]>('/settings', { values })).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['app-settings'] });
      void qc.invalidateQueries({ queryKey: ['tickets'] });
      void qc.invalidateQueries({ queryKey: queryKeys.dashboard });
    },
  });
};

export const useUsers = () =>
  useQuery({
    queryKey: queryKeys.users,
    queryFn: async () => (await api.get<UserDto[]>('/users')).data,
  });

// --- Dashboard --------------------------------------------------------------------------------

/**
 * @param refetchIntervalMs Masaüstü özet kutusu için periyodik yenileme. Tam panelde
 * varsayılan olarak kapalıdır; kullanıcı zaten sayfayı yenileyebiliyor.
 */
export const useDashboard = (refetchIntervalMs?: number, enabled = true) =>
  useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: async () => (await api.get<DashboardDto>('/dashboard')).data,
    // Dashboard yalnızca yöneticiye açıktır. Rol bilinmeden istek atılırsa çalışan 403 görür.
    enabled,
    refetchInterval: refetchIntervalMs ?? false,
    refetchIntervalInBackground: Boolean(refetchIntervalMs),
  });

// --- Ticket -----------------------------------------------------------------------------------

export interface TicketFilters {
  search?: string;
  status?: TicketStatus[];
  assigneeUserId?: string;
  unassigned?: boolean;
  priority?: number;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDescending?: boolean;
}

export const useTickets = (filters: TicketFilters) =>
  useQuery({
    queryKey: queryKeys.tickets(filters),
    queryFn: async () =>
      (
        await api.get<PagedResult<TicketListItemDto>>('/tickets', {
          params: filters,
          paramsSerializer: { indexes: null },
        })
      ).data,
  });

export const useTicket = (id: string | undefined) =>
  useQuery({
    queryKey: queryKeys.ticket(id ?? ''),
    queryFn: async () => (await api.get<TicketDetailDto>(`/tickets/${id}`)).data,
    enabled: Boolean(id),
  });

function useTicketMutation<TVariables>(
  mutationFn: (vars: TVariables) => Promise<unknown>,
  ticketId?: string,
) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['tickets'] });
      void qc.invalidateQueries({ queryKey: queryKeys.dashboard });
      if (ticketId) void qc.invalidateQueries({ queryKey: queryKeys.ticket(ticketId) });
    },
  });
}

export const useCreateTicket = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: CreateTicketRequest) =>
      (await api.post<TicketDetailDto>('/tickets', vars)).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['tickets'] });
      void qc.invalidateQueries({ queryKey: queryKeys.dashboard });
    },
  });
};

export const useAssignTicket = (ticketId: string) =>
  useTicketMutation(
    async (vars: { assigneeUserId: string; note?: string }) =>
      (await api.post<TicketDetailDto>(`/tickets/${ticketId}/assign`, vars)).data,
    ticketId,
  );

export const useChangeTicketStatus = (ticketId: string) =>
  useTicketMutation(
    async (vars: { status: TicketStatus; note?: string }) =>
      (await api.post<TicketDetailDto>(`/tickets/${ticketId}/status`, vars)).data,
    ticketId,
  );

export const useAddTicketNote = (ticketId: string) =>
  useTicketMutation(
    async (vars: { body: string }) =>
      (await api.post<TicketNoteDto>(`/tickets/${ticketId}/notes`, vars)).data,
    ticketId,
  );

export const useParseWarnings = () =>
  useQuery({
    queryKey: queryKeys.warnings,
    queryFn: async () => (await api.get<ParseWarningDto[]>('/tickets/warnings')).data,
  });

// --- Ingestion --------------------------------------------------------------------------------

export const useRunIngestion = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => (await api.post<IngestionRunResultDto>('/ingestion/run')).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['tickets'] });
      void qc.invalidateQueries({ queryKey: queryKeys.dashboard });
      void qc.invalidateQueries({ queryKey: queryKeys.warnings });
    },
  });
};

// --- Çalışma takvimi --------------------------------------------------------------------------

export const useMyWeek = (weekStart?: string) =>
  useQuery({
    queryKey: queryKeys.myWeek(weekStart),
    queryFn: async () =>
      (await api.get<MyWeekDto>('/schedule/my-week', { params: weekStart ? { weekStart } : {} })).data,
  });

export const useSaveMyWeek = (weekStart?: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: {
      weekStartDate: string;
      days: { date: string; mode: WorkMode }[];
      submit: boolean;
    }) => {
      const payload = { weekStartDate: vars.weekStartDate, days: vars.days };
      const url = vars.submit ? '/schedule/my-week/submit' : '/schedule/my-week';
      const response = vars.submit
        ? await api.post<MyWeekDto>(url, payload)
        : await api.put<MyWeekDto>(url, payload);
      return response.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: queryKeys.myWeek(weekStart) });
      void qc.invalidateQueries({ queryKey: ['team-matrix'] });
      void qc.invalidateQueries({ queryKey: queryKeys.dashboard });
    },
  });
};

export const useTeamMatrix = (weekStart?: string) =>
  useQuery({
    queryKey: queryKeys.teamMatrix(weekStart),
    queryFn: async () =>
      (
        await api.get<WeeklyScheduleMatrixDto>('/schedule/team', {
          params: weekStart ? { weekStart } : {},
        })
      ).data,
  });

export const useTodayStatus = () =>
  useQuery({
    queryKey: queryKeys.today,
    queryFn: async () => (await api.get<TodayTeamStatusDto>('/schedule/today')).data,
  });

export const useScheduleDecision = (weekStart?: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: { weekId: string; decision: 'Approved' | 'Rejected'; comment?: string }) =>
      (
        await api.post<MyWeekDto>(`/schedule/${vars.weekId}/decision`, {
          decision: vars.decision,
          comment: vars.comment,
        })
      ).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: queryKeys.teamMatrix(weekStart) });
      void qc.invalidateQueries({ queryKey: ['team-matrix'] });
    },
  });
};

export const useScheduleOverride = (weekStart?: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: { weekId: string; date: string; mode: WorkMode; note?: string }) =>
      (
        await api.post<MyWeekDto>(`/schedule/${vars.weekId}/override`, {
          date: vars.date,
          mode: vars.mode,
          note: vars.note,
        })
      ).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: queryKeys.teamMatrix(weekStart) });
      void qc.invalidateQueries({ queryKey: ['team-matrix'] });
      void qc.invalidateQueries({ queryKey: queryKeys.dashboard });
    },
  });
};

// --- Hatırlatma -------------------------------------------------------------------------------

export const useReminderTemplates = () =>
  useQuery({
    queryKey: queryKeys.reminderTemplates,
    queryFn: async () => (await api.get<ReminderTemplateDto[]>('/reminders/templates')).data,
  });

export const useReminderPreview = () =>
  useMutation({
    mutationFn: async (vars: {
      recipientUserId: string;
      ticketIds: string[];
      templateId?: string | null;
      cc?: string[];
    }) => (await api.post<ReminderPreviewDto>('/reminders/preview', vars)).data,
  });

export const useSendReminder = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vars: {
      recipientUserId: string;
      ticketIds: string[];
      templateId?: string | null;
      subject: string;
      body: string;
      cc?: string[];
      confirmed: boolean;
    }) => (await api.post<ReminderHistoryItemDto>('/reminders/send', vars)).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: queryKeys.reminderHistory });
      void qc.invalidateQueries({ queryKey: queryKeys.dashboard });
    },
  });
};

export const useReminderHistory = () =>
  useQuery({
    queryKey: queryKeys.reminderHistory,
    queryFn: async () => (await api.get<ReminderHistoryItemDto[]>('/reminders/history')).data,
  });
