import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Alert,
  Box,
  Button,
  Chip,
  Divider,
  Drawer,
  IconButton,
  Link,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import type { TicketDetailDto, TicketStatus, UserDto } from '../api/types';
import { AgingBadge, PriorityBadge, TicketStatusBadge } from './Badges';
import { TIXBOX_DISCLAIMER, formatDateTime, ticketStatusLabels, ticketTypeLabels } from '../labels';
import { LoadingSkeleton } from './States';
import { problemMessage } from '../api/client';
import { useAddTicketNote, useAssignTicket, useChangeTicketStatus } from '../api/hooks';

const assignSchema = z.object({
  assigneeUserId: z.string().uuid('Çalışan seçilmelidir.'),
  note: z.string().max(500, 'Not en fazla 500 karakter olabilir.').optional(),
});

const statusSchema = z.object({
  status: z.string().min(1, 'Durum seçilmelidir.'),
  note: z.string().max(500).optional(),
});

const noteSchema = z.object({
  body: z.string().min(1, 'Not boş olamaz.').max(4000),
});

interface Props {
  open: boolean;
  onClose: () => void;
  ticket: TicketDetailDto | undefined;
  isLoading: boolean;
  users: UserDto[];
  onOpenFullPage?: () => void;
}

export function TicketDetailDrawer({ open, onClose, ticket, isLoading, users, onOpenFullPage }: Props) {
  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      PaperProps={{ sx: { width: { xs: '100%', sm: 560 }, p: 0 } }}
    >
      <Stack
        direction="row"
        alignItems="center"
        justifyContent="space-between"
        sx={{ p: 2, borderBottom: '1px solid', borderColor: 'divider' }}
      >
        <Typography variant="h3">Ticket detayı</Typography>
        <Stack direction="row" spacing={1} alignItems="center">
          {onOpenFullPage && (
            <Button size="small" onClick={onOpenFullPage}>
              Sayfada aç
            </Button>
          )}
          <IconButton onClick={onClose} aria-label="Kapat">
            <CloseIcon />
          </IconButton>
        </Stack>
      </Stack>

      <Box sx={{ p: 2, overflowY: 'auto' }}>
        {isLoading || !ticket ? <LoadingSkeleton rows={6} /> : <TicketDetailBody ticket={ticket} users={users} />}
      </Box>
    </Drawer>
  );
}

