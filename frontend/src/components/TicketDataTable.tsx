import {
  Box,
  Checkbox,
  IconButton,
  Link,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TableSortLabel,
  Tooltip,
  Typography,
} from '@mui/material';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import type { TicketListItemDto } from '../api/types';
import { AgingBadge, PriorityBadge, TicketStatusBadge } from './Badges';
import { formatDateTime, ticketTypeLabels } from '../labels';
import { EmptyState } from './States';

interface Column {
  id: string;
  label: string;
  sortable?: boolean;
  align?: 'left' | 'right' | 'center';
}

const columns: Column[] = [
  { id: 'ExternalTicketNumber', label: 'Ticket no' },
  { id: 'RequesterName', label: 'Talep eden' },
  { id: 'ApplicationName', label: 'Uygulama' },
  { id: 'SourceMailbox', label: 'Okunduğu kutu' },
  { id: 'Priority', label: 'Öncelik', sortable: true, align: 'center' },
  { id: 'Status', label: 'Durum', sortable: true },
  { id: 'Assignee', label: 'Sorumlu' },
  { id: 'OriginalSentAtUtc', label: 'Açılış', sortable: true },
  { id: 'UpdatedAtUtc', label: 'Son güncelleme', sortable: true },
  { id: 'Aging', label: 'Yaş durumu' },
  { id: 'actions', label: '', align: 'right' },
];

/**
 * Ticket'ın hangi posta kutusundan okunduğunu gösterir. Tabloyu şişirmemek için
 * yalnızca adresin @ öncesi yazılır; tam adres ipucunda görünür.
 */
function SourceMailboxCell({ mailboxes, manual }: { mailboxes: string[]; manual: boolean }) {
  if (mailboxes.length === 0) {
    return (
      <Typography variant="caption" color="text.secondary">
        {manual ? 'Elle eklendi' : '—'}
      </Typography>
    );
  }

  return (
    <Tooltip title={mailboxes.join(', ')}>
      <Stack spacing={0.25}>
        {mailboxes.map((mailbox) => (
          <Typography key={mailbox} variant="caption" noWrap>
            {mailbox.split('@')[0]}
          </Typography>
        ))}
      </Stack>
    </Tooltip>
  );
}

interface TicketDataTableProps {
  tickets: TicketListItemDto[];
  totalCount?: number;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDescending?: boolean;
  selectable?: boolean;
  selectedIds?: string[];
  onSelectionChange?: (ids: string[]) => void;
  onRowClick?: (ticket: TicketListItemDto) => void;
  onSortChange?: (column: string) => void;
  onPageChange?: (page: number) => void;
  onPageSizeChange?: (pageSize: number) => void;
  emptyTitle?: string;
  emptyDescription?: string;
  dense?: boolean;
}

