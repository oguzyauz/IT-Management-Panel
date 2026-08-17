import {
  Box,
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
  Tooltip,
  Typography,
} from '@mui/material';
import { useReminderHistory } from '../api/hooks';
import { EmptyState, ErrorState, LoadingSkeleton } from '../components/States';
import { formatDateTime, reminderStatusLabels } from '../labels';

export function ReminderHistoryPage() {
  const { data, isLoading, isError, error, refetch } = useReminderHistory();

  return (
    <Stack spacing={2.5}>
      <Box>
        <Typography variant="h1">Hatırlatma geçmişi</Typography>
        <Typography variant="body2" color="text.secondary">
          Gönderilen tüm hatırlatmalar ve sonuçları
        </Typography>
      </Box>

      <Card>
        <CardContent sx={{ p: isLoading || isError ? 2 : 0, '&:last-child': { pb: isLoading ? 2 : 0 } }}>
          {isLoading && <LoadingSkeleton rows={5} />}
          {isError && <ErrorState error={error} onRetry={() => void refetch()} />}

          {data && data.length === 0 && (
            <Box sx={{ p: 2 }}>
              <EmptyState
                title="Henüz hatırlatma gönderilmedi"
                description="Hatırlatma gönder ekranından bir çalışana hatırlatma oluşturabilirsiniz."
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
