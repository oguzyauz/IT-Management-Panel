import { Box, Button, Card, CardContent, Stack, Typography } from '@mui/material';
import BlockIcon from '@mui/icons-material/Block';
import ReportProblemOutlinedIcon from '@mui/icons-material/ReportProblemOutlined';
import { useNavigate, useRouteError } from 'react-router-dom';
import type { ReactNode } from 'react';

function StatusLayout({
  icon,
  title,
  description,
  action,
}: {
  icon: ReactNode;
  title: string;
  description: string;
  action?: ReactNode;
}) {
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
      <Card sx={{ maxWidth: 480, width: '100%' }}>
        <CardContent sx={{ p: 4, textAlign: 'center' }}>
          <Stack spacing={2} alignItems="center">
            <Box sx={{ color: 'text.disabled' }}>{icon}</Box>
            <Typography variant="h2">{title}</Typography>
            <Typography variant="body2" color="text.secondary">
              {description}
            </Typography>
            {action}
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}

export function UnauthorizedPage() {
  const navigate = useNavigate();

  return (
    <StatusLayout
      icon={<BlockIcon sx={{ fontSize: 56 }} />}
      title="Yetkiniz yok"
      description="Bu sayfayı görüntülemek için gerekli role sahip değilsiniz. Erişim gerekiyorsa IT yöneticinizle iletişime geçin."
      action={
        <Button variant="contained" onClick={() => navigate('/login', { replace: true })}>
          Giriş ekranına dön
        </Button>
      }
    />
  );
}

export function ErrorPage() {
  const error = useRouteError();
  const navigate = useNavigate();
  const message = error instanceof Error ? error.message : 'Beklenmeyen bir hata oluştu.';

  return (
    <StatusLayout
      icon={<ReportProblemOutlinedIcon sx={{ fontSize: 56 }} />}
      title="Bir şeyler ters gitti"
      description={message}
      action={
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" onClick={() => window.location.reload()}>
            Sayfayı yenile
          </Button>
          <Button variant="contained" onClick={() => navigate('/', { replace: true })}>
            Ana sayfa
          </Button>
        </Stack>
      }
    />
  );
}

export function NotFoundPage() {
  const navigate = useNavigate();

  return (
    <StatusLayout
      icon={<ReportProblemOutlinedIcon sx={{ fontSize: 56 }} />}
      title="Sayfa bulunamadı"
      description="Aradığınız sayfa taşınmış veya hiç var olmamış olabilir."
      action={
        <Button variant="contained" onClick={() => navigate('/', { replace: true })}>
          Ana sayfa
        </Button>
      }
    />
  );
}