export function TicketDataTable({
  tickets,
  totalCount,
  page = 1,
  pageSize = 25,
  sortBy,
  sortDescending = true,
  selectable = false,
  selectedIds = [],
  onSelectionChange,
  onRowClick,
  onSortChange,
  onPageChange,
  onPageSizeChange,
  emptyTitle = 'Ticket bulunamadı',
  emptyDescription,
  dense = false,
}: TicketDataTableProps) {
  if (tickets.length === 0) {
    return <EmptyState title={emptyTitle} description={emptyDescription} />;
  }

  const allSelected = selectable && tickets.every((t) => selectedIds.includes(t.id));

  const toggleAll = () => {
    if (!onSelectionChange) return;
    onSelectionChange(allSelected ? [] : tickets.map((t) => t.id));
  };

  const toggleOne = (id: string) => {
    if (!onSelectionChange) return;
    onSelectionChange(
      selectedIds.includes(id) ? selectedIds.filter((x) => x !== id) : [...selectedIds, id],
    );
  };

  return (
    <Box>
      <TableContainer sx={{ overflowX: 'auto' }}>
        <Table size={dense ? 'small' : 'medium'}>
          <TableHead>
            <TableRow>
              {selectable && (
                <TableCell padding="checkbox">
                  <Checkbox
                    checked={allSelected}
                    indeterminate={!allSelected && selectedIds.length > 0}
                    onChange={toggleAll}
                    inputProps={{ 'aria-label': 'Tümünü seç' }}
                  />
                </TableCell>
              )}
              {columns.map((column) => (
                <TableCell key={column.id} align={column.align ?? 'left'}>
                  {column.sortable && onSortChange ? (
                    <TableSortLabel
                      active={sortBy === column.id}
                      direction={sortBy === column.id && sortDescending ? 'desc' : 'asc'}
                      onClick={() => onSortChange(column.id)}
                    >
                      {column.label}
                    </TableSortLabel>
                  ) : (
                    column.label
                  )}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {tickets.map((ticket) => (
              <TableRow
                key={ticket.id}
                hover
                sx={{ cursor: onRowClick ? 'pointer' : 'default' }}
                onClick={() => onRowClick?.(ticket)}
              >
                {selectable && (
                  <TableCell padding="checkbox" onClick={(e) => e.stopPropagation()}>
                    <Checkbox
                      checked={selectedIds.includes(ticket.id)}
                      onChange={() => toggleOne(ticket.id)}
                      inputProps={{ 'aria-label': `${ticket.externalTicketNumber} seç` }}
                    />
                  </TableCell>
                )}

                <TableCell>
                  <Stack direction="row" spacing={0.75} alignItems="center">
                    <Typography variant="body2" fontWeight={600} fontFamily="monospace">
                      {ticket.externalTicketNumber}
                    </Typography>
                    {ticket.hasParseWarning && (
                      <Tooltip title="Veri uyumsuzluğu uyarısı var">
                        <WarningAmberIcon fontSize="small" color="error" />
                      </Tooltip>
                    )}
                  </Stack>
                  <Typography variant="caption" color="text.secondary">
                    {ticketTypeLabels[ticket.ticketType]}
                    {ticket.createdManually && ' · elle eklendi'}
                  </Typography>
                </TableCell>

                <TableCell>{ticket.requesterName}</TableCell>
                <TableCell>{ticket.applicationName}</TableCell>
                <TableCell>
                  <SourceMailboxCell mailboxes={ticket.sourceMailboxes} manual={ticket.createdManually} />
                </TableCell>
                <TableCell align="center">
                  <PriorityBadge priority={ticket.priority} />
                </TableCell>
                <TableCell>
                  <TicketStatusBadge status={ticket.status} />
                </TableCell>
                <TableCell>
                  {ticket.assigneeName ? (
                    <Stack spacing={0.25}>
                      <Typography variant="body2">{ticket.assigneeName}</Typography>
                      {ticket.autoAssigned && (
                        <Tooltip title="Ticket maili doğrudan bu kişiye gelmiş; sistem otomatik atadı">
                          <Typography variant="caption" color="text.secondary">
                            otomatik atandı
                          </Typography>
                        </Tooltip>
                      )}
                    </Stack>
                  ) : (
                    <Typography variant="body2" color="warning.main" fontWeight={600}>
                      Atanmamış
                    </Typography>
                  )}
                </TableCell>
                <TableCell>
                  <Typography variant="body2">{formatDateTime(ticket.originalSentAtUtc)}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {ticket.daysOpen} gün önce
                  </Typography>
                </TableCell>
                <TableCell>
                  <Typography variant="body2">{formatDateTime(ticket.updatedAtUtc)}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {ticket.daysSinceUpdate} gün önce
                  </Typography>
                </TableCell>
                <TableCell>
                  <AgingBadge aging={ticket.aging} daysOpen={ticket.daysOpen} />
                </TableCell>
                <TableCell align="right" onClick={(e) => e.stopPropagation()}>
                  {ticket.externalUrl && (
                    <Tooltip title="Tixbox'ta aç (salt görüntüleme)">
                      <IconButton
                        size="small"
                        component={Link}
                        href={ticket.externalUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                      >
                        <OpenInNewIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {totalCount !== undefined && onPageChange && (
        <TablePagination
          component="div"
          count={totalCount}
          page={page - 1}
          rowsPerPage={pageSize}
          onPageChange={(_, newPage) => onPageChange(newPage + 1)}
          onRowsPerPageChange={(e) => onPageSizeChange?.(Number(e.target.value))}
          rowsPerPageOptions={[10, 25, 50, 100]}
          labelRowsPerPage="Sayfa başına"
          labelDisplayedRows={({ from, to, count }) => `${from}-${to} / ${count}`}
        />
      )}
    </Box>
  );
}
