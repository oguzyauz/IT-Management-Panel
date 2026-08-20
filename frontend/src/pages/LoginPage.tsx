import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Alert,
  Avatar,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  List,
  ListItemAvatar,
  ListItemButton,
  ListItemText,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { api, problemMessage } from '../api/client';
import { useInitialSetup, useLogin, useMockUsers, useSetupStatus } from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import type { UserDto } from '../api/types';
import { ErrorState, LoadingSkeleton } from '../components/States';

/** Rol kodlarından yönetici olup olmadığını çıkarır — birkaç yerde gerekiyor. */
const isManagerRole = (roles: string[]) => roles.includes('MANAGER') || roles.includes('ADMIN');

const landingFor = (roles: string[]) =>
  isManagerRole(roles) ? '/manager/dashboard' : '/employee/my-tickets';

export function LoginPage() {
  const { data: status, isLoading, isError, error, refetch } = useSetupStatus();

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        bgcolor: 'background.default',
        p: 2,
      }}
    >
      <Card sx={{ maxWidth: 460, width: '100%' }}>
        <CardContent sx={{ p: 3 }}>
          <Stack spacing={0.5} sx={{ mb: 2.5 }}>
            <Typography variant="h1">IT Yönetim Paneli</Typography>
            <Typography variant="body2" color="text.secondary">
              Mail tabanlı ticket takibi
            </Typography>
          </Stack>

          {isLoading && <LoadingSkeleton rows={4} />}
          {isError && <ErrorState error={error} onRetry={() => void refetch()} />}

          {status?.needsInitialSetup && <InitialSetupForm suggestedEmail={status.adminEmail} />}

          {status && !status.needsInitialSetup &&
            (status.authProvider === 'Local' || status.authProvider === 'Ldap') &&
            <PasswordForm />}

          {status && !status.needsInitialSetup &&
            status.authProvider !== 'Local' && status.authProvider !== 'Ldap' &&
            <MockUserPicker />}
        </CardContent>
      </Card>
    </Box>
  );
}

/**
 * İlk açılış: yönetici hesabının parolasını belirler. Bu ekran yalnızca sistemde
 * hiç parola yokken görünür, sonrasında sunucu bu ucu kapatır.
 */
function InitialSetupForm({ suggestedEmail }: { suggestedEmail?: string | null }) {
  const setup = useInitialSetup();
  const login = useLogin();
  const auth = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState(suggestedEmail ?? '');
  const [password, setPassword] = useState('');
  const [repeat, setRepeat] = useState('');
  const [formError, setFormError] = useState<string | null>(null);

  const mismatch = repeat.length > 0 && password !== repeat;

  const submit = async () => {
    setFormError(null);

    if (password !== repeat) {
      setFormError('Parolalar birbirini tutmuyor.');
      return;
    }

    try {
      await setup.mutateAsync({ email, password });

      // Kurulumdan hemen sonra giriş yapılır; kullanıcı parolayı iki kez yazmasın.
      const session = await login.mutateAsync({ email, password });
      auth.login(session.token);
      navigate(landingFor(session.user.roles), { replace: true });
    } catch (err) {
      setFormError(problemMessage(err));
    }
  };

  return (
    <Stack spacing={2}>
      <Alert severity="info">
        <strong>İlk kurulum.</strong> Yönetici hesabının parolasını belirleyin. Diğer kullanıcıları
        daha sonra Yönetim ekranından ekleyebilirsiniz.
      </Alert>

      {formError && <Alert severity="error">{formError}</Alert>}

      <TextField
        label="Yönetici e-postası"
        size="small"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        autoComplete="username"
      />
      <TextField
        label="Parola"
        type="password"
        size="small"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        helperText="En az 8 karakter"
        autoComplete="new-password"
      />
      <TextField
        label="Parola (tekrar)"
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
        size="large"
        disabled={!email || password.length < 8 || mismatch || setup.isPending || login.isPending}
        onClick={() => void submit()}
      >
        {setup.isPending || login.isPending ? 'Kuruluyor…' : 'Kurulumu tamamla'}
      </Button>
    </Stack>
  );
}

function PasswordForm() {
  const login = useLogin();
  const auth = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [formError, setFormError] = useState<string | null>(null);

  const submit = async () => {
    setFormError(null);
    try {
      const session = await login.mutateAsync({ email, password });
      auth.login(session.token);

      // Yönetici geçici parola verdiyse kullanıcı önce onu değiştirir.
      navigate(session.mustChangePassword ? '/parola-degistir' : landingFor(session.user.roles), {
        replace: true,
      });
    } catch (err) {
      setFormError(problemMessage(err));
    }
  };

  return (
    <Stack
      component="form"
      spacing={2}
      onSubmit={(e) => {
        e.preventDefault();
        void submit();
      }}
    >
      {formError && <Alert severity="error">{formError}</Alert>}

      <TextField
        label="E-posta"
        size="small"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        autoComplete="username"
        autoFocus
      />
      <TextField
        label="Parola"
        type="password"
        size="small"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        autoComplete="current-password"
      />

      <Button type="submit" variant="contained" size="large" disabled={!email || !password || login.isPending}>
        {login.isPending ? 'Giriş yapılıyor…' : 'Giriş yap'}
      </Button>

      <Typography variant="caption" color="text.secondary">
        Parolanızı unuttuysanız yöneticinizden sıfırlamasını isteyin.
      </Typography>
    </Stack>
  );
}

/** Geliştirme modu: parola yok, kullanıcı listeden seçilir. */
function MockUserPicker() {
  const { data: users, isLoading, isError, error, refetch } = useMockUsers();
  const { login } = useAuth();
  const navigate = useNavigate();
  const [busyId, setBusyId] = useState<string | null>(null);
  const [loginError, setLoginError] = useState<string | null>(null);

  const handleLogin = async (user: UserDto) => {
    setBusyId(user.id);
    setLoginError(null);
    try {
      const response = await api.post<{ token: string; user: UserDto }>('/auth/mock-login', {
        userId: user.id,
      });
      login(response.data.token);
      navigate(landingFor(user.roles), { replace: true });
    } catch (err) {
      setLoginError(problemMessage(err));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <>
      <Alert severity="warning" sx={{ mb: 2 }}>
        <strong>Geliştirme modu.</strong> Parola sorulmaz, kullanıcı listeden seçilir. Gerçek
        kullanımda <code>Auth:Provider</code> ayarı <code>Local</code> olmalıdır.
      </Alert>

      {loginError && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setLoginError(null)}>
          {loginError}
        </Alert>
      )}

      {isLoading && <LoadingSkeleton rows={4} />}
      {isError && <ErrorState error={error} onRetry={() => void refetch()} />}

      {users && (
        <>
          <Divider sx={{ mb: 1 }} />
          <List disablePadding>
            {users.map((user) => (
              <ListItemButton
                key={user.id}
                onClick={() => void handleLogin(user)}
                disabled={busyId !== null}
                sx={{ borderRadius: 1.5, mb: 0.5 }}
              >
                <ListItemAvatar>
                  <Avatar sx={{ bgcolor: 'primary.main' }}>{user.displayName.charAt(0)}</Avatar>
                </ListItemAvatar>
                <ListItemText primary={user.displayName} secondary={user.title ?? user.email} />
                <Chip
                  size="small"
                  label={isManagerRole(user.roles) ? 'Yönetici' : 'Çalışan'}
                  color={isManagerRole(user.roles) ? 'primary' : 'default'}
                />
              </ListItemButton>
            ))}
          </List>
        </>
      )}
    </>
  );
}
