import { useCallback, useMemo, useState } from 'react';
import {
  Autocomplete,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import FilterListIcon from '@mui/icons-material/FilterList';
import ClearIcon from '@mui/icons-material/Clear';
import { useReminderHistory, useUsers } from '../api/hooks';
import type { ReminderHistoryFilters, ReminderStatus, UserDto } from '../api/types';
import { EmptyState, ErrorState, LoadingSkeleton } from '../components/States';
import { formatDateTime, reminderStatusLabels } from '../labels';

// ── Tarih yardımcıları ──────────────────────────────────────────────────────

function toIsoDate(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

type DatePreset = 'all' | 'today' | 'week' | 'month' | 'custom';

function presetRange(preset: DatePreset): { start?: string; end?: string } {
  if (preset === 'all') return {};
  const now = new Date();
  const today = toIsoDate(now);
  if (preset === 'today') return { start: today, end: today };
  if (preset === 'week') {
    const mon = new Date(now);
    mon.setDate(now.getDate() - ((now.getDay() + 6) % 7)); // Pazartesi
    return { start: toIsoDate(mon), end: today };
  }
  if (preset === 'month') {
    const ago = new Date(now);
    ago.setDate(now.getDate() - 30);
    return { start: toIsoDate(ago), end: today };
  }
  return {}; // custom — kullanıcı elle girer
}

const presetLabels: Record<DatePreset, string> = {
  all: 'Tümü',
  today: 'Bugün',
  week: 'Bu Hafta',
  month: 'Son 30 Gün',
  custom: 'Özel Aralık',
};

const statusOptions: { value: ReminderStatus | 'all'; label: string }[] = [
  { value: 'all', label: 'Tümü' },
  { value: 'Sent', label: 'Gönderildi' },
  { value: 'Failed', label: 'Başarısız' },
  { value: 'Pending', label: 'Bekliyor' },
];

// ── Bileşen ─────────────────────────────────────────────────────────────────

export function ReminderHistoryPage() {
  // Filtre state
  const [datePreset, setDatePreset] = useState<DatePreset>('all');
  const [customStart, setCustomStart] = useState('');
  const [customEnd, setCustomEnd] = useState('');
  const [selectedUser, setSelectedUser] = useState<UserDto | null>(null);
  const [statusFilter, setStatusFilter] = useState<ReminderStatus | 'all'>('all');

  // Filtre parametrelerini hesapla
  const filters = useMemo<ReminderHistoryFilters>(() => {
    const f: ReminderHistoryFilters = { take: 200 };

    if (datePreset === 'custom') {
      if (customStart) f.startDate = customStart;
      if (customEnd) f.endDate = customEnd;
    } else if (datePreset !== 'all') {
      const range = presetRange(datePreset);
      if (range.start) f.startDate = range.start;
      if (range.end) f.endDate = range.end;
    }

    if (selectedUser) f.recipientUserId = selectedUser.id;
    if (statusFilter !== 'all') f.status = statusFilter;

    return f;
  }, [datePreset, customStart, customEnd, selectedUser, statusFilter]);

  const { data, isLoading, isError, error, refetch } = useReminderHistory(filters);
  const { data: users } = useUsers();

  // Filtreleri temizle
  const clearFilters = useCallback(() => {
    setDatePreset('all');
    setCustomStart('');
    setCustomEnd('');
    setSelectedUser(null);
    setStatusFilter('all');
  }, []);

  const hasActiveFilter = datePreset !== 'all' || selectedUser !== null || statusFilter !== 'all';

  // KPI hesapla
  const kpi = useMemo(() => {
    if (!data) return null;
    const total = data.length;
    const sent = data.filter((d) => d.status === 'Sent').length;
    const failed = data.filter((d) => d.status === 'Failed').length;
    const ticketSet = new Set(data.flatMap((d) => d.ticketNumbers));
    return { total, sent, failed, ticketCount: ticketSet.size };
  }, [data]);

  return (
    <Stack spacing={2.5}>
      <Box>
        <Typography variant="h1">Hatırlatma geçmişi</Typography>
        <Typography variant="body2" color="text.secondary">
          Gönderilen tüm hatırlatmalar ve sonuçları
        </Typography>
      </Box>

      {/* ── Filtre çubuğu ──────────────────────────────────────────── */}
      <Card>
        <CardContent>
          <Stack spacing={2}>
            {/* Tarih presetleri */}
            <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
              <FilterListIcon color="action" fontSize="small" />
              <Typography variant="subtitle2" sx={{ mr: 1 }}>
                Tarih:
              </Typography>
              {(Object.keys(presetLabels) as DatePreset[]).map((p) => (
                <Chip
                  key={p}
                  label={presetLabels[p]}
                  size="small"
                  variant={datePreset === p ? 'filled' : 'outlined'}
                  color={datePreset === p ? 'primary' : 'default'}
                  onClick={() => setDatePreset(p)}
                />
              ))}
            </Stack>

            {/* Özel aralık tarih girdileri */}
            {datePreset === 'custom' && (
              <Stack direction="row" spacing={2}>
                <TextField
                  label="Başlangıç"
                  type="date"
                  size="small"
                  value={customStart}
                  onChange={(e) => setCustomStart(e.target.value)}
                  slotProps={{ inputLabel: { shrink: true } }}
                  sx={{ minWidth: 160 }}
                />
                <TextField
                  label="Bitiş"
                  type="date"
                  size="small"
                  value={customEnd}
                  onChange={(e) => setCustomEnd(e.target.value)}
                  slotProps={{ inputLabel: { shrink: true } }}
                  sx={{ minWidth: 160 }}
                />
              </Stack>
            )}

            {/* Çalışan + Durum filtresi */}
            <Stack direction="row" spacing={2} alignItems="center" flexWrap="wrap" useFlexGap>
              <Autocomplete
                size="small"
                sx={{ minWidth: 240 }}
                options={users ?? []}
                getOptionLabel={(u) => u.displayName}
                value={selectedUser}
                onChange={(_e, val) => setSelectedUser(val)}
                renderInput={(params) => (
                  <TextField {...params} label="Çalışan" placeholder="Tüm çalışanlar" />
                )}
                isOptionEqualToValue={(opt, val) => opt.id === val.id}
              />

              <Stack direction="row" spacing={0.5} alignItems="center">
                <Typography variant="subtitle2" sx={{ mr: 0.5 }}>
                  Durum:
                </Typography>
                {statusOptions.map((s) => (
                  <Chip
                    key={s.value}
                    label={s.label}
                    size="small"
                    variant={statusFilter === s.value ? 'filled' : 'outlined'}
                    color={
                      statusFilter === s.value
                        ? s.value === 'Sent'
                          ? 'success'
                          : s.value === 'Failed'
                            ? 'error'
                            : 'primary'
                        : 'default'
                    }
                    onClick={() => setStatusFilter(s.value)}
                  />
                ))}
              </Stack>

              {hasActiveFilter && (
                <Button
                  size="small"
                  startIcon={<ClearIcon />}
                  onClick={clearFilters}
                  color="inherit"
                >
                  Temizle
                </Button>
              )}
            </Stack>
          </Stack>
        </CardContent>
      </Card>

      {/* ── KPI Özet ───────────────────────────────────────────────── */}
      {kpi && kpi.total > 0 && (
        <Stack direction="row" spacing={2} flexWrap="wrap" useFlexGap>
          <KpiChip label="Toplam" value={kpi.total} color="default" />
          <KpiChip label="Başarılı" value={kpi.sent} color="success" />
          <KpiChip label="Hatalı" value={kpi.failed} color="error" />
          <KpiChip label="Ticket" value={kpi.ticketCount} color="info" />
        </Stack>
      )}

      {/* ── Tablo ──────────────────────────────────────────────────── */}
      <Card>
        <CardContent sx={{ p: isLoading || isError ? 2 : 0, '&:last-child': { pb: isLoading ? 2 : 0 } }}>
          {isLoading && <LoadingSkeleton rows={5} />}
          {isError && <ErrorState error={error} onRetry={() => void refetch()} />}

          {data && data.length === 0 && (
            <Box sx={{ p: 2 }}>
              <EmptyState
                title={hasActiveFilter ? 'Filtre sonucu bulunamadı' : 'Henüz hatırlatma gönderilmedi'}
                description={
                  hasActiveFilter
                    ? 'Seçili filtrelere uygun hatırlatma kaydı yok. Filtreleri değiştirmeyi deneyin.'
                    : 'Hatırlatma gönder ekranından bir çalışana hatırlatma oluşturabilirsiniz.'
                }
              />
            </Box>
          )}

          {data && data.length > 0 && (
            <TableContainer sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Tarih</TableCell>
                    <TableCell>Alıcı</TableCell>
                    <TableCell>Konu</TableCell>
                    <TableCell align="center">Ticket</TableCell>
                    <TableCell>Gönderen</TableCell>
                    <TableCell align="center">Durum</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {data.map((item) => (
                    <TableRow key={item.id} hover>
                      <TableCell>
                        <Typography variant="body2">{formatDateTime(item.createdAtUtc)}</Typography>
                        {item.sentAtUtc && (
                          <Typography variant="caption" color="text.secondary">
                            Gönderim: {formatDateTime(item.sentAtUtc)}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>{item.recipientName}</TableCell>
                      <TableCell sx={{ maxWidth: 320 }}>
                        <Typography variant="body2" noWrap title={item.subject}>
                          {item.subject}
                        </Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Tooltip title={item.ticketNumbers.join(', ') || '—'}>
                          <Chip label={item.ticketCount} size="small" variant="outlined" />
                        </Tooltip>
                      </TableCell>
                      <TableCell>{item.sentByName}</TableCell>
                      <TableCell align="center">
                        <Tooltip title={item.errorMessage ?? ''}>
                          <Chip
                            size="small"
                            label={reminderStatusLabels[item.status]}
                            color={
                              item.status === 'Sent' ? 'success' : item.status === 'Failed' ? 'error' : 'default'
                            }
                          />
                        </Tooltip>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </CardContent>
      </Card>
    </Stack>
  );
}

// ── KPI Chip bileşeni ─────────────────────────────────────────────────────

function KpiChip({ label, value, color }: { label: string; value: number; color: 'default' | 'success' | 'error' | 'info' }) {
  return (
    <Chip
      label={`${label}: ${value}`}
      color={color}
      variant="outlined"
      size="medium"
      sx={{ fontWeight: 600, fontSize: '0.85rem', px: 1 }}
    />
  );
}
