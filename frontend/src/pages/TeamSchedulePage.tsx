import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CardHeader,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import { useScheduleDecision, useScheduleOverride, useTeamMatrix, useTodayStatus } from '../api/hooks';
import type { WeeklyScheduleRowDto, WorkMode } from '../api/types';
import { WeeklyScheduleMatrix } from '../components/WeeklyScheduleMatrix';
import { WorkModeBadge } from '../components/Badges';
import { ErrorState, LoadingSkeleton } from '../components/States';
import { formatDate, formatDayShort, toIsoDate, workModeLabels } from '../labels';
import { problemMessage } from '../api/client';

function mondayOf(date: Date): Date {
  const copy = new Date(date);
  const offset = (copy.getDay() + 6) % 7;
  copy.setDate(copy.getDate() - offset);
  copy.setHours(0, 0, 0, 0);
  return copy;
}

export function TeamSchedulePage() {
  const [weekStart, setWeekStart] = useState(() => toIsoDate(mondayOf(new Date())));
  const { data: matrix, isLoading, isError, error, refetch } = useTeamMatrix(weekStart);
  const { data: today } = useTodayStatus();

  const decision = useScheduleDecision(weekStart);
  const override = useScheduleOverride(weekStart);

  const [overrideTarget, setOverrideTarget] = useState<{ row: WeeklyScheduleRowDto; date: string } | null>(null);
  const [overrideMode, setOverrideMode] = useState<WorkMode>('Office');
  const [overrideNote, setOverrideNote] = useState('');
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const shiftWeek = (weeks: number) => {
    const current = new Date(`${weekStart}T00:00:00`);
    current.setDate(current.getDate() + weeks * 7);
    setWeekStart(toIsoDate(current));
  };

  const handleDecision = async (row: WeeklyScheduleRowDto, value: 'Approved' | 'Rejected') => {
    if (!row.weekId) return;
    setFeedback(null);
    try {
      await decision.mutateAsync({ weekId: row.weekId, decision: value });
      setFeedback({
        type: 'success',
        message: `${row.displayName} planı ${value === 'Approved' ? 'onaylandı' : 'reddedildi'}.`,
      });
    } catch (err) {
      setFeedback({ type: 'error', message: problemMessage(err) });
    }
  };

  const submitOverride = async () => {
    if (!overrideTarget?.row.weekId) return;
    setFeedback(null);
    try {
      await override.mutateAsync({
        weekId: overrideTarget.row.weekId,
        date: overrideTarget.date,
        mode: overrideMode,
        note: overrideNote || undefined,
      });
      setFeedback({ type: 'success', message: 'Gün güncellendi.' });
      setOverrideTarget(null);
      setOverrideNote('');
    } catch (err) {
      setFeedback({ type: 'error', message: problemMessage(err) });
    }
  };

  return (
    <Stack spacing={2.5}>
      <Box>
        <Typography variant="h1">Ekip çalışma takvimi</Typography>
        <Typography variant="body2" color="text.secondary">
          Haftalık ofis / home office / izin dağılımı
        </Typography>
      </Box>

      {feedback && (
        <Alert severity={feedback.type} onClose={() => setFeedback(null)}>
          {feedback.message}
        </Alert>
      )}

      {today && (
        <Card>
          <CardHeader
            title={`Bugün — ${formatDate(today.date)}`}
            subheader={today.isHoliday ? today.holidayName : undefined}
            titleTypographyProps={{ variant: 'h3' }}
          />
          <CardContent>
            <Stack direction="row" spacing={2} flexWrap="wrap" useFlexGap>
              {today.members.map((member) => (
                <Stack key={member.userId} spacing={0.5} sx={{ minWidth: 160 }}>
                  <Typography variant="body2" fontWeight={600}>
                    {member.displayName}
                  </Typography>
                  <WorkModeBadge mode={member.mode} />
                  {!member.hasSubmittedWeek && (
                    <Typography variant="caption" color="warning.main">
                      Plan göndermedi
                    </Typography>
                  )}
                </Stack>
              ))}
            </Stack>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader
          title="Haftalık matris"
          subheader={`${formatDate(weekStart)} haftası`}
          titleTypographyProps={{ variant: 'h3' }}
          action={
            <Stack direction="row" spacing={1} alignItems="center">
              <Button size="small" startIcon={<ChevronLeftIcon />} onClick={() => shiftWeek(-1)}>
                Önceki
              </Button>
              <Button size="small" onClick={() => setWeekStart(toIsoDate(mondayOf(new Date())))}>
                Bu hafta
              </Button>
              <Button size="small" endIcon={<ChevronRightIcon />} onClick={() => shiftWeek(1)}>
                Sonraki
              </Button>
            </Stack>
          }
        />
        <CardContent sx={{ pt: 0 }}>
          {isLoading && <LoadingSkeleton rows={5} />}
          {isError && <ErrorState error={error} onRetry={() => void refetch()} />}
          {matrix && (
            <WeeklyScheduleMatrix
              matrix={matrix}
              onCellClick={(row, date) => {
                if (!row.weekId) return;
                const cell = row.cells.find((c) => c.date === date);
                if (cell?.isHoliday) return;
                setOverrideTarget({ row, date });
                setOverrideMode(cell?.mode ?? 'Office');
                setOverrideNote('');
              }}
              renderRowActions={(row) =>
                row.weekId && row.status === 'Submitted' ? (
                  <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                    <Button size="small" color="success" onClick={() => void handleDecision(row, 'Approved')}>
                      Onayla
                    </Button>
                    <Button size="small" color="error" onClick={() => void handleDecision(row, 'Rejected')}>
                      Reddet
                    </Button>
                  </Stack>
                ) : null
              }
            />
          )}
          <Typography variant="caption" color="text.secondary" sx={{ mt: 1.5, display: 'block' }}>
            Bir hücreye tıklayarak yönetici olarak günü değiştirebilirsiniz.
          </Typography>
        </CardContent>
      </Card>

      <Dialog open={Boolean(overrideTarget)} onClose={() => setOverrideTarget(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Günü değiştir</DialogTitle>
        <DialogContent>
          {overrideTarget && (
            <Stack spacing={2} sx={{ mt: 1 }}>
              <Typography variant="body2">
                {overrideTarget.row.displayName} — {formatDayShort(overrideTarget.date)}
              </Typography>
              <TextField
                select
                label="Çalışma şekli"
                size="small"
                value={overrideMode}
                onChange={(e) => setOverrideMode(e.target.value as WorkMode)}
              >
                {(['Office', 'HomeOffice', 'Leave'] as WorkMode[]).map((mode) => (
                  <MenuItem key={mode} value={mode}>
                    {workModeLabels[mode]}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                label="Gerekçe (opsiyonel)"
                size="small"
                value={overrideNote}
                onChange={(e) => setOverrideNote(e.target.value)}
              />
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOverrideTarget(null)}>Vazgeç</Button>
          <Button variant="contained" onClick={() => void submitOverride()} disabled={override.isPending}>
            Kaydet
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
}
