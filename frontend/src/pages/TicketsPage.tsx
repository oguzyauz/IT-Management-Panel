import { useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  IconButton,
  InputAdornment,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import AddIcon from '@mui/icons-material/Add';
import { CreateTicketDialog } from '../components/CreateTicketDialog';
import { useTicket, useTickets, useUsers } from '../api/hooks';
import type { TicketStatus } from '../api/types';
import { TicketDataTable } from '../components/TicketDataTable';
import { TicketDetailDrawer } from '../components/TicketDetailDrawer';
import { ErrorState, LoadingSkeleton } from '../components/States';
import { TIXBOX_DISCLAIMER, ticketStatusLabels } from '../labels';

const statusOptions: TicketStatus[] = [
  'Unassigned',
  'Assigned',
  'InProgress',
  'Completed',
  'Archived',
];

export function TicketsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<TicketStatus | ''>('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [sortBy, setSortBy] = useState('OriginalSentAtUtc');
  const [sortDescending, setSortDescending] = useState(true);
  const [selectedTicketId, setSelectedTicketId] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  const unassignedOnly = searchParams.get('unassigned') === 'true';
  const assigneeUserId = searchParams.get('assigneeUserId') ?? undefined;

  const filters = useMemo(
    () => ({
      search: search || undefined,
      status: status ? [status] : undefined,
      unassigned: unassignedOnly || undefined,
      assigneeUserId,
      page,
      pageSize,
      sortBy,
      sortDescending,
    }),
    [search, status, unassignedOnly, assigneeUserId, page, pageSize, sortBy, sortDescending],
  );

  const { data, isLoading, isError, error, refetch } = useTickets(filters);
  const { data: users } = useUsers();
  const { data: ticketDetail, isLoading: isDetailLoading } = useTicket(selectedTicketId ?? undefined);

  const handleSort = (column: string) => {
    if (sortBy === column) {
      setSortDescending((prev) => !prev);
    } else {
      setSortBy(column);
      setSortDescending(true);
    }
  };

  return (
    <Stack spacing={2.5}>
      <Stack direction="row" justifyContent="space-between" alignItems="flex-start" flexWrap="wrap" useFlexGap>
        <Box>
          <Typography variant="h1">Ticket'lar</Typography>
          <Typography variant="body2" color="text.secondary">
            Gmail'den okunan Service Desk kayıtları
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
          Elle ticket ekle
        </Button>
      </Stack>

      <Alert severity="info" variant="outlined">
        {TIXBOX_DISCLAIMER}
      </Alert>

      <Card>
        <CardContent>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
            <TextField
              placeholder="Ticket no, talep eden, uygulama, açıklama veya posta kutusunda ara…"
              size="small"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              sx={{ flexGrow: 1 }}
              InputProps={{
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" color="action" />
                  </InputAdornment>
                ),
                endAdornment: search ? (
                  <InputAdornment position="end">
                    <IconButton size="small" onClick={() => { setSearch(''); setPage(1); }} aria-label="Aramayı temizle">
                      <ClearIcon fontSize="small" />
                    </IconButton>
                  </InputAdornment>
                ) : null,
              }}
            />
            <TextField
              select
              label="Durum"
              size="small"
              value={status}
              onChange={(e) => {
                setStatus(e.target.value as TicketStatus | '');
                setPage(1);
              }}
              sx={{ minWidth: 200 }}
            >
              <MenuItem value="">Tümü</MenuItem>
              {statusOptions.map((s) => (
                <MenuItem key={s} value={s}>
                  {ticketStatusLabels[s]}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              label="Atama"
              size="small"
              value={unassignedOnly ? 'unassigned' : 'all'}
              onChange={(e) => {
                const next = new URLSearchParams(searchParams);
                if (e.target.value === 'unassigned') next.set('unassigned', 'true');
                else next.delete('unassigned');
                setSearchParams(next);
                setPage(1);
              }}
              sx={{ minWidth: 180 }}
            >
              <MenuItem value="all">Tümü</MenuItem>
              <MenuItem value="unassigned">Yalnızca atanmamış</MenuItem>
            </TextField>
          </Stack>
        </CardContent>
      </Card>

      <Card>
        <CardContent sx={{ p: 0, '&:last-child': { pb: 0 } }}>
          {isLoading && (
            <Box sx={{ p: 2 }}>
              <LoadingSkeleton rows={6} />
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
              totalCount={data.totalCount}
              page={data.page}
              pageSize={data.pageSize}
              sortBy={sortBy}
              sortDescending={sortDescending}
              onSortChange={handleSort}
              onPageChange={setPage}
              onPageSizeChange={(size) => {
                setPageSize(size);
                setPage(1);
              }}
              onRowClick={(t) => setSelectedTicketId(t.id)}
              emptyTitle="Filtreye uyan ticket yok"
              emptyDescription="Filtreleri gevşetin veya dashboard'dan mailleri okutun."
            />
          )}
        </CardContent>
      </Card>

      {/* Satıra tıklayınca çekmece açılır; kalıcı bağlantı için /manager/tickets/:id sayfası kullanılır. */}
      <TicketDetailDrawer
        open={Boolean(selectedTicketId)}
        onClose={() => setSelectedTicketId(null)}
        ticket={ticketDetail}
        isLoading={isDetailLoading}
        users={users ?? []}
        onOpenFullPage={() => selectedTicketId && navigate(`/manager/tickets/${selectedTicketId}`)}
      />

      <CreateTicketDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={(id) => setSelectedTicketId(id)}
      />
    </Stack>
  );
}
