import { Chip, Tooltip } from '@mui/material';
import type { ChipProps } from '@mui/material';
import type { ReactElement } from 'react';
import HomeWorkOutlinedIcon from '@mui/icons-material/HomeWorkOutlined';
import BusinessOutlinedIcon from '@mui/icons-material/BusinessOutlined';
import BeachAccessOutlinedIcon from '@mui/icons-material/BeachAccessOutlined';
import type { AgingLevel, TicketStatus, WorkMode } from '../api/types';
import { agingLabels, ticketStatusLabels, workModeLabels } from '../labels';

const statusColors: Record<TicketStatus, ChipProps['color']> = {
  New: 'default',
  Unassigned: 'warning',
  Assigned: 'info',
  InProgress: 'primary',
  Completed: 'success',
  Archived: 'default',
};

export function TicketStatusBadge({ status, size = 'small' }: { status: TicketStatus; size?: 'small' | 'medium' }) {
  return <Chip label={ticketStatusLabels[status]} color={statusColors[status]} size={size} variant="filled" />;
}

const agingColors: Record<AgingLevel, ChipProps['color']> = {
  Normal: 'default',
  NeedsUpdate: 'info',
  LongOpen: 'warning',
  LongOpenCritical: 'error',
};

export function AgingBadge({ aging, daysOpen }: { aging: AgingLevel; daysOpen?: number }) {
  if (aging === 'Normal') return null;

  return (
    <Tooltip title={daysOpen !== undefined ? `${daysOpen} gündür açık` : ''}>
      <Chip label={agingLabels[aging]} color={agingColors[aging]} size="small" variant="outlined" />
    </Tooltip>
  );
}

const workModeIcons: Record<WorkMode, ReactElement> = {
  Office: <BusinessOutlinedIcon fontSize="small" />,
  HomeOffice: <HomeWorkOutlinedIcon fontSize="small" />,
  Leave: <BeachAccessOutlinedIcon fontSize="small" />,
};

const workModeColors: Record<WorkMode, ChipProps['color']> = {
  Office: 'primary',
  HomeOffice: 'secondary',
  Leave: 'default',
};

export function WorkModeBadge({
  mode,
  isOverride = false,
  size = 'small',
}: {
  mode: WorkMode | null | undefined;
  isOverride?: boolean;
  size?: 'small' | 'medium';
}) {
  if (!mode) {
    return <Chip label="—" size={size} variant="outlined" sx={{ color: 'text.disabled' }} />;
  }

  const chip = (
    <Chip
      icon={workModeIcons[mode]}
      label={workModeLabels[mode]}
      color={workModeColors[mode]}
      size={size}
      variant={isOverride ? 'outlined' : 'filled'}
    />
  );

  return isOverride ? <Tooltip title="Yönetici tarafından değiştirildi">{chip}</Tooltip> : chip;
}

export function PriorityBadge({ priority }: { priority: number }) {
  const color: ChipProps['color'] = priority <= 1 ? 'error' : priority === 2 ? 'warning' : 'default';
  return <Chip label={`P${priority}`} color={color} size="small" variant="outlined" />;
}
