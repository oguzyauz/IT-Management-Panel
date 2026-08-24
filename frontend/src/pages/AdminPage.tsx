import { useEffect, useState } from 'react';
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
  IconButton,
  MenuItem,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import {
  useAddMailbox,
  useAppSettings,
  useAuthorizeMailbox,
  useCreateUser,
  useGmailStatus,
  useGmailSyncState,
  useManagedUsers,
  useRemoveMailbox,
  useRescanMailbox,
  useResetUserPassword,
  useRunIngestion,
  useSetUserActive,
  useUpdateAppSettings,
} from '../api/hooks';
import type { ManagedUserDto } from '../api/types';
import { problemMessage } from '../api/client';
import { ErrorState, LoadingSkeleton } from '../components/States';
import { formatDateTime } from '../labels';
import { hasPasswordError, passwordHelperText, validatePassword } from '../utils/passwordValidation';

type Feedback = { type: 'success' | 'error' | 'info'; message: string } | null;

export function AdminPage() {
  const [tab, setTab] = useState(0);

  return (
    <Stack spacing={2.5}>
      <Box>
        <Typography variant="h1">Yönetim</Typography>
        <Typography variant="body2" color="text.secondary">
          Kullanıcılar, posta kutuları ve panel ayarları
        </Typography>
      </Box>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable" allowScrollButtonsMobile>
        <Tab label="Kullanıcılar" />
        <Tab label="Posta kutuları" />
        <Tab label="Ayarlar" />
      </Tabs>

      {tab === 0 && <UsersTab />}
      {tab === 1 && <MailboxesTab />}
      {tab === 2 && <SettingsTab />}
    </Stack>
  );
}

// --- Kullanıcılar -------------------------------------------------------------------------------

