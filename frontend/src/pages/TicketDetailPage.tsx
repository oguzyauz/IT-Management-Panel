import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Card, CardContent, Stack, Typography } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { useTicket, useUsers } from '../api/hooks';
import { TicketDetailBody } from '../components/TicketDetailDrawer';
import { ErrorState, LoadingSkeleton } from '../components/States';
import { TIXBOX_DISCLAIMER } from '../labels';

export function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: ticket, isLoading, isError, error, refetch } = useTicket(id);
  const { data: users } = useUsers();

  return (
    <Stack spacing={2.5}>
      <Stack direction="row" spacing={1} alignItems="center">
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate('/manager/tickets')}>
          Ticket listesi
        </Button>
      </Stack>

      <Alert severity="info" variant="outlined">
        {TIXBOX_DISCLAIMER}
      </Alert>

      <Card>
        <CardContent>
          {isLoading && <LoadingSkeleton rows={8} />}
          {isError && <ErrorState error={error} onRetry={() => void refetch()} />}
          {ticket && (
            <Box sx={{ maxWidth: 760 }}>
              <TicketDetailBody ticket={ticket} users={users ?? []} />
            </Box>
          )}
          {!isLoading && !isError && !ticket && <Typography>Ticket bulunamadı.</Typography>}
        </CardContent>
      </Card>
    </Stack>
  );
}
