import { Box, Card, CardContent, LinearProgress, Stack, Typography } from '@mui/material';
import type { EmployeeWorkloadDto } from '../api/types';
import { WorkModeBadge } from './Badges';

export function EmployeeWorkloadCard({
  workload,
  maxOpen,
  onClick,
}: {
  workload: EmployeeWorkloadDto;
  maxOpen: number;
  onClick?: () => void;
}) {
  const ratio = maxOpen > 0 ? (workload.openTicketCount / maxOpen) * 100 : 0;

  return (
    <Card sx={{ cursor: onClick ? 'pointer' : 'default' }} onClick={onClick}>
      <CardContent>
        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="subtitle2" noWrap>
              {workload.displayName}
            </Typography>
            <Typography variant="caption" color="text.secondary" noWrap>
              {workload.title ?? '—'}
            </Typography>
          </Box>
          <WorkModeBadge mode={workload.todayMode} />
        </Stack>

        <Stack direction="row" spacing={2} sx={{ mt: 1.5 }}>
          <Metric label="Açık" value={workload.openTicketCount} />
          <Metric label="Devam eden" value={workload.inProgressCount} />
          <Metric label="Bekleyen" value={workload.staleCount} tone="warning.main" />
        </Stack>

        <LinearProgress
          variant="determinate"
          value={Math.min(100, ratio)}
          sx={{ mt: 1.5, height: 6, borderRadius: 3 }}
        />
      </CardContent>
    </Card>
  );
}

function Metric({ label, value, tone }: { label: string; value: number; tone?: string }) {
  return (
    <Box>
      <Typography variant="h6" fontWeight={700} color={tone}>
        {value}
      </Typography>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
    </Box>
  );
}
