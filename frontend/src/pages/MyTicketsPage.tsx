import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Link,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import IconButton from '@mui/material/IconButton';
import InputAdornment from '@mui/material/InputAdornment';
import { useAddTicketNote, useChangeTicketStatus, useTicket, useTickets } from '../api/hooks';
import type { TicketStatus } from '../api/types';
import { TicketDataTable } from '../components/TicketDataTable';
import { AgingBadge, PriorityBadge, TicketStatusBadge } from '../components/Badges';
import { ErrorState, LoadingSkeleton } from '../components/States';
import { TIXBOX_DISCLAIMER, formatDateTime, ticketStatusLabels } from '../labels';
import { problemMessage } from '../api/client';

export function MyTicketsPage() {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  const { data, isLoading, isError, error, refetch } = useTickets({
    search: search || undefined,
    status: ['Unassigned', 'Assigned', 'InProgress', 'Completed'],
    pageSize: 100,
    sortBy: 'Priority',
    sortDescending: false,
  });

  return (
    <Stack spacing={2.5}>
      <Box>
        <Typography variant="h1">Ticket'larım</Typography>
        <Typography variant="body2" color="text.secondary">
          Size atanmış kayıtlar. Durumu güncellediğinizde yöneticiniz panelinde görür.
        </Typography>
      </Box>

      <Alert severity="info" variant="outlined">
        {TIXBOX_DISCLAIMER}
      </Alert>

      <TextField
        placeholder="Ticket numarası, talep eden veya uygulamada ara…"
        size="small"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        InputProps={{
          startAdornment: (
            <InputAdornment position="start">
              <SearchIcon fontSize="small" color="action" />
            </InputAdornment>
          ),
          endAdornment: search ? (
            <InputAdornment position="end">
              <IconButton size="small" onClick={() => setSearch('')} aria-label="Aramayı temizle">
                <ClearIcon fontSize="small" />
              </IconButton>
            </InputAdornment>
          ) : null,
        }}
      />

      <Card>
        <CardContent sx={{ p: 0, '&:last-child': { pb: 0 } }}>
          {isLoading && (
            <Box sx={{ p: 2 }}>
              <LoadingSkeleton rows={4} />
            </Box>
          )}
          {isError && (
            <Box sx={{ p: 2 }}>
              <ErrorState error={error} onRetry={() => void refetch()} />
            </Box>
          )}
          {data && (
            <TicketDataTable
              tickets={data.items}
              onRowClick={(t) => setSelectedId(t.id)}
              emptyTitle="Size atanmış ticket yok"
              emptyDescription="Yöneticiniz size bir ticket atadığında burada görünür."
            />
          )}
        </CardContent>
      </Card>

      <MyTicketDialog id={selectedId} onClose={() => setSelectedId(null)} />
    </Stack>
  );
}

function MyTicketDialog({ id, onClose }: { id: string | null; onClose: () => void }) {
  const { data: ticket, isLoading } = useTicket(id ?? undefined);

  return (
    <Dialog open={Boolean(id)} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Ticket detayı</DialogTitle>
      <DialogContent dividers>
        {isLoading || !ticket ? <LoadingSkeleton rows={5} /> : <MyTicketBody ticketId={ticket.id} />}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Kapat</Button>
      </DialogActions>
    </Dialog>
  );
}

