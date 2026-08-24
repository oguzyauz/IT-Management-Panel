import { useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
  Typography,
  Alert,
} from '@mui/material';
import { useCreateLeave } from '../api/hooks';
import type { LeaveType } from '../api/types';
import { leaveTypeLabels } from '../labels';
import { problemMessage } from '../api/client';

const leaveTypes: LeaveType[] = ['Annual', 'Personal', 'Medical', 'Unpaid'];

interface Props {
  open: boolean;
  onClose: () => void;
  /** Dialog açılırken önceden seçilmiş tarih. */
  initialDate?: string;
}

export function CreateLeaveDialog({ open, onClose, initialDate }: Props) {
  const [startDate, setStartDate] = useState(initialDate ?? '');
  const [endDate, setEndDate] = useState(initialDate ?? '');
  const [type, setType] = useState<LeaveType>('Annual');
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);

  const create = useCreateLeave();

  const resetForm = () => {
    setStartDate(initialDate ?? '');
    setEndDate(initialDate ?? '');
    setType('Annual');
    setDescription('');
    setError(null);
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  const handleSubmit = async () => {
    setError(null);

    if (!startDate || !endDate) {
      setError('Başlangıç ve bitiş tarihleri zorunludur.');
      return;
    }

    if (endDate < startDate) {
      setError('Bitiş tarihi başlangıçtan önce olamaz.');
      return;
    }

    try {
      await create.mutateAsync({
        startDate,
        endDate,
        type,
        description: description.trim() || undefined,
      });
      handleClose();
    } catch (err) {
      setError(problemMessage(err));
    }
  };

  // Gün sayısı hesabı (hafta sonları dahil basit fark)
  const dayCount =
    startDate && endDate && endDate >= startDate
      ? Math.round(
          (new Date(`${endDate}T00:00:00`).getTime() - new Date(`${startDate}T00:00:00`).getTime()) /
            (1000 * 60 * 60 * 24),
        ) + 1
      : 0;

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Yeni İzin Talebi</DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" onClose={() => setError(null)}>
              {error}
            </Alert>
          )}

          <Stack direction="row" spacing={2}>
            <TextField
              label="Başlangıç"
              type="date"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              fullWidth
              required
            />
            <TextField
              label="Bitiş"
              type="date"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              fullWidth
              required
            />
          </Stack>

          {dayCount > 0 && (
            <Typography variant="body2" color="text.secondary">
              Toplam: <strong>{dayCount} gün</strong>
            </Typography>
          )}

          <TextField
            select
            label="İzin Türü"
            value={type}
            onChange={(e) => setType(e.target.value as LeaveType)}
            fullWidth
          >
            {leaveTypes.map((t) => (
              <MenuItem key={t} value={t}>
                {leaveTypeLabels[t]}
              </MenuItem>
            ))}
          </TextField>

          <TextField
            label="Açıklama (opsiyonel)"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            multiline
            minRows={2}
            maxRows={4}
            fullWidth
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} color="inherit">
          Vazgeç
        </Button>
        <Button onClick={handleSubmit} variant="contained" disabled={create.isPending}>
          {create.isPending ? 'Gönderiliyor…' : 'Talep Oluştur'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
