import { Box, Button, CircularProgress, Stack, Typography } from '@mui/material';
import { useDashboard } from '../api/hooks';
import { useAuth } from '../auth/AuthContext';

/**
 * Masaüstünde duran özet kutusu.
 *
 * Bilinçli olarak sade: yönetici buna bakıp "bugün bir şey yapmam gerekiyor mu?" sorusunu
 * bir saniyede cevaplamalı. Detay burada gösterilmez — tıklayınca tam panel açılır.
 */
export function WidgetPage() {
  const { isAuthenticated, isLoading: authLoading, isManager } = useAuth();

  // Sorgu yalnızca yönetici oturumunda çalışır; aksi hâlde çalışan kutuyu açtığında
  // yetkisi olmayan bir uca istek gidip 403 hatası görünüyordu.
  const { data, isLoading } = useDashboard(60_000, isAuthenticated && isManager);

  const open = (path: string) => window.open(path, '_blank', 'noopener');

  if (authLoading || isLoading) return <Frame><Spinner /></Frame>;

  if (!isAuthenticated) {
    return (
      <Frame>
        <Center>
          <Button variant="contained" onClick={() => open('/login')}>Giriş yap</Button>
        </Center>
      </Frame>
    );
  }

  if (!isManager) {
    return (
      <Frame>
        <Center>
          <Button variant="outlined" onClick={() => open('/employee/my-tickets')}>
            Ticket'larımı aç
          </Button>
        </Center>
      </Frame>
    );
  }

  if (!data) {
    return (
      <Frame>
        <Center>
          <Typography variant="body2" color="text.secondary">Bağlanılamadı</Typography>
        </Center>
      </Frame>
    );
  }

  const { unassignedTickets, staleTickets, inOfficeToday, homeOfficeToday, onLeaveToday } = data.metrics;

  return (
    <Frame onClick={() => open('/manager/dashboard')}>
      <Stack spacing={2.5} sx={{ height: '100%', justifyContent: 'center' }}>
        <Big
          value={unassignedTickets}
          label="atanmamış ticket"
          tone={unassignedTickets > 0 ? 'warning.main' : 'text.disabled'}
          onClick={() => open('/manager/tickets?unassigned=true')}
        />

        <Big
          value={staleTickets}
          label="uzun süredir açık"
          tone={staleTickets > 0 ? 'error.main' : 'text.disabled'}
          onClick={() => open('/manager/tickets')}
        />

        <Box sx={{ textAlign: 'center' }}>
          <Typography variant="body2" color="text.secondary">
            Bugün{' '}
            <Box component="span" sx={{ color: 'text.primary', fontWeight: 600 }}>
              {inOfficeToday}
            </Box>{' '}
            ofiste ·{' '}
            <Box component="span" sx={{ color: 'text.primary', fontWeight: 600 }}>
              {homeOfficeToday}
            </Box>{' '}
            evde
            {onLeaveToday > 0 ? ` · ${onLeaveToday} izinli` : ''}
          </Typography>
        </Box>
      </Stack>
    </Frame>
  );
}

function Frame({ children, onClick }: { children: React.ReactNode; onClick?: () => void }) {
  return (
    <Box
      onClick={onClick}
      sx={{
        height: '100vh',
        p: 2,
        bgcolor: 'background.paper',
        display: 'flex',
        flexDirection: 'column',
        cursor: onClick ? 'pointer' : 'default',
      }}
    >
      <Typography
        variant="caption"
        color="text.secondary"
        sx={{ textAlign: 'center', letterSpacing: 1, textTransform: 'uppercase', fontSize: 10 }}
      >
        IT Paneli
      </Typography>
      <Box sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
        {children}
      </Box>
    </Box>
  );
}

function Big({
  value,
  label,
  tone,
  onClick,
}: {
  value: number;
  label: string;
  tone: string;
  onClick: () => void;
}) {
  return (
    <Box
      onClick={(e) => {
        e.stopPropagation();
        onClick();
      }}
      sx={{
        textAlign: 'center',
        cursor: 'pointer',
        borderRadius: 2,
        py: 1,
        '&:hover': { bgcolor: 'action.hover' },
      }}
    >
      <Typography sx={{ fontSize: 56, fontWeight: 700, lineHeight: 1, color: tone }}>
        {value}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
    </Box>
  );
}

function Center({ children }: { children: React.ReactNode }) {
  return <Stack alignItems="center" justifyContent="center" sx={{ height: '100%' }}>{children}</Stack>;
}

function Spinner() {
  return (
    <Center>
      <CircularProgress size={24} />
    </Center>
  );
}