function MyTicketBody({ ticketId }: { ticketId: string }) {
  const { data: ticket } = useTicket(ticketId);
  const statusMutation = useChangeTicketStatus(ticketId);
  const noteMutation = useAddTicketNote(ticketId);

  const [status, setStatus] = useState<TicketStatus | ''>('');
  const [statusNote, setStatusNote] = useState('');
  const [noteBody, setNoteBody] = useState('');
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  if (!ticket) return <LoadingSkeleton rows={5} />;

  const submitStatus = async () => {
    if (!status) return;
    setFeedback(null);
    try {
      await statusMutation.mutateAsync({ status, note: statusNote || undefined });
      setFeedback({ type: 'success', message: 'Durum güncellendi. Yöneticiniz panelinde görecek.' });
      setStatus('');
      setStatusNote('');
    } catch (err) {
      setFeedback({ type: 'error', message: problemMessage(err) });
    }
  };

  const submitNote = async () => {
    if (!noteBody.trim()) return;
    setFeedback(null);
    try {
      await noteMutation.mutateAsync({ body: noteBody });
      setFeedback({ type: 'success', message: 'Not eklendi.' });
      setNoteBody('');
    } catch (err) {
      setFeedback({ type: 'error', message: problemMessage(err) });
    }
  };

  return (
    <Stack spacing={2}>
      {feedback && (
        <Alert severity={feedback.type} onClose={() => setFeedback(null)}>
          {feedback.message}
        </Alert>
      )}

      <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
        <Typography variant="h3" fontFamily="monospace">
          {ticket.externalTicketNumber}
        </Typography>
        <PriorityBadge priority={ticket.priority} />
        <TicketStatusBadge status={ticket.status} />
        <AgingBadge aging={ticket.aging} daysOpen={ticket.daysOpen} />
      </Stack>

      <Box>
        <Typography variant="caption" color="text.secondary">
          Talep eden / uygulama
        </Typography>
        <Typography variant="body2">
          {ticket.requesterName} · {ticket.applicationName}
        </Typography>
      </Box>

      <Box>
        <Typography variant="caption" color="text.secondary">
          Açılış
        </Typography>
        <Typography variant="body2">
          {formatDateTime(ticket.originalSentAtUtc)} ({ticket.daysOpen} gün önce)
        </Typography>
      </Box>

      <Box>
        <Typography variant="caption" color="text.secondary">
          Açıklama
        </Typography>
        <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', mt: 0.5 }}>
          {ticket.description || '—'}
        </Typography>
      </Box>

      {ticket.externalUrl && (
        <Link
          href={ticket.externalUrl}
          target="_blank"
          rel="noopener noreferrer"
          sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, fontSize: 14 }}
        >
          Tixbox kaydını aç <OpenInNewIcon fontSize="inherit" />
        </Link>
      )}

      <Divider />

      {ticket.allowedNextStatuses.length > 0 ? (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2">Durumu güncelle</Typography>
          <TextField
            select
            size="small"
            label="Yeni durum"
            value={status}
            onChange={(e) => setStatus(e.target.value as TicketStatus)}
          >
            {ticket.allowedNextStatuses.map((s) => (
              <MenuItem key={s} value={s}>
                {ticketStatusLabels[s]}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            size="small"
            label="Not (opsiyonel)"
            placeholder="Yöneticiniz bu notu görecek"
            value={statusNote}
            onChange={(e) => setStatusNote(e.target.value)}
          />
          <Button
            variant="contained"
            disabled={!status || statusMutation.isPending}
            onClick={() => void submitStatus()}
          >
            Güncelle
          </Button>
        </Stack>
      ) : (
        <Alert severity="info" variant="outlined">
          Bu ticket için yapabileceğiniz bir durum değişikliği kalmadı. Yeniden açılması gerekiyorsa
          yöneticinize başvurun.
        </Alert>
      )}

      <Divider />

      <Stack spacing={1.5}>
        <Typography variant="subtitle2">Not ekle</Typography>
        <TextField
          multiline
          minRows={2}
          size="small"
          placeholder="Yalnızca panelde görünür"
          value={noteBody}
          onChange={(e) => setNoteBody(e.target.value)}
        />
        <Button variant="outlined" disabled={!noteBody.trim() || noteMutation.isPending} onClick={() => void submitNote()}>
          Not ekle
        </Button>
      </Stack>

      {ticket.notes.length > 0 && (
        <Stack spacing={1}>
          {ticket.notes.map((note) => (
            <Box key={note.id} sx={{ p: 1.5, bgcolor: 'action.hover', borderRadius: 1 }}>
              <Typography variant="caption" color="text.secondary">
                {note.authorName} · {formatDateTime(note.createdAtUtc)}
              </Typography>
              <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>
                {note.body}
              </Typography>
            </Box>
          ))}
        </Stack>
      )}

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Durum geçmişi
        </Typography>
        <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
          {ticket.statusHistory.map((h) => (
            <Chip
              key={h.id}
              size="small"
              variant="outlined"
              label={`${ticketStatusLabels[h.toStatus]} · ${h.changedByName}`}
            />
          ))}
        </Stack>
      </Box>
    </Stack>
  );
}
