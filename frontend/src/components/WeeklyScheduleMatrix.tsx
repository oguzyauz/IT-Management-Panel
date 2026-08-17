import {
  Chip,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import type { WeeklyScheduleMatrixDto, WeeklyScheduleRowDto } from '../api/types';
import { WorkModeBadge } from './Badges';
import { formatDayShort, scheduleStatusLabels } from '../labels';
import { EmptyState } from './States';
import type { ReactNode } from 'react';

interface Props {
  matrix: WeeklyScheduleMatrixDto;
  onCellClick?: (row: WeeklyScheduleRowDto, date: string) => void;
  renderRowActions?: (row: WeeklyScheduleRowDto) => ReactNode;
}

export function WeeklyScheduleMatrix({ matrix, onCellClick, renderRowActions }: Props) {
  if (matrix.rows.length === 0) {
    return <EmptyState title="Ekipte aktif çalışan bulunmuyor" />;
  }

  return (
    <TableContainer sx={{ overflowX: 'auto' }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell sx={{ minWidth: 180 }}>Çalışan</TableCell>
            {matrix.days.map((day) => (
              <TableCell key={day} align="center" sx={{ minWidth: 120 }}>
                {formatDayShort(day)}
              </TableCell>
            ))}
            <TableCell align="center" sx={{ minWidth: 120 }}>
              Plan durumu
            </TableCell>
            {renderRowActions && <TableCell align="right" sx={{ minWidth: 160 }} />}
          </TableRow>
        </TableHead>
        <TableBody>
          {matrix.rows.map((row) => (
            <TableRow key={row.userId} hover>
              <TableCell>
                <Stack direction="row" spacing={0.5} alignItems="center">
                  <Typography variant="body2" fontWeight={600}>
                    {row.displayName}
                  </Typography>
                  {row.hasRuleViolation && (
                    <Tooltip title={row.ruleViolationNote ?? 'Kural ihlali'}>
                      <WarningAmberIcon fontSize="small" color="warning" />
                    </Tooltip>
                  )}
                </Stack>
              </TableCell>

              {row.cells.map((cell) => (
                <TableCell
                  key={cell.date}
                  align="center"
                  onClick={() => onCellClick?.(row, cell.date)}
                  sx={{
                    cursor: onCellClick ? 'pointer' : 'default',
                    bgcolor: cell.isHoliday ? 'action.hover' : undefined,
                  }}
                >
                  {cell.isHoliday ? (
                    <Tooltip title={cell.holidayName ?? 'Resmî tatil'}>
                      <Chip label="Tatil" size="small" variant="outlined" />
                    </Tooltip>
                  ) : (
                    <WorkModeBadge mode={cell.mode} isOverride={cell.isManagerOverride} />
                  )}
                </TableCell>
              ))}

              <TableCell align="center">
                <Chip
                  label={scheduleStatusLabels[row.status]}
                  size="small"
                  color={
                    row.status === 'Approved'
                      ? 'success'
                      : row.status === 'Rejected'
                        ? 'error'
                        : row.status === 'Submitted'
                          ? 'info'
                          : 'default'
                  }
                />
              </TableCell>

              {renderRowActions && <TableCell align="right">{renderRowActions(row)}</TableCell>}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