export function TicketDetailBody({ ticket, users }: { ticket: TicketDetailDto; users: UserDto[] }) {
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const assignMutation = useAssignTicket(ticket.id);
  const statusMutation = useChangeTicketStatus(ticket.id);
  const noteMutation = useAddTicketNote(ticket.id);

  const assignForm = useForm({
    resolver: zodResolver(assignSchema),
    defaultValues: { assigneeUserId: ticket.assigneeUserId ?? '', note: '' },
  });

  const statusForm = useForm({
    resolver: zodResolver(statusSchema),
    defaultValues: { status: '', note: '' },
  });

  const noteForm = useForm({
    resolver: zodResolver(noteSchema),
    defaultValues: { body: '' },
  });

  const assignableUsers = users.filter((u) => u.roles.includes('EMPLOYEE') || u.roles.includes('MANAGER'));

  return (
    <Stack spacing={2.5}>
      {feedback && (
        <Alert severity={feedback.type} onClose={() => setFeedback(null)}>
          {feedback.message}
        </Alert>
      )}

      <Stack spacing={1}>
        <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
          <Typography variant="h2" fontFamily="monospace">
            {ticket.externalTicketNumber}
          </Typography>
          <Chip label={ticketTypeLabels[ticket.ticketType]} size="small" variant="outlined" />
          <PriorityBadge priority={ticket.priority} />
          <TicketStatusBadge status={ticket.status} />
          <AgingBadge aging={ticket.aging} daysOpen={ticket.daysOpen} />
        </Stack>

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
      </Stack>

      {ticket.parseWarnings.filter((w) => w.severity === 'Error').length > 0 && (
        <Alert severity="error">
          <Typography variant="subtitle2">Veri uyumsuzluğu</Typography>
          {ticket.parseWarnings
            .filter((w) => w.severity === 'Error')
            .map((w) => (
              <Typography key={w.id} variant="body2">
                {w.message}
              </Typography>
            ))}
        </Alert>
      )}

      <Field label="Talep eden" value={ticket.requesterName} />
      <Field label="Uygulama" value={ticket.applicationName} />
      <Field label="Kategori" value={ticket.categoryPath ?? '—'} />
      <Field label="Dış referans" value={ticket.externalReference ?? '—'} />
      <Field label="Açılış (Tixbox)" value={formatDateTime(ticket.originalSentAtUtc)} />
      <Field
        label="Sorumlu"
        value={
          ticket.assigneeName
            ? ticket.autoAssigned
              ? `${ticket.assigneeName} (kişiye özel mail — otomatik atandı)`
              : ticket.assigneeName
            : 'Atanmamış'
        }
      />

      <Box>
        <Typography variant="caption" color="text.secondary">
          Açıklama
        </Typography>
        <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', mt: 0.5 }}>
          {ticket.description || '—'}
        </Typography>
      </Box>

      <Divider />

      <Alert severity="info" variant="outlined">
        {TIXBOX_DISCLAIMER}
      </Alert>

      {/* --- Atama --- */}
      <Box
        component="form"
        onSubmit={assignForm.handleSubmit(async (values) => {
          try {
            await assignMutation.mutateAsync({ assigneeUserId: values.assigneeUserId, note: values.note });
            setFeedback({ type: 'success', message: 'Ticket atandı.' });
          } catch (error) {
            setFeedback({ type: 'error', message: problemMessage(error) });
          }
        })}
      >
        <Typography variant="subtitle2" gutterBottom>
          {ticket.assigneeUserId ? 'Yeniden ata' : 'Çalışana ata'}
        </Typography>
        <Stack spacing={1.5}>
          <TextField
            select
            size="small"
            label="Çalışan"
            error={Boolean(assignForm.formState.errors.assigneeUserId)}
            helperText={assignForm.formState.errors.assigneeUserId?.message}
            {...assignForm.register('assigneeUserId')}
            value={assignForm.watch('assigneeUserId')}
          >
            {assignableUsers.map((u) => (
              <MenuItem key={u.id} value={u.id}>
                {u.displayName}
                {u.title ? ` — ${u.title}` : ''}
              </MenuItem>
            ))}
          </TextField>
          <TextField size="small" label="Not (opsiyonel)" {...assignForm.register('note')} />
          <Button type="submit" variant="contained" disabled={assignMutation.isPending}>
            {ticket.assigneeUserId ? 'Yeniden ata' : 'Ata'}
          </Button>
        </Stack>
      </Box>

      <Divider />

      {/* --- Durum --- */}
      <Box
        component="form"
        onSubmit={statusForm.handleSubmit(async (values) => {
          try {
            await statusMutation.mutateAsync({ status: values.status as TicketStatus, note: values.note });
            setFeedback({ type: 'success', message: 'Takip durumu güncellendi (Tixbox etkilenmedi).' });
            statusForm.reset({ status: '', note: '' });
          } catch (error) {
            setFeedback({ type: 'error', message: problemMessage(error) });
          }
        })}
      >
        <Typography variant="subtitle2" gutterBottom>
          Takip durumunu değiştir
        </Typography>
        <Stack spacing={1.5}>
          <TextField
            select
            size="small"
            label="Yeni durum"
            error={Boolean(statusForm.formState.errors.status)}
            helperText={statusForm.formState.errors.status?.message}
            {...statusForm.register('status')}
            value={statusForm.watch('status')}
          >
            {ticket.allowedNextStatuses.map((s) => (
              <MenuItem key={s} value={s}>
                {ticketStatusLabels[s]}
              </MenuItem>
            ))}
          </TextField>
          <TextField size="small" label="Not (opsiyonel)" {...statusForm.register('note')} />
          <Button type="submit" variant="outlined" disabled={statusMutation.isPending}>
            Durumu güncelle
          </Button>
        </Stack>
      </Box>

      <Divider />

      {/* --- Not --- */}
      <Box
        component="form"
        onSubmit={noteForm.handleSubmit(async (values) => {
          try {
            await noteMutation.mutateAsync({ body: values.body });
            setFeedback({ type: 'success', message: 'Dahili not eklendi.' });
            noteForm.reset({ body: '' });
          } catch (error) {
            setFeedback({ type: 'error', message: problemMessage(error) });
          }
        })}
      >
        <Typography variant="subtitle2" gutterBottom>
          Dahili not ekle
        </Typography>
        <Stack spacing={1.5}>
          <TextField
            multiline
            minRows={2}
            size="small"
            placeholder="Yalnızca panelde görünür"
            error={Boolean(noteForm.formState.errors.body)}
            helperText={noteForm.formState.errors.body?.message}
            {...noteForm.register('body')}
          />
          <Button type="submit" variant="text" disabled={noteMutation.isPending}>
            Not ekle
          </Button>
        </Stack>
      </Box>

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

      <Divider />

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Mail kaynakları ({ticket.mailSources.length})
        </Typography>
        <Stack spacing={1}>
          {ticket.mailSources.length === 0 && (
            <Typography variant="caption" color="text.secondary">
              {ticket.createdManually
                ? 'Mail kaynağı yok — bu kayıt panelden elle eklendi.'
                : 'Mail kaynağı yok.'}
            </Typography>
          )}
          {ticket.mailSources.map((source) => (
            <Box key={source.id} sx={{ fontSize: 13 }}>
              <Typography variant="body2">
                {source.isForwarded ? `İletildi: ${source.forwardedBy}` : 'Doğrudan mail'}
              </Typography>
              {/* Hangi posta kutusu okunurken bulundu — çoklu kutu okumasında ayırt edici. */}
              <Typography variant="caption" color="text.secondary" display="block">
                Okunduğu kutu: {source.sourceMailbox}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Orijinal gönderen {source.originalSender} · {formatDateTime(source.originalSentAtUtc)} ·{' '}
                {source.originalRecipients.length} alıcı
              </Typography>
            </Box>
          ))}
        </Stack>
      </Box>

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Durum geçmişi
        </Typography>
        <Stack spacing={0.75}>
          {ticket.statusHistory.map((h) => (
            <Typography key={h.id} variant="caption" color="text.secondary">
              {formatDateTime(h.changedAtUtc)} — {h.fromStatus ? ticketStatusLabels[h.fromStatus] : 'Yeni'} →{' '}
              {ticketStatusLabels[h.toStatus]} ({h.changedByName})
              {h.note ? ` · ${h.note}` : ''}
            </Typography>
          ))}
        </Stack>
      </Box>
    </Stack>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="body2">{value}</Typography>
    </Box>
  );
}
