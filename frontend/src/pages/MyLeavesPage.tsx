import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  IconButton,
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
import AddIcon from '@mui/icons-material/Add';
import CancelOutlinedIcon from '@mui/icons-material/CancelOutlined';
import { useMyLeaves, useCancelLeave } from '../api/hooks';
import { leaveTypeLabels, leaveStatusLabels, leaveTypeColors, formatDate, formatDateTime } from '../labels';
import { CreateLeaveDialog } from '../components/CreateLeaveDialog';
import { EmptyState, ErrorState, LoadingSkeleton } from '../components/States';
import { problemMessage } from '../api/client';

export function MyLeavesPage() {
  const { data, isLoading, isError, error, refetch } = useMyLeaves();
  const cancel = useCancelLeave();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const handleCancel = async (leaveId: string) => {
    setFeedback(null);
    try {
      await cancel.mutateAsync(leaveId);
      setFeedback({ type: 'success', message: 'İzin talebi iptal edildi.' });
    } catch (err) {
      setFeedback({ type: 'error', message: problemMessage(err) });
    }
  };

  const statusColor = (status: string) => {
    switch (status) {
      case 'Approved': return 'success';
      case 'Rejected': return 'error';
      case 'Cancelled': return 'default';
      default: return 'warning';
    }
  };

  return (
    <Stack spacing={2.5}>
      <Stack direction="row" alignItems="center" justifyContent="space-between">
        <Box>
          <Typography variant="h1">İzinlerim</Typography>
          <Typography variant="body2" color="text.secondary">
            İzin talepleriniz ve durumları
          </Typography>
        </Box>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => setDialogOpen(true)}
        >
          Yeni İzin Talebi
        </Button>
      </Stack>

      {feedback && (
        <Alert severity={feedback.type} onClose={() => setFeedback(null)}>
          {feedback.message}
        </Alert>
      )}

      <Card>
        <CardContent sx={{ p: isLoading || isError ? 2 : 0, '&:last-child': { pb: isLoading ? 2 : 0 } }}>
          {isLoading && <LoadingSkeleton rows={5} />}
          {isError && <ErrorState error={error} onRetry={() => void refetch()} />}

          {data && data.length === 0 && (
            <Box sx={{ p: 2 }}>
              <EmptyState
                title="Henüz izin talebiniz yok"
                description="Yeni İzin Talebi butonuyla izin oluşturabilirsiniz."
              />
            </Box>
          )}

          {data && data.length > 0 && (
            <TableContainer sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Tarih Aralığı</TableCell>
                    <TableCell>Tür</TableCell>
                    <TableCell align="center">Gün</TableCell>
                    <TableCell>Durum</TableCell>
                    <TableCell>Açıklama</TableCell>
                    <TableCell>Değerlendiren</TableCell>
                    <TableCell>Oluşturma</TableCell>
                    <TableCell align="center">İşlem</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {data.map((leave) => (
                    <TableRow key={leave.id} hover>
                      <TableCell>
                        <Typography variant="body2">
                          {formatDate(leave.startDate)} — {formatDate(leave.endDate)}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={leaveTypeLabels[leave.type]}
                          size="small"
                          sx={{
                            bgcolor: leaveTypeColors[leave.type],
                            color: '#fff',
                            fontSize: '0.75rem',
                          }}
                        />
                      </TableCell>
                      <TableCell align="center">
                        <Typography variant="body2" fontWeight={600}>
                          {leave.dayCount}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={leaveStatusLabels[leave.status]}
                          size="small"
                          color={statusColor(leave.status) as 'success' | 'error' | 'default' | 'warning'}
                        />
                      </TableCell>
                      <TableCell sx={{ maxWidth: 200 }}>
                        <Typography variant="body2" noWrap title={leave.description ?? ''}>
                          {leave.description ?? '—'}
                        </Typography>
                        {leave.reviewNote && (
                          <Typography variant="caption" color="text.secondary" display="block">
                            Not: {leave.reviewNote}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>
                        {leave.reviewedByName ? (
                          <Stack spacing={0}>
                            <Typography variant="body2">{leave.reviewedByName}</Typography>
                            {leave.reviewedAtUtc && (
                              <Typography variant="caption" color="text.secondary">
                                {formatDateTime(leave.reviewedAtUtc)}
                              </Typography>
                            )}
                          </Stack>
                        ) : (
                          <Typography variant="body2" color="text.secondary">—</Typography>
                        )}
                      </TableCell>
                      <TableCell>
                        <Typography variant="caption">{formatDateTime(leave.createdAtUtc)}</Typography>
                      </TableCell>
                      <TableCell align="center">
                        {leave.status === 'Pending' && (
                          <Tooltip title="İptal et">
                            <IconButton
                              size="small"
                              color="error"
                              onClick={() => void handleCancel(leave.id)}
                              disabled={cancel.isPending}
                            >
                              <CancelOutlinedIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </CardContent>
      </Card>

      <CreateLeaveDialog open={dialogOpen} onClose={() => setDialogOpen(false)} />
    </Stack>
  );
}
