import { useNavigate } from 'react-router-dom';
import {
  Alert,
  AlertTitle,
  Box,
  Button,
  Card,
  CardContent,
  CardHeader,
  Chip,
  Grid,
  Stack,
  Typography,
} from '@mui/material';
import ConfirmationNumberOutlinedIcon from '@mui/icons-material/ConfirmationNumberOutlined';
import PersonOffOutlinedIcon from '@mui/icons-material/PersonOffOutlined';
import PlayCircleOutlineIcon from '@mui/icons-material/PlayCircleOutline';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import BusinessOutlinedIcon from '@mui/icons-material/BusinessOutlined';
import HomeWorkOutlinedIcon from '@mui/icons-material/HomeWorkOutlined';
import BeachAccessOutlinedIcon from '@mui/icons-material/BeachAccessOutlined';
import EventBusyOutlinedIcon from '@mui/icons-material/EventBusyOutlined';
import RefreshIcon from '@mui/icons-material/Refresh';
import { useDashboard, useRunIngestion } from '../api/hooks';
import { MetricCard } from '../components/MetricCard';
import { TicketDataTable } from '../components/TicketDataTable';
import { EmployeeWorkloadCard } from '../components/EmployeeWorkloadCard';
import { WeeklyScheduleMatrix } from '../components/WeeklyScheduleMatrix';
import { TicketStatusBadge, WorkModeBadge } from '../components/Badges';
import { ErrorState, LoadingSkeleton } from '../components/States';
import { formatDate, formatDateTime, reminderStatusLabels, ticketStatusLabels } from '../labels';
import { problemMessage } from '../api/client';

