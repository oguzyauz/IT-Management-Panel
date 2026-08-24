import type {
  AgingLevel,
  LeaveStatus,
  LeaveType,
  ParseWarningSeverity,
  ReminderStatus,
  ScheduleStatus,
  TicketStatus,
  TicketType,
  WorkMode,
} from './api/types';

export const ticketStatusLabels: Record<TicketStatus, string> = {
  New: 'Yeni',
  Unassigned: 'Atanmamış',
  Assigned: 'Atandı',
  InProgress: 'Devam ediyor',
  Completed: 'Tamamlandı',
  Archived: 'Arşivlendi',
};

export const ticketTypeLabels: Record<TicketType, string> = {
  Incident: 'Incident',
  ServiceRequest: 'Talep',
};

/**
 * Tixbox'tan SLA verisi gelmediği için hiçbir etiket "gecikme" ya da "SLA" demez.
 * bkz. docs/revised-scope.md §5.
 */
export const agingLabels: Record<AgingLevel, string> = {
  Normal: 'Normal',
  NeedsUpdate: 'Güncelleme bekliyor',
  LongOpen: 'Uzun süredir açık',
  LongOpenCritical: 'Uzun süredir açık (kritik)',
};

export const workModeLabels: Record<WorkMode, string> = {
  Office: 'Ofis',
  HomeOffice: 'Home office',
  Leave: 'İzinli',
};

export const scheduleStatusLabels: Record<ScheduleStatus, string> = {
  Draft: 'Taslak',
  Submitted: 'Gönderildi',
  Approved: 'Onaylandı',
  Rejected: 'Reddedildi',
};

export const reminderStatusLabels: Record<ReminderStatus, string> = {
  Pending: 'Bekliyor',
  Sent: 'Gönderildi',
  Failed: 'Başarısız',
};

export const warningSeverityLabels: Record<ParseWarningSeverity, string> = {
  Info: 'Bilgi',
  Warning: 'Uyarı',
  Error: 'Veri uyumsuzluğu',
};

/** Ticket durumu değiştiren her ekranda gösterilmesi zorunlu uyarı. */
export const TIXBOX_DISCLAIMER =
  'Bu durum yalnızca yönetim panelindeki takip durumudur. Tixbox durumunu değiştirmez.';

export function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—';
  return new Date(value).toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  return new Date(value).toLocaleDateString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });
}

export function formatDayShort(isoDate: string): string {
  const date = new Date(`${isoDate}T00:00:00`);
  return date.toLocaleDateString('tr-TR', { weekday: 'short', day: '2-digit', month: '2-digit' });
}

export function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export const leaveTypeLabels: Record<LeaveType, string> = {
  Annual: 'Yıllık İzin',
  Personal: 'Mazeret İzni',
  Medical: 'Rapor / Hastalık',
  Unpaid: 'Ücretsiz İzin',
};

export const leaveStatusLabels: Record<LeaveStatus, string> = {
  Pending: 'Onay Bekliyor',
  Approved: 'Onaylandı',
  Rejected: 'Reddedildi',
  Cancelled: 'İptal Edildi',
};

/** İzin türünün takvimde gösterileceği renk. */
export const leaveTypeColors: Record<LeaveType, string> = {
  Annual: '#2196f3',    // Mavi
  Medical: '#f44336',   // Kırmızı
  Personal: '#ff9800',  // Turuncu
  Unpaid: '#9e9e9e',    // Gri
};
