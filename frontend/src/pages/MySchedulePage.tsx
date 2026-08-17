import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CardHeader,
  Chip,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import { useMyWeek, useSaveMyWeek } from '../api/hooks';
import type { WorkMode } from '../api/types';
import { ErrorState, LoadingSkeleton } from '../components/States';
import { formatDate, formatDateTime, formatDayShort, scheduleStatusLabels, workModeLabels } from '../labels';
import { problemMessage } from '../api/client';

const modes: WorkMode[] = ['Office', 'HomeOffice', 'Leave'];

export function MySchedulePage() {
  const { data, isLoading, isError, error, refetch } = useMyWeek();
  const save = useSaveMyWeek();

  const [selection, setSelection] = useState<Record<string, WorkMode>>({});
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  useEffect(() => {
    if (!data) return;
    const initial: Record<string, WorkMode> = {};
    for (const day of data.days) {
      if (day.mode) initial[day.date] = day.mode;
    }
    setSelection(initial);
  }, [data]);

  if (isLoading) return <LoadingSkeleton rows={6} height={64} />;
  if (isError) return <ErrorState error={error} onRetry={() => void refetch()} />;
  if (!data) return null;

  const editableDays = data.days.filter((d) => !d.isHoliday && !d.isManagerOverride);
  const allFilled = editableDays.every((d) => selection[d.date]);
  const readOnly = data.isLocked || data.status === 'Approved';

  const counts = {
    office: Object.values(selection).filter((m) => m === 'Office').length,
    home: Object.values(selection).filter((m) => m === 'HomeOffice').length,
    leave: Object.values(selection).filter((m) => m === 'Leave').length,
  };

  const submit = async (shouldSubmit: boolean) => {
    setFeedback(null);
    try {
      await save.mutateAsync({
        weekStartDate: data.weekStartDate,
        days: Object.entries(selection).map(([date, mode]) => ({ date, mode })),
        submit: shouldSubmit,
      });
      setFeedback({
        type: 'success',
        message: shouldSubmit ? 'Planınız gönderildi.' : 'Taslak kaydedildi.',
      });
    } catch (err) {
      setFeedback({ type: 'error', message: problemMessage(err) });
    }
  };

  return (
    <Stack spacing={2.5}>
      <Box>
        <Typography variant="h1">Çalışma planım</Typography>
        <Typography variant="body2" color="text.secondary">
          {formatDate(data.weekStartDate)} haftası
        </Typography>
      </Box>

      {feedback && (
        <Alert severity={feedback.type} onClose={() => setFeedback(null)}>
          {feedback.message}
        </Alert>
      )}

      <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap alignItems="center">
        <Chip label={scheduleStatusLabels[data.status]} color={data.status === 'Approved' ? 'success' : 'default'} />
        <Chip
          variant="outlined"
          label={`Kural: en az ${data.rules.requiredOfficeDays} ofis, en fazla ${data.rules.requiredHomeOfficeDays} home office`}
        />
        {data.lockDeadlineUtc && (
          <Chip
            variant="outlined"
            color={data.isLocked ? 'error' : 'default'}
            label={`Son gönderim: ${formatDateTime(data.lockDeadlineUtc)}`}
          />
        )}
      </Stack>

      {data.isLocked && (
        <Alert severity="warning">
          Bu hafta için gönderim süresi doldu. Değişiklik gerekiyorsa yöneticinize başvurun.
        </Alert>
      )}

      {data.status === 'Approved' && (
        <Alert severity="success">Planınız onaylandı ve artık değiştirilemez.</Alert>
      )}

      {data.hasRuleViolation && data.ruleViolationNote && (
        <Alert severity="warning">
          {data.ruleViolationNote} — Gönderim engellenmez, ancak yöneticiniz bu uyarıyı görür.
        </Alert>
      )}

      <Card>
        <CardHeader title="Günler" titleTypographyProps={{ variant: 'h3' }} />
        <CardContent>
          <Stack spacing={2}>
            {data.days.map((day) => (
              <Stack
                key={day.date}
                direction={{ xs: 'column', sm: 'row' }}
                spacing={1.5}
                alignItems={{ sm: 'center' }}
                justifyContent="space-between"
              >
                <Box sx={{ minWidth: 160 }}>
                  <Typography variant="subtitle2">{formatDayShort(day.date)}</Typography>
                  {day.isHoliday && (
                    <Typography variant="caption" color="text.secondary">
                      {day.holidayName}
                    </Typography>
                  )}
                  {day.isManagerOverride && (
                    <Typography variant="caption" color="warning.main" display="block">
                      Yönetici değişikliği{day.overrideNote ? `: ${day.overrideNote}` : ''}
                    </Typography>
                  )}
                </Box>

                {day.isHoliday ? (
                  <Chip label="Resmî tatil" variant="outlined" />
                ) : (
                  <ToggleButtonGroup
                    exclusive
                    size="small"
                    value={selection[day.date] ?? null}
                    disabled={readOnly || day.isManagerOverride}
                    onChange={(_, value) => {
                      if (!value) return;
                      setSelection((prev) => ({ ...prev, [day.date]: value as WorkMode }));
                    }}
                  >
                    {modes.map((mode) => (
                      <ToggleButton key={mode} value={mode}>
                        {workModeLabels[mode]}
                      </ToggleButton>
                    ))}
                  </ToggleButtonGroup>
                )}
              </Stack>
            ))}
          </Stack>
        </CardContent>
      </Card>

      <Stack direction="row" spacing={2} alignItems="center" flexWrap="wrap" useFlexGap>
        <Typography variant="body2" color="text.secondary">
          Ofis: {counts.office} · Home office: {counts.home} · İzin: {counts.leave}
        </Typography>
        <Box sx={{ flexGrow: 1 }} />
        <Button variant="outlined" disabled={readOnly || save.isPending} onClick={() => void submit(false)}>
          Taslak kaydet
        </Button>
        <Button
          variant="contained"
          disabled={readOnly || save.isPending || !allFilled}
          onClick={() => void submit(true)}
        >
          Planı gönder
        </Button>
      </Stack>

      {!allFilled && !readOnly && (
        <Typography variant="caption" color="text.secondary">
          Gönderebilmek için tüm iş günlerini doldurun.
        </Typography>
      )}

      {data.decisions.length > 0 && (
        <Card>
          <CardHeader title="Yönetici kararları" titleTypographyProps={{ variant: 'h3' }} />
          <CardContent>
            <Stack spacing={1}>
              {data.decisions.map((d) => (
                <Typography key={d.id} variant="body2">
                  {formatDateTime(d.decidedAtUtc)} — {d.decision === 'Approved' ? 'Onaylandı' : 'Reddedildi'} (
                  {d.decidedByName}){d.comment ? ` · ${d.comment}` : ''}
                </Typography>
              ))}
            </Stack>
          </CardContent>
        </Card>
      )}
    </Stack>
  );
}
