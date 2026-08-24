import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useChangePassword } from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { problemMessage } from '../api/client';
import { hasPasswordError, passwordHelperText, validatePassword } from '../utils/passwordValidation';

/**
 * Hem zorunlu parola değişimi (yönetici geçici parola verdiğinde) hem de kullanıcının
 * kendi isteğiyle değiştirmesi için aynı ekran kullanılır.
 */
export function ChangePasswordPage() {
  const { user, isManager, mustChangePassword } = useAuth();
  const change = useChangePassword();
  const navigate = useNavigate();

  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [repeat, setRepeat] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const mismatch = repeat.length > 0 && next !== repeat;

  const submit = async () => {
    setFormError(null);

    if (next !== repeat) {
      setFormError('Yeni parolalar birbirini tutmuyor.');
      return;
    }

    try {
      await change.mutateAsync({ currentPassword: current, newPassword: next });
      setDone(true);
      setCurrent('');
      setNext('');
      setRepeat('');
    } catch (err) {
      setFormError(problemMessage(err));
    }
  };

  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
      <Card sx={{ maxWidth: 460, width: '100%' }}>
        <CardContent sx={{ p: 3 }}>
          <Stack spacing={2}>
            <Box>
              <Typography variant="h1">Parola değiştir</Typography>
              <Typography variant="body2" color="text.secondary">
                {user?.email}
              </Typography>
            </Box>

            {done ? (
              <>
                <Alert severity="success">
                  Parolanız değiştirildi. Diğer cihazlardaki oturumlarınız kapatıldı.
                </Alert>
                <Button
                  variant="contained"
                  onClick={() => navigate(isManager ? '/manager/dashboard' : '/employee/my-tickets')}
                >
                  Panele dön
                </Button>
              </>
            ) : (
              <>
                {mustChangePassword ? (
                  <Alert severity="warning">
                    Hesabınıza yönetici tarafından geçici bir parola verildi. Devam etmek için kendi
                    parolanızı belirlemeniz gerekiyor.
                  </Alert>
                ) : (
                  <Alert severity="info">
                    Parolanızı değiştirdiğinizde bu cihaz dışındaki oturumlarınız kapanır.
                  </Alert>
                )}

                {formError && <Alert severity="error">{formError}</Alert>}

                <TextField
                  label="Mevcut parola"
                  type="password"
                  size="small"
                  value={current}
                  onChange={(e) => setCurrent(e.target.value)}
                  autoComplete="current-password"
                />
                <TextField
                  label="Yeni parola"
                  type="password"
                  size="small"
                  value={next}
                  onChange={(e) => setNext(e.target.value)}
                  error={next.length > 0 && hasPasswordError(validatePassword(next))}
                  helperText={passwordHelperText(next)}
                  autoComplete="new-password"
                />
                <TextField
                  label="Yeni parola (tekrar)"
                  type="password"
                  size="small"
                  value={repeat}
                  onChange={(e) => setRepeat(e.target.value)}
                  error={mismatch}
                  helperText={mismatch ? 'Parolalar birbirini tutmuyor' : ' '}
                  autoComplete="new-password"
                />

                <Button
                  variant="contained"
                  disabled={!current || hasPasswordError(validatePassword(next)) || mismatch || change.isPending}
                  onClick={() => void submit()}
                >
                  {change.isPending ? 'Değiştiriliyor…' : 'Parolayı değiştir'}
                </Button>
              </>
            )}
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