export function ManagerDashboard() {
  const navigate = useNavigate();
  const { data, isLoading, isError, error, refetch } = useDashboard();
  const ingestion = useRunIngestion();

  if (isLoading) return <LoadingSkeleton rows={6} height={80} />;
  if (isError) return <ErrorState error={error} onRetry={() => void refetch()} />;
  if (!data) return null;

  const { metrics, todayTeamStatus, agingThresholds } = data;
  const maxOpen = Math.max(1, ...data.workload.map((w) => w.openTicketCount));

  return (
    <Stack spacing={3}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" flexWrap="wrap" useFlexGap>
        <Box>
          <Typography variant="h1">Dashboard</Typography>
          <Typography variant="body2" color="text.secondary">
            {formatDate(todayTeamStatus.date)}
            {todayTeamStatus.isHoliday ? ` · ${todayTeamStatus.holidayName}` : ''}
          </Typography>
        </Box>
        <Button
          variant="outlined"
          startIcon={<RefreshIcon />}
          onClick={() => ingestion.mutate()}
          disabled={ingestion.isPending}
        >
          {ingestion.isPending ? 'Mailler okunuyor…' : 'Mailleri şimdi oku'}
        </Button>
      </Stack>

      {ingestion.isSuccess && ingestion.data && (
        <Alert severity="success" onClose={() => ingestion.reset()}>
          {ingestion.data.messagesSeen} mail okundu · {ingestion.data.ticketsCreated} yeni ticket ·{' '}
          {ingestion.data.duplicatesSkipped} duplicate atlandı · {ingestion.data.mailsRejected} mail reddedildi
          {ingestion.data.createdTicketNumbers.length > 0 &&
            ` (${ingestion.data.createdTicketNumbers.join(', ')})`}
        </Alert>
      )}

      {ingestion.isError && (
        <Alert severity="error" onClose={() => ingestion.reset()}>
          {problemMessage(ingestion.error)}
        </Alert>
      )}

      {data.dataMismatchWarnings.length > 0 && (
        <Alert severity="error">
          <AlertTitle>Veri uyumsuzluğu ({data.dataMismatchWarnings.length})</AlertTitle>
          <Stack spacing={0.5}>
            {data.dataMismatchWarnings.map((w) => (
              <Typography key={w.id} variant="body2">
                {w.ticketNumber ? `${w.ticketNumber}: ` : ''}
                {w.message}
              </Typography>
            ))}
          </Stack>
        </Alert>
      )}

      {/* --- Metrik kartları --- */}
      <Grid container spacing={2}>
        <Grid item xs={6} md={3}>
          <MetricCard
            label="Toplam açık ticket"
            value={metrics.totalOpenTickets}
            icon={<ConfirmationNumberOutlinedIcon />}
            onClick={() => navigate('/manager/tickets')}
          />
        </Grid>
        <Grid item xs={6} md={3}>
          <MetricCard
            label="Atanmamış"
            value={metrics.unassignedTickets}
            tone={metrics.unassignedTickets > 0 ? 'warning' : 'default'}
            icon={<PersonOffOutlinedIcon />}
            onClick={() => navigate('/manager/tickets?unassigned=true')}
          />
        </Grid>
        <Grid item xs={6} md={3}>
          <MetricCard
            label="Devam eden"
            value={metrics.inProgressTickets}
            tone="info"
            icon={<PlayCircleOutlineIcon />}
          />
        </Grid>
        <Grid item xs={6} md={3}>
          <MetricCard
            label="Uzun süredir açık"
            value={metrics.staleTickets}
            tone={metrics.staleTickets > 0 ? 'error' : 'default'}
            icon={<HourglassEmptyIcon />}
            hint={`${agingThresholds.staleAfterDays}/${agingThresholds.oldAfterDays}/${agingThresholds.criticalAfterDays} gün eşikleri`}
          />
        </Grid>

        <Grid item xs={6} md={3}>
          <MetricCard label="Bugün ofiste" value={metrics.inOfficeToday} icon={<BusinessOutlinedIcon />} />
        </Grid>
        <Grid item xs={6} md={3}>
          <MetricCard label="Bugün home office" value={metrics.homeOfficeToday} icon={<HomeWorkOutlinedIcon />} />
        </Grid>
        <Grid item xs={6} md={3}>
          <MetricCard label="Bugün izinli" value={metrics.onLeaveToday} icon={<BeachAccessOutlinedIcon />} />
        </Grid>
        <Grid item xs={6} md={3}>
          <MetricCard
            label="Plan göndermeyen"
            value={metrics.missingScheduleSubmissions}
            tone={metrics.missingScheduleSubmissions > 0 ? 'warning' : 'default'}
            icon={<EventBusyOutlinedIcon />}
            hint="Gelecek hafta"
            onClick={() => navigate('/manager/team-schedule')}
          />
        </Grid>
      </Grid>

      {/* --- 1. Bugünkü ekip durumu --- */}
      <Card>
        <CardHeader title="Bugünkü ekip durumu" titleTypographyProps={{ variant: 'h3' }} />
        <CardContent>
          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            {todayTeamStatus.members.map((member) => (
              <Chip
                key={member.userId}
                label={
                  <Stack direction="row" spacing={0.75} alignItems="center">
                    <span>{member.displayName}</span>
                    <WorkModeBadge mode={member.mode} />
                  </Stack>
                }
                variant="outlined"
                sx={{ height: 'auto', py: 0.75 }}
              />
            ))}
          </Stack>
        </CardContent>
      </Card>

      {/* --- 2. Atanmamış ticket'lar --- */}
      <Card>
        <CardHeader
          title="Atanmamış ticket'lar"
          titleTypographyProps={{ variant: 'h3' }}
          action={<Button size="small" onClick={() => navigate('/manager/tickets?unassigned=true')}>Tümü</Button>}
        />
        <CardContent sx={{ pt: 0 }}>
          <TicketDataTable
            tickets={data.unassignedTickets}
            dense
            onRowClick={(t) => navigate(`/manager/tickets/${t.id}`)}
            emptyTitle="Atanmamış ticket yok"
            emptyDescription="Tüm açık ticket'lar bir çalışana atanmış durumda."
          />
        </CardContent>
      </Card>

      {/* --- 3. Dikkat gerektiren ticket'lar --- */}
      <Card>
        <CardHeader
          title="Dikkat gerektirenler"
          subheader="Uzun süredir açık, güncelleme bekleyen veya veri uyumsuzluğu olan kayıtlar"
          titleTypographyProps={{ variant: 'h3' }}
        />
        <CardContent sx={{ pt: 0 }}>
          <TicketDataTable
            tickets={data.attentionTickets}
            dense
            onRowClick={(t) => navigate(`/manager/tickets/${t.id}`)}
            emptyTitle="Dikkat gerektiren ticket yok"
          />
        </CardContent>
      </Card>

      {/* --- 4. Çalışan bazlı iş yükü --- */}
      <Box>
        <Typography variant="h3" gutterBottom>
          Çalışan bazlı açık ticket
        </Typography>
        <Grid container spacing={2}>
          {data.workload.map((w) => (
            <Grid item xs={12} sm={6} md={4} key={w.userId}>
              <EmployeeWorkloadCard
                workload={w}
                maxOpen={maxOpen}
                onClick={() => navigate(`/manager/tickets?assigneeUserId=${w.userId}`)}
              />
            </Grid>
          ))}
        </Grid>
      </Box>

      {/* --- 5. Haftalık çalışma matrisi --- */}
      <Card>
        <CardHeader
          title="Haftalık çalışma matrisi"
          subheader={`Hafta başlangıcı: ${formatDate(data.weeklyMatrix.weekStartDate)}`}
          titleTypographyProps={{ variant: 'h3' }}
          action={<Button size="small" onClick={() => navigate('/manager/team-schedule')}>Detay</Button>}
        />
        <CardContent sx={{ pt: 0 }}>
          <WeeklyScheduleMatrix matrix={data.weeklyMatrix} />
        </CardContent>
      </Card>

      {/* --- 6. Ekipten gelen güncellemeler --- */}
      <Card>
        <CardHeader
          title="Ekipten gelen güncellemeler"
          subheader="Çalışanların kendi ticket'larında yaptığı durum değişiklikleri"
          titleTypographyProps={{ variant: 'h3' }}
        />
        <CardContent sx={{ pt: 0 }}>
          {data.recentTeamUpdates.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              Henüz çalışan güncellemesi yok.
            </Typography>
          ) : (
            <Stack spacing={1.5}>
              {data.recentTeamUpdates.map((u) => (
                <Stack
                  key={`${u.ticketId}-${u.changedAtUtc}`}
                  direction="row"
                  spacing={1.5}
                  alignItems="flex-start"
                  sx={{ cursor: 'pointer' }}
                  onClick={() => navigate(`/manager/tickets/${u.ticketId}`)}
                >
                  <TicketStatusBadge status={u.toStatus} />
                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="body2">
                      <strong>{u.changedByName}</strong> · {u.externalTicketNumber} ({u.applicationName})
                      {u.fromStatus ? ` — ${ticketStatusLabels[u.fromStatus]} → ` : ' — '}
                      {ticketStatusLabels[u.toStatus]}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {formatDateTime(u.changedAtUtc)}
                      {u.note ? ` · "${u.note}"` : ''}
                    </Typography>
                  </Box>
                </Stack>
              ))}
            </Stack>
          )}
        </CardContent>
      </Card>

      {/* --- 7. Son hatırlatmalar --- */}
      <Card>
        <CardHeader
          title="Son gönderilen hatırlatmalar"
          titleTypographyProps={{ variant: 'h3' }}
          action={<Button size="small" onClick={() => navigate('/manager/reminder-history')}>Tümü</Button>}
        />
        <CardContent sx={{ pt: 0 }}>
          {data.recentReminders.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              Henüz hatırlatma gönderilmedi.
            </Typography>
          ) : (
            <Stack spacing={1}>
              {data.recentReminders.map((r) => (
                <Stack
                  key={r.id}
                  direction="row"
                  spacing={1}
                  alignItems="center"
                  justifyContent="space-between"
                  sx={{ py: 0.5 }}
                >
                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="body2" noWrap>
                      {r.recipientName} — {r.subject}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {formatDateTime(r.createdAtUtc)} · {r.ticketCount} ticket · {r.sentByName}
                    </Typography>
                  </Box>
                  <Chip
                    size="small"
                    label={reminderStatusLabels[r.status]}
                    color={r.status === 'Sent' ? 'success' : r.status === 'Failed' ? 'error' : 'default'}
                  />
                </Stack>
              ))}
            </Stack>
          )}
        </CardContent>
      </Card>
    </Stack>
  );
}
