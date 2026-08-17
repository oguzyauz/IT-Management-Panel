import { useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CardHeader,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import SendOutlinedIcon from '@mui/icons-material/SendOutlined';
import { useReminderPreview, useReminderTemplates, useSendReminder, useTickets, useUsers } from '../api/hooks';
import { TicketDataTable } from '../components/TicketDataTable';
import { ReminderPreviewDialog } from '../components/ReminderPreviewDialog';
import { EmptyState, ErrorState, LoadingSkeleton } from '../components/States';
import { problemMessage } from '../api/client';

export function RemindersPage() {
  const { data: users } = useUsers();
  const { data: templates } = useReminderTemplates();

  const [recipientId, setRecipientId] = useState('');
  const [templateId, setTemplateId] = useState('');
  const [selectedTicketIds, setSelectedTicketIds] = useState<string[]>([]);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const filters = useMemo(
    () => ({
      assigneeUserId: recipientId || undefined,
      status: ['Assigned', 'InProgress'] as const,
      pageSize: 100,
    }),
    [recipientId],
  );

  const { data: tickets, isLoading, isError, error, refetch } = useTickets({
    ...filters,
    status: [...filters.status],
  });

  const preview = useReminderPreview();
  const send = useSendReminder();

  const assignableUsers = (users ?? []).filter((u) => !u.roles.includes('ADMIN'));

  const openPreview = async () => {
    setFeedback(null);
    try {
      await preview.mutateAsync({
        recipientUserId: recipientId,
        ticketIds: selectedTicketIds,
        templateId: templateId || null,
      });
      setDialogOpen(true);
    } catch (err) {
      setFeedback({ type: 'error', message: problemMessage(err) });
    }
  };

  const handleSend = async (payload: { subject: string; body: string; cc: string[] }) => {
    setFeedback(null);
    try {
      const result = await send.mutateAsync({
        recipientUserId: recipientId,
        ticketIds: selectedTicketIds,
        templateId: templateId || null,
        subject: payload.subject,
        body: payload.body,
        cc: payload.cc,
        confirmed: true,
      });

      setDialogOpen(false);
      setSelectedTicketIds([]);
      setFeedback({
        type: result.status === 'Sent' ? 'success' : 'error',
        message:
          result.status === 'Sent'
            ? `Hatırlatma ${result.recipientName} kişisine gönderildi (${result.ticketCount} ticket).`
            : `Gönderim başarısız: ${result.errorMessage ?? 'bilinmeyen hata'}`,
      });
    } catch (err) {
      setFeedback({ type: 'error', message: problemMessage(err) });
    }
  };

  return (
    <Stack spacing={2.5}>
      <Box>
        <Typography variant="h1">Hatırlatma gönder</Typography>
        <Typography variant="body2" color="text.secondary">
          Çalışan seç → ticket'ları işaretle → önizle → onayla → gönder
        </Typography>
      </Box>

      {feedback && (
        <Alert severity={feedback.type} onClose={() => setFeedback(null)}>
          {feedback.message}
        </Alert>
      )}

      <Card>
        <CardHeader title="1. Çalışan ve şablon" titleTypographyProps={{ variant: 'h3' }} />
        <CardContent>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField
              select
              label="Çalışan"
              size="small"
              value={recipientId}
              onChange={(e) => {
                setRecipientId(e.target.value);
                setSelectedTicketIds([]);
              }}
              sx={{ minWidth: 260 }}
            >
              {assignableUsers.map((u) => (
                <MenuItem key={u.id} value={u.id}>
                  {u.displayName}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Şablon"
              size="small"
              value={templateId}
              onChange={(e) => setTemplateId(e.target.value)}
              sx={{ minWidth: 260 }}
            >
              <MenuItem value="">Varsayılan</MenuItem>
              {(templates ?? []).map((t) => (
                <MenuItem key={t.id} value={t.id}>
                  {t.name}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
        </CardContent>
      </Card>

      <Card>
        <CardHeader
          title="2. Ticket seç"
          subheader="Yalnızca seçilen çalışana atanmış, açık ticket'lar listelenir"
          titleTypographyProps={{ variant: 'h3' }}
        />
        <CardContent sx={{ pt: 0 }}>
          {!recipientId && <EmptyState title="Önce bir çalışan seçin" />}
          {recipientId && isLoading && <LoadingSkeleton rows={4} />}
          {recipientId && isError && <ErrorState error={error} onRetry={() => void refetch()} />}
          {recipientId && tickets && (
            <TicketDataTable
              tickets={tickets.items}
              dense
              selectable
              selectedIds={selectedTicketIds}
              onSelectionChange={setSelectedTicketIds}
              emptyTitle="Bu çalışanda açık ticket yok"
              emptyDescription="Atanmış veya devam eden ticket bulunamadı."
            />
          )}
        </CardContent>
      </Card>

      <Stack direction="row" spacing={2} alignItems="center">
        <Typography variant="body2" color="text.secondary">
          {selectedTicketIds.length} ticket seçildi
        </Typography>
        <Box sx={{ flexGrow: 1 }} />
        <Button
          variant="contained"
          startIcon={<SendOutlinedIcon />}
          disabled={!recipientId || selectedTicketIds.length === 0 || preview.isPending}
          onClick={() => void openPreview()}
        >
          Önizlemeyi aç
        </Button>
      </Stack>

      <ReminderPreviewDialog
        open={dialogOpen}
        preview={preview.data}
        isLoading={preview.isPending}
        isSending={send.isPending}
        error={send.isError ? problemMessage(send.error) : null}
        onClose={() => setDialogOpen(false)}
        onSend={(payload) => void handleSend(payload)}
      />
    </Stack>
  );
}