function UsersTab() {
  const { data: users, isLoading, isError, error, refetch } = useManagedUsers();
  const setActive = useSetUserActive();
  const { authProvider } = useAuth();

  const [createOpen, setCreateOpen] = useState(false);
  const [resetTarget, setResetTarget] = useState<ManagedUserDto | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(null);

  const toggleActive = async (user: ManagedUserDto) => {
    setFeedback(null);
    try {
      await setActive.mutateAsync({ userId: user.id, isActive: !user.isActive });
      setFeedback({
        type: 'success',
        message: user.isActive
          ? `${user.displayName} pasifleştirildi; açık oturumu kapatıldı.`
          : `${user.displayName} yeniden aktif.`,
      });
    } catch (err) {
      setFeedback({ type: 'error', message: problemMessage(err) });
    }
  };

  return (
    <Stack spacing={2}>
      <Alert severity="info" variant="outlined">
        Kullanıcılar <strong>silinmez</strong>. Pasifleştirilen kişi giriş yapamaz ve yeni ticket
        alamaz, ama geçmiş atamalarda ve durum geçmişinde görünmeye devam eder.
      </Alert>

      {feedback && (
        <Alert severity={feedback.type} onClose={() => setFeedback(null)}>
          {feedback.message}
        </Alert>
      )}

      <Box>
        <Button variant="contained" onClick={() => setCreateOpen(true)}>
          Kullanıcı ekle
        </Button>
      </Box>

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

          {users && (
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Ad soyad</TableCell>
                    <TableCell>E-posta</TableCell>
                    <TableCell>Rol</TableCell>
                    <TableCell>Durum</TableCell>
                    <TableCell>Son giriş</TableCell>
                    <TableCell align="right" />
                  </TableRow>
                </TableHead>
                <TableBody>
                  {users.map((user) => (
                    <TableRow key={user.id} hover>
                      <TableCell>
                        <Typography variant="body2">{user.displayName}</Typography>
                        {user.title && (
                          <Typography variant="caption" color="text.secondary">
                            {user.title}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>{user.email}</TableCell>
                      <TableCell>
                        {user.roles.map((role) => (
                          <Chip
                            key={role}
                            size="small"
                            label={roleLabels[role] ?? role}
                            color={role === 'EMPLOYEE' ? 'default' : 'primary'}
                            sx={{ mr: 0.5 }}
                          />
                        ))}
                      </TableCell>
                      <TableCell>
                        <Stack spacing={0.25}>
                          <Typography variant="body2" color={user.isActive ? 'text.primary' : 'text.disabled'}>
                            {user.isActive ? 'Aktif' : 'Pasif'}
                          </Typography>
                          {!user.hasPassword && (
                            <Typography variant="caption" color="warning.main">
                              parola tanımlı değil
                            </Typography>
                          )}
                          {user.mustChangePassword && (
                            <Typography variant="caption" color="text.secondary">
                              ilk girişte değiştirecek
                            </Typography>
                          )}
                          {user.isLockedOut && (
                            <Typography variant="caption" color="error.main">
                              geçici olarak kilitli
                            </Typography>
                          )}
                        </Stack>
                      </TableCell>
                      <TableCell>
                        <Typography variant="caption" color="text.secondary">
                          {user.lastLoginAtUtc ? formatDateTime(user.lastLoginAtUtc) : 'hiç girmedi'}
                        </Typography>
                      </TableCell>
                      <TableCell align="right">
                        <Stack direction="row" spacing={1} justifyContent="flex-end">
                          {authProvider !== 'Ldap' && (
                            <Button size="small" onClick={() => setResetTarget(user)}>
                              Parola sıfırla
                            </Button>
                          )}
                          <Button
                            size="small"
                            color={user.isActive ? 'warning' : 'primary'}
                            onClick={() => void toggleActive(user)}
                            disabled={setActive.isPending}
                          >
                            {user.isActive ? 'Pasifleştir' : 'Aktifleştir'}
                          </Button>
                        </Stack>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          )}
        </CardContent>
      </Card>

      <CreateUserDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={(message) => setFeedback({ type: 'success', message })}
      />
      <ResetPasswordDialog
        user={resetTarget}
        onClose={() => setResetTarget(null)}
        onReset={(message) => setFeedback({ type: 'success', message })}
      />
    </Stack>
  );
}

const roleLabels: Record<string, string> = {
  ADMIN: 'Sistem yöneticisi',
  MANAGER: 'Yönetici',
  EMPLOYEE: 'Çalışan',
};

function CreateUserDialog({
  open,
  onClose,
  onCreated,
}: {
  open: boolean;
  onClose: () => void;
  onCreated: (message: string) => void;
}) {
  const create = useCreateUser();
  const { authProvider } = useAuth();
  const isLdap = authProvider === 'Ldap';

  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [title, setTitle] = useState('');
  const [role, setRole] = useState('EMPLOYEE');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);

  const close = () => {
    setEmail('');
    setDisplayName('');
    setTitle('');
    setRole('EMPLOYEE');
    setPassword('');
    setError(null);
    onClose();
  };

  const submit = async () => {
    setError(null);
    try {
      await create.mutateAsync({
        email,
        displayName,
        title: title || undefined,
        role,
        initialPassword: isLdap ? undefined : password,
      });
      onCreated(isLdap
        ? `${displayName} eklendi. Active Directory parolasıyla giriş yapabilir.`
        : `${displayName} eklendi. Başlangıç parolasını kendisine iletin.`);
      close();
    } catch (err) {
      setError(problemMessage(err));
    }
  };

  return (
    <Dialog open={open} onClose={close} maxWidth="xs" fullWidth>
      <DialogTitle>Kullanıcı ekle</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          {!isLdap && (
            <Alert severity="info" sx={{ fontSize: 13 }}>
              Belirlediğiniz parolayı kişiye iletin. İlk girişinde kendi parolasını belirlemesi
              istenecek, böylece parolayı siz bilmemeye devam edersiniz.
            </Alert>
          )}

          {error && <Alert severity="error">{error}</Alert>}

          <TextField
            label="E-posta"
            size="small"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
          <TextField
            label="Ad soyad"
            size="small"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            required
          />
          <TextField
            label="Ünvan"
            size="small"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
          />
          <TextField select label="Rol" size="small" value={role} onChange={(e) => setRole(e.target.value)}>
            <MenuItem value="EMPLOYEE">Çalışan — yalnızca kendi ticket'ları</MenuItem>
            <MenuItem value="MANAGER">Yönetici — tüm ticket'lar, atama, hatırlatma</MenuItem>
            <MenuItem value="ADMIN">Sistem yöneticisi — tüm yetkiler</MenuItem>
          </TextField>
          {!isLdap ? (
            <TextField
              label="Başlangıç parolası"
              size="small"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              error={password.length > 0 && hasPasswordError(validatePassword(password))}
              helperText={passwordHelperText(password)}
              required
            />
          ) : (
            <Alert severity="info" sx={{ fontSize: 13 }}>
              Active Directory kullanıcısı. Parola AD tarafından yönetilir;
              bu panelden parola belirlenmez.
            </Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={close}>Vazgeç</Button>
        <Button
          variant="contained"
          onClick={() => void submit()}
          disabled={!email || !displayName || (!isLdap && hasPasswordError(validatePassword(password))) || create.isPending}
        >
          {create.isPending ? 'Ekleniyor…' : 'Ekle'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function ResetPasswordDialog({
  user,
  onClose,
  onReset,
}: {
  user: ManagedUserDto | null;
  onClose: () => void;
  onReset: (message: string) => void;
}) {
  const reset = useResetUserPassword();
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);

  const close = () => {
    setPassword('');
    setError(null);
    onClose();
  };

  const submit = async () => {
    if (!user) return;
    setError(null);
    try {
      await reset.mutateAsync({ userId: user.id, newPassword: password });
      onReset(`${user.displayName} için parola sıfırlandı; açık oturumları kapatıldı.`);
      close();
    } catch (err) {
      setError(problemMessage(err));
    }
  };

  return (
    <Dialog open={Boolean(user)} onClose={close} maxWidth="xs" fullWidth>
      <DialogTitle>Parola sıfırla</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          <Typography variant="body2">
            {user?.displayName} ({user?.email})
          </Typography>

          <Alert severity="warning" sx={{ fontSize: 13 }}>
            Kullanıcının açık oturumları kapatılır ve ilk girişinde parolayı değiştirmesi istenir.
          </Alert>

          {error && <Alert severity="error">{error}</Alert>}

          <TextField
            label="Yeni parola"
            size="small"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={password.length > 0 && hasPasswordError(validatePassword(password))}
            helperText={passwordHelperText(password)}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={close}>Vazgeç</Button>
        <Button variant="contained" onClick={() => void submit()} disabled={hasPasswordError(validatePassword(password)) || reset.isPending}>
          {reset.isPending ? 'Sıfırlanıyor…' : 'Sıfırla'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// --- Posta kutuları -----------------------------------------------------------------------------

function MailboxesTab() {
  const { data: status, isLoading, isError, error, refetch } = useGmailStatus();
  const { data: syncStates } = useGmailSyncState();
  const addMailbox = useAddMailbox();
  const removeMailbox = useRemoveMailbox();
  const rescanMailbox = useRescanMailbox();
  const authorize = useAuthorizeMailbox();
  const runIngestion = useRunIngestion();

  const [newMailbox, setNewMailbox] = useState('');
  const [feedback, setFeedback] = useState<Feedback>(null);

  const act = async (fn: () => Promise<unknown>, success: string) => {
    setFeedback(null);
    try {
      await fn();
      setFeedback({ type: 'success', message: success });
    } catch (err) {
      setFeedback({ type: 'error', message: problemMessage(err) });
    }
  };

  const stateFor = (mailbox: string) =>
    syncStates?.find((s) => s.mailboxAddress.toLowerCase() === mailbox.toLowerCase());

  return (
    <Stack spacing={2}>
      {isLoading && <LoadingSkeleton rows={4} />}
      {isError && <ErrorState error={error} onRetry={() => void refetch()} />}

      {status && (
        <>
          <Alert severity={status.credentialsValid ? 'info' : 'warning'} variant="outlined">
            <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
              Sıradaki adım
            </Typography>
            <Typography variant="body2">{status.nextStep}</Typography>
            {status.problem && (
              <Typography variant="body2" color="error.main" sx={{ mt: 0.5 }}>
                {status.problem}
              </Typography>
            )}
          </Alert>

          {status.provider !== 'Google' && (
            <Alert severity="warning">
              Gmail sağlayıcısı <strong>{status.provider}</strong> — örnek mail dosyaları okunuyor,
              gerçek Gmail'e bağlanılmıyor.
            </Alert>
          )}
        </>
      )}

      {feedback && (
        <Alert severity={feedback.type} onClose={() => setFeedback(null)}>
          {feedback.message}
        </Alert>
      )}

      <Card>
        <CardContent>
          <Typography variant="subtitle2" gutterBottom>
            Okunacak posta kutuları
          </Typography>
          <Typography variant="caption" color="text.secondary" display="block">
            Her kutu ayrı ayrı Google onayı gerektirir. Yetkilendirilmemiş kutu her okumada hata
            verir. Onay penceresi <strong>sunucunun çalıştığı bilgisayarda</strong> açılır.
          </Typography>
          <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 0.5 }}>
            Kutu bağlı görünüyor ama eski mailler gelmiyorsa <strong>Baştan tara</strong> deyin.
            Google onayı korunur, ticket'lar silinmez.
          </Typography>

          <Divider sx={{ my: 2 }} />

          <Stack spacing={1.5}>
            {status?.mailboxes.map((mailbox) => {
              const sync = stateFor(mailbox.mailboxAddress);

              return (
                <Stack
                  key={mailbox.mailboxAddress}
                  direction="row"
                  spacing={1.5}
                  alignItems="center"
                  sx={{ p: 1.5, bgcolor: 'action.hover', borderRadius: 1 }}
                >
                  {mailbox.authorized ? (
                    <Tooltip title="Yetkilendirildi">
                      <CheckCircleIcon color="success" fontSize="small" />
                    </Tooltip>
                  ) : (
                    <Tooltip title="Henüz yetkilendirilmedi">
                      <ErrorOutlineIcon color="warning" fontSize="small" />
                    </Tooltip>
                  )}

                  <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                    <Typography variant="body2" noWrap>
                      {mailbox.mailboxAddress}
                    </Typography>
                    <Typography variant="caption" color={sync?.lastError ? 'error.main' : 'text.secondary'}>
                      {sync?.lastError
                        ? `Son hata: ${sync.lastError}`
                        : sync?.lastSyncCompletedAtUtc
                          ? `Son okuma ${formatDateTime(sync.lastSyncCompletedAtUtc)} · ${sync.ticketsCreated} ticket`
                          : 'Henüz okunmadı'}
                    </Typography>
                  </Box>

                  {mailbox.authorized && (
                    <Tooltip title="Okuma penceresini sıfırlar; sonraki okuma bu kutuyu baştan tarar. Ticket'lar silinmez.">
                      <span>
                        <Button
                          size="small"
                          startIcon={<RestartAltIcon />}
                          disabled={rescanMailbox.isPending || runIngestion.isPending}
                          onClick={() =>
                            void act(async () => {
                              await rescanMailbox.mutateAsync({ mailbox: mailbox.mailboxAddress });
                              const r = await runIngestion.mutateAsync();
                              setFeedback({
                                type: r.ticketsCreated > 0 ? 'success' : 'info',
                                message:
                                  `${mailbox.mailboxAddress} baştan tarandı: ${r.messagesSeen} mail okundu, ` +
                                  `${r.ticketsCreated} yeni ticket, ${r.duplicatesSkipped} tekrar atlandı.`,
                              });
                            }, 'Tarama tamamlandı.')
                          }
                        >
                          {rescanMailbox.isPending || runIngestion.isPending ? 'Taranıyor…' : 'Baştan tara'}
                        </Button>
                      </span>
                    </Tooltip>
                  )}

                  {!mailbox.authorized && status?.readyToAuthorize && (
                    <Button
                      size="small"
                      variant="contained"
                      disabled={authorize.isPending}
                      onClick={() =>
                        void act(
                          () => authorize.mutateAsync({ mailbox: mailbox.mailboxAddress }),
                          `${mailbox.mailboxAddress} yetkilendirildi.`,
                        )
                      }
                    >
                      {authorize.isPending ? 'Bekleniyor…' : 'Yetkilendir'}
                    </Button>
                  )}

                  <Tooltip title="Listeden çıkar. Geçmiş ticket'lar silinmez; kutuyu tekrar eklerseniz okuma baştan başlar.">
                    <span>
                      <IconButton
                        size="small"
                        disabled={removeMailbox.isPending}
                        onClick={() =>
                          void act(
                            () => removeMailbox.mutateAsync({ mailbox: mailbox.mailboxAddress }),
                            `${mailbox.mailboxAddress} listeden çıkarıldı.`,
                          )
                        }
                      >
                        <DeleteOutlineIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                </Stack>
              );
            })}

            {status?.mailboxes.length === 0 && (
              <Typography variant="body2" color="text.secondary">
                Henüz posta kutusu eklenmedi.
              </Typography>
            )}
          </Stack>

          <Divider sx={{ my: 2 }} />

          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
            <TextField
              label="Yeni posta kutusu"
              placeholder="ornek@menarini.com.tr"
              size="small"
              value={newMailbox}
              onChange={(e) => setNewMailbox(e.target.value)}
              sx={{ flexGrow: 1 }}
            />
            <Button
              variant="outlined"
              disabled={!newMailbox.includes('@') || addMailbox.isPending}
              onClick={() =>
                void act(async () => {
                  await addMailbox.mutateAsync({ mailbox: newMailbox });
                  setNewMailbox('');
                }, `${newMailbox} eklendi. Şimdi 'Yetkilendir' ile Google onayı verin.`)
              }
            >
              Ekle
            </Button>
          </Stack>
        </CardContent>
      </Card>

      <Box>
        <Button
          variant="contained"
          disabled={runIngestion.isPending}
          onClick={() =>
            void act(async () => {
              const result = await runIngestion.mutateAsync();
              setFeedback({
                type: 'success',
                message:
                  `${result.messagesSeen} mail okundu, ${result.ticketsCreated} yeni ticket, ` +
                  `${result.duplicatesSkipped} tekrar atlandı.`,
              });
            }, 'Okuma tamamlandı.')
          }
        >
          {runIngestion.isPending ? 'Okunuyor…' : 'Mailleri şimdi oku'}
        </Button>
      </Box>
    </Stack>
  );
}

// --- Ayarlar ------------------------------------------------------------------------------------

/** Son kullanıcıya gösterilecek ayarlar. Diğerleri teknik olduğu için gizlenir. */
const EDITABLE_SETTINGS = [
  'Aging.StaleAfterDays',
  'Aging.OldAfterDays',
  'Aging.CriticalAfterDays',
  'Gmail.PollIntervalMinutes',
  'Schedule.RequiredOfficeDays',
  'Schedule.RequiredHomeOfficeDays',
];

function SettingsTab() {
  const { data: settings, isLoading, isError, error, refetch } = useAppSettings();
  const update = useUpdateAppSettings();

  const [draft, setDraft] = useState<Record<string, string>>({});
  const [feedback, setFeedback] = useState<Feedback>(null);

  // Sunucudan gelen değerler forma yalnızca ilk yüklemede ve kaydetme sonrasında yansır.
  useEffect(() => {
    if (!settings) return;
    setDraft(Object.fromEntries(settings.map((s) => [s.key, s.value])));
  }, [settings]);

  const visible = (settings ?? []).filter((s) => EDITABLE_SETTINGS.includes(s.key));
  const changed = visible.some((s) => draft[s.key] !== s.value);

  const save = async () => {
    setFeedback(null);
    try {
      const changes = Object.fromEntries(
        visible.filter((s) => draft[s.key] !== s.value).map((s) => [s.key, draft[s.key]]),
      );
      await update.mutateAsync(changes);
      setFeedback({ type: 'success', message: 'Ayarlar kaydedildi.' });
    } catch (err) {
      setFeedback({ type: 'error', message: problemMessage(err) });
    }
  };

  return (
    <Stack spacing={2}>
      <Alert severity="info" variant="outlined">
        Bu eşikler <strong>SLA değildir</strong>. Tixbox'ta hedef tarih verisi olmadığı için panel
        yalnızca "kaç gündür açık / kaç gündür güncellenmedi" bilgisini gösterir.
      </Alert>

      {feedback && (
        <Alert severity={feedback.type} onClose={() => setFeedback(null)}>
          {feedback.message}
        </Alert>
      )}

      {isLoading && <LoadingSkeleton rows={5} />}
      {isError && <ErrorState error={error} onRetry={() => void refetch()} />}

      {settings && (
        <Card>
          <CardContent>
            <Stack spacing={2.5}>
              {visible.map((setting) => (
                <TextField
                  key={setting.key}
                  label={settingLabels[setting.key] ?? setting.key}
                  size="small"
                  type={setting.dataType === 'int' ? 'number' : 'text'}
                  value={draft[setting.key] ?? ''}
                  onChange={(e) => setDraft((prev) => ({ ...prev, [setting.key]: e.target.value }))}
                  helperText={setting.description}
                />
              ))}

              <Box>
                <Button variant="contained" disabled={!changed || update.isPending} onClick={() => void save()}>
                  {update.isPending ? 'Kaydediliyor…' : 'Kaydet'}
                </Button>
              </Box>
            </Stack>
          </CardContent>
        </Card>
      )}
    </Stack>
  );
}

const settingLabels: Record<string, string> = {
  'Aging.StaleAfterDays': 'Kaç gün güncellenmezse "Güncelleme bekliyor"',
  'Aging.OldAfterDays': 'Kaç gün açık kalırsa "Uzun süredir açık"',
  'Aging.CriticalAfterDays': 'Kaç gün açık kalırsa kritik',
  'Gmail.PollIntervalMinutes': 'Mail okuma sıklığı (dakika)',
  'Schedule.RequiredOfficeDays': 'Haftalık asgari ofis günü',
  'Schedule.RequiredHomeOfficeDays': 'Haftalık azami home office günü',
};
