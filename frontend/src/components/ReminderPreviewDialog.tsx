import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import type { ReminderPreviewDto } from '../api/types';
import { formatDateTime } from '../labels';
import { LoadingSkeleton } from './States';

interface Props {
  open: boolean;
  preview: ReminderPreviewDto | undefined;
  isLoading: boolean;
  isSending: boolean;
  error?: string | null;
  onClose: () => void;
  onSend: (payload: { subject: string; body: string; cc: string[] }) => void;
}

/**
 * Hatırlatma önizlemesi. Gönderim, müdür <b>açıkça onay kutusunu işaretlemeden</b>
 * etkinleşmez (bkz. docs/revised-scope.md §10).
 */
export function ReminderPreviewDialog({
  open,
  preview,
  isLoading,
  isSending,
  error,
  onClose,
  onSend,
}: Props) {
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');
  const [cc, setCc] = useState('');
  const [confirmed, setConfirmed] = useState(false);

  useEffect(() => {
    if (!preview) return;
    setSubject(preview.subject);
    setBody(preview.body);
    setCc(preview.cc.join(', '));
    setConfirmed(false);
  }, [preview]);

  const canSend = confirmed && subject.trim().length > 0 && body.trim().length > 0 && !isSending;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Hatırlatma maili önizlemesi</DialogTitle>
      <DialogContent dividers>
        {isLoading || !preview ? (
          <LoadingSkeleton rows={6} />
        ) : (
          <Stack spacing={2}>
            {error && <Alert severity="error">{error}</Alert>}

            <Box>
              <Typography variant="caption" color="text.secondary">
                Alıcı
              </Typography>
              <Typography variant="body2" fontWeight={600}>
                {preview.recipientName} &lt;{preview.recipientEmail}&gt;
              </Typography>
            </Box>

            <Box>
              <Typography variant="caption" color="text.secondary">
                Ticket'lar ({preview.tickets.length})
              </Typography>
              <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap sx={{ mt: 0.5 }}>
                {preview.tickets.map((t) => (
                  <Chip key={t.id} label={t.externalTicketNumber} size="small" variant="outlined" />
                ))}
              </Stack>
            </Box>

            {preview.lastReminderSentAtUtc && (
              <Alert severity="info">
                Bu çalışana en son {formatDateTime(preview.lastReminderSentAtUtc)} tarihinde hatırlatma
                gönderildi.
              </Alert>
            )}

            {preview.providerName === 'Mock' && (
              <Alert severity="warning">
                Mail sağlayıcısı <strong>Mock</strong> modunda. Gönderim gerçekten yapılmaz; içerik
                <code> outbox</code> klasörüne yazılır ve kayıt altına alınır.
              </Alert>
            )}

            <TextField
              label="Konu"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              fullWidth
              size="small"
            />

            <TextField
              label="CC (virgülle ayrılmış)"
              value={cc}
              onChange={(e) => setCc(e.target.value)}
              fullWidth
              size="small"
            />

            <TextField
              label="Mail gövdesi"
              value={body}
              onChange={(e) => setBody(e.target.value)}
              fullWidth
              multiline
              minRows={12}
            />

            <FormControlLabel
              control={<Checkbox checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />}
              label="Bu maili yukarıdaki alıcıya göndermeyi onaylıyorum."
            />
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Vazgeç</Button>
        <Button
          variant="contained"
          disabled={!canSend}
          onClick={() =>
            onSend({
              subject,
              body,
              cc: cc
                .split(',')
                .map((x) => x.trim())
                .filter(Boolean),
            })
          }
        >
          {isSending ? 'Gönderiliyor…' : 'Onayla ve gönder'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
