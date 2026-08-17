import { Alert, AlertTitle, Box, Button, Paper, Skeleton, Stack, Typography } from '@mui/material';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import type { ReactNode } from 'react';
import { problemMessage } from '../api/client';

export function LoadingSkeleton({ rows = 4, height = 48 }: { rows?: number; height?: number }) {
  return (
    <Stack spacing={1} aria-busy="true" aria-label="Yükleniyor">
      {Array.from({ length: rows }).map((_, index) => (
        <Skeleton key={index} variant="rounded" height={height} />
      ))}
    </Stack>
  );
}

export function EmptyState({
  title,
  description,
  action,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <Paper
      variant="outlined"
      sx={{ p: 4, textAlign: 'center', borderStyle: 'dashed', bgcolor: 'transparent' }}
    >
      <Box sx={{ color: 'text.disabled', mb: 1 }}>
        <InboxOutlinedIcon fontSize="large" />
      </Box>
      <Typography variant="subtitle1" fontWeight={600}>
        {title}
      </Typography>
      {description && (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          {description}
        </Typography>
      )}
      {action && <Box sx={{ mt: 2 }}>{action}</Box>}
    </Paper>
  );
}

export function ErrorState({
  error,
  onRetry,
  title = 'Bir hata oluştu',
}: {
  error: unknown;
  onRetry?: () => void;
  title?: string;
}) {
  // Ham axios metni yerine anlaşılır karşılığı gösterilir.
  const message = typeof error === 'string' ? error : problemMessage(error);

  return (
    <Alert
      severity="error"
      action={
        onRetry && (
          <Button color="inherit" size="small" onClick={onRetry}>
            Tekrar dene
          </Button>
        )
      }
    >
      <AlertTitle>{title}</AlertTitle>
      {message}
    </Alert>
  );
}
