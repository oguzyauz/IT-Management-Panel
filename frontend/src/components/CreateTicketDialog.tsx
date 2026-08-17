import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useCreateTicket, useUsers } from '../api/hooks';
import { problemMessage } from '../api/client';
import { TIXBOX_DISCLAIMER, toIsoDate } from '../labels';
import { useState } from 'react';

/** Ayrıştırıcıyla aynı kural: I veya S + 6 hane + alt çizgi + 6 hane. */
const TICKET_NUMBER = /^[ISis]\d{6}_\d{6}$/;

const schema = z.object({
  externalTicketNumber: z
    .string()
    .trim()
    .regex(TICKET_NUMBER, 'Örnek: I260729_000144 (I veya S ile başlar)'),
  requesterName: z.string().trim().min(1, 'Talep eden zorunlu'),
  applicationName: z.string().trim().min(1, 'Uygulama zorunlu'),
  priority: z.coerce.number().int().min(1, '1-5 arası').max(5, '1-5 arası'),
  openedOn: z.string().min(1, 'Açılış tarihi zorunlu'),
  openedAt: z.string().min(1, 'Saat zorunlu'),
  description: z.string().optional(),
  categoryPath: z.string().optional(),
  externalReference: z.string().optional(),
  externalUrl: z.string().optional(),
  assigneeUserId: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

export function CreateTicketDialog({
  open,
  onClose,
  onCreated,
}: {
  open: boolean;
  onClose: () => void;
  onCreated?: (id: string) => void;
}) {
  const { data: users } = useUsers();
  const create = useCreateTicket();
  const [error, setError] = useState<string | null>(null);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      externalTicketNumber: '',
      requesterName: '',
      applicationName: '',
      priority: 3,
      openedOn: toIsoDate(new Date()),
      openedAt: '09:00',
      description: '',
      categoryPath: '',
      externalReference: '',
      externalUrl: '',
      assigneeUserId: '',
    },
  });

  const close = () => {
    form.reset();
    setError(null);
    onClose();
  };

  const submit = form.handleSubmit(async (v) => {
    setError(null);
    try {
      // Girilen tarih/saat yerel kabul edilir ve UTC'ye çevrilir.
      const local = new Date(`${v.openedOn}T${v.openedAt}:00`);

      const created = await create.mutateAsync({
        externalTicketNumber: v.externalTicketNumber.trim().toUpperCase(),
        requesterName: v.requesterName,
        applicationName: v.applicationName,
        priority: v.priority,
        originalSentAtUtc: local.toISOString(),
        description: v.description || undefined,
        categoryPath: v.categoryPath || undefined,
        externalReference: v.externalReference || undefined,
        externalUrl: v.externalUrl || undefined,
        assigneeUserId: v.assigneeUserId || null,
      });

      onCreated?.(created.id);
      close();
    } catch (err) {
      setError(problemMessage(err));
    }
  });

  const assignable = (users ?? []).filter((u) => !u.roles.includes('ADMIN'));

  return (
    <Dialog open={open} onClose={close} maxWidth="sm" fullWidth>
      <DialogTitle>Elle ticket ekle</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          <Alert severity="info" variant="outlined" sx={{ fontSize: 13 }}>
            {TIXBOX_DISCLAIMER} Buradan eklenen kayıt Tixbox'ta ticket <strong>açmaz</strong>;
            yalnızca panelde takip edilir.
          </Alert>

          {error && <Alert severity="error">{error}</Alert>}

          <TextField
            label="Ticket numarası"
            placeholder="I260729_000144"
            size="small"
            required
            error={Boolean(form.formState.errors.externalTicketNumber)}
            helperText={form.formState.errors.externalTicketNumber?.message ?? 'Tixbox numarası'}
            {...form.register('externalTicketNumber')}
          />

          <Stack direction="row" spacing={2}>
            <TextField
              label="Talep eden"
              placeholder="Turcan, Merve"
              size="small"
              required
              fullWidth
              error={Boolean(form.formState.errors.requesterName)}
              helperText={form.formState.errors.requesterName?.message ?? '"Soyad, Ad" da yazabilirsiniz'}
              {...form.register('requesterName')}
            />
            <TextField
              label="Uygulama"
              placeholder="ERP TR"
              size="small"
              required
              fullWidth
              error={Boolean(form.formState.errors.applicationName)}
              helperText={form.formState.errors.applicationName?.message}
              {...form.register('applicationName')}
            />
          </Stack>

          <Stack direction="row" spacing={2}>
            <TextField
              select
              label="Öncelik"
              size="small"
              sx={{ minWidth: 120 }}
              error={Boolean(form.formState.errors.priority)}
              {...form.register('priority')}
              value={form.watch('priority')}
            >
              {[1, 2, 3, 4, 5].map((p) => (
                <MenuItem key={p} value={p}>
                  P{p}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              label="Açılış tarihi"
              type="date"
              size="small"
              fullWidth
              InputLabelProps={{ shrink: true }}
              error={Boolean(form.formState.errors.openedOn)}
              helperText={form.formState.errors.openedOn?.message}
              {...form.register('openedOn')}
            />
            <TextField
              label="Saat"
              type="time"
              size="small"
              sx={{ minWidth: 130 }}
              InputLabelProps={{ shrink: true }}
              error={Boolean(form.formState.errors.openedAt)}
              {...form.register('openedAt')}
            />
          </Stack>

          <Typography variant="caption" color="text.secondary">
            Açılış tarihi, ticket'ın Tixbox'ta açıldığı andır — "kaç gündür açık" hesabı buna dayanır.
          </Typography>

          <TextField
            label="Açıklama"
            size="small"
            multiline
            minRows={3}
            {...form.register('description')}
          />

          <Stack direction="row" spacing={2}>
            <TextField label="Kategori" size="small" fullWidth {...form.register('categoryPath')} />
            <TextField label="Dış referans" size="small" sx={{ minWidth: 160 }} {...form.register('externalReference')} />
          </Stack>

          <TextField
            label="Tixbox bağlantısı"
            placeholder="https://tixcore.menarini.com/..."
            size="small"
            {...form.register('externalUrl')}
          />

          <TextField
            select
            label="Sorumlu (opsiyonel)"
            size="small"
            {...form.register('assigneeUserId')}
            value={form.watch('assigneeUserId')}
          >
            <MenuItem value="">Atanmamış bırak</MenuItem>
            {assignable.map((u) => (
              <MenuItem key={u.id} value={u.id}>
                {u.displayName}
              </MenuItem>
            ))}
          </TextField>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={close}>Vazgeç</Button>
        <Button variant="contained" onClick={() => void submit()} disabled={create.isPending}>
          {create.isPending ? 'Ekleniyor…' : 'Ticket ekle'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
