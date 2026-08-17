import { Card, CardActionArea, CardContent, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';

interface MetricCardProps {
  label: string;
  value: number | string;
  icon?: ReactNode;
  hint?: string;
  tone?: 'default' | 'warning' | 'error' | 'success' | 'info';
  onClick?: () => void;
}

const toneColors = {
  default: 'text.primary',
  warning: 'warning.main',
  error: 'error.main',
  success: 'success.main',
  info: 'info.main',
} as const;

export function MetricCard({ label, value, icon, hint, tone = 'default', onClick }: MetricCardProps) {
  const content = (
    <CardContent>
      <Stack direction="row" alignItems="flex-start" justifyContent="space-between" spacing={1}>
        <Stack spacing={0.5} sx={{ minWidth: 0 }}>
          <Typography variant="body2" color="text.secondary" noWrap>
            {label}
          </Typography>
          <Typography variant="h4" fontWeight={700} color={toneColors[tone]}>
            {value}
          </Typography>
          {hint && (
            <Typography variant="caption" color="text.secondary">
              {hint}
            </Typography>
          )}
        </Stack>
        {icon && <Stack sx={{ color: toneColors[tone], opacity: 0.7 }}>{icon}</Stack>}
      </Stack>
    </CardContent>
  );

  return (
    <Card sx={{ height: '100%' }}>
      {onClick ? <CardActionArea onClick={onClick} sx={{ height: '100%' }}>{content}</CardActionArea> : content}
    </Card>
  );
}
