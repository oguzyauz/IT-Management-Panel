import { useCallback, useMemo, useState, useEffect } from 'react';
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    Chip,
    IconButton,
    Stack,
    Tooltip,
    Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import TodayIcon from '@mui/icons-material/Today';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import CancelOutlinedIcon from '@mui/icons-material/CancelOutlined';
import { useLeaveCalendar, useAllLeaves, useLeaveDecision } from '../api/hooks';
import type { LeaveCalendarItemDto, LeaveType } from '../api/types';
import { leaveTypeLabels, leaveTypeColors, leaveStatusLabels, formatDate, toIsoDate } from '../labels';
import { CreateLeaveDialog } from '../components/CreateLeaveDialog';
import { ErrorState, LoadingSkeleton } from '../components/States';
import { useAuth } from '../auth/AuthContext';
import { api, problemMessage } from '../api/client';

// ── Tarih yardımcıları ──────────────────────────────────────────────────────

function getMonthRange(year: number, month: number) {
    const start = new Date(year, month, 1);
    const end = new Date(year, month + 1, 0);
    return {
        startDate: toIsoDate(start),
        endDate: toIsoDate(end),
        daysInMonth: end.getDate(),
        firstDayOfWeek: (start.getDay() + 6) % 7, // 0=Pazartesi
    };
}

const DAY_NAMES = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz'];
const MONTH_NAMES = [
    'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
    'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
];

// ── Sayfa bileşeni ──────────────────────────────────────────────────────────

export function LeavesPage() {
    const now = new Date();
    const [year, setYear] = useState(now.getFullYear());
    const [month, setMonth] = useState(now.getMonth());
    const [dialogOpen, setDialogOpen] = useState(false);
    const [selectedDate, setSelectedDate] = useState<string | undefined>(undefined);
    const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

    // Dinamik Resmi Tatiller için state
    const [holidays, setHolidays] = useState<{ date: string; title: string }[]>([]);

    const { isManager } = useAuth();
    const decision = useLeaveDecision();

    const { startDate, endDate, daysInMonth, firstDayOfWeek } = useMemo(
        () => getMonthRange(year, month),
        [year, month],
    );

    const { data: calendarItems, isLoading, isError, error, refetch } = useLeaveCalendar(startDate, endDate);
    const { data: pendingLeaves } = useAllLeaves(isManager ? { status: 'Pending' } : undefined);

    // API'den dinamik resmi tatilleri çekme fonksiyonu
    useEffect(() => {
        api.get<{ date: string; title: string }[]>(`/publicHolidays/${year}`)
            .then((res) => {
                setHolidays(res.data);
            })
            .catch((err: unknown) => {
                console.error("Resmi tatiller çekilemedi:", err);
            });
    }, [year]);

    const dayHolidays = useMemo(() => {
        const map = new Map<number, string>();
        for (const h of holidays) {
            const [y, m, d] = h.date.split('-');
            if (parseInt(y, 10) === year && parseInt(m, 10) - 1 === month) {
                map.set(parseInt(d, 10), h.title);
            }
        }
        return map;
    }, [holidays, year, month]);

    // Ay navigasyonu
    const goToPrev = () => {
        if (month === 0) { setYear((y) => y - 1); setMonth(11); }
        else setMonth((m) => m - 1);
    };
    const goToNext = () => {
        if (month === 11) { setYear((y) => y + 1); setMonth(0); }
        else setMonth((m) => m + 1);
    };
    const goToToday = () => { setYear(now.getFullYear()); setMonth(now.getMonth()); };

    // Takvim hücreleri için izinleri grupla
    const dayLeaves = useMemo(() => {
        if (!calendarItems) return new Map<number, LeaveCalendarItemDto[]>();
        const map = new Map<number, LeaveCalendarItemDto[]>();
        for (const item of calendarItems) {
            const s = new Date(`${item.startDate}T00:00:00`);
            const e = new Date(`${item.endDate}T00:00:00`);
            const monthStart = new Date(year, month, 1);
            const monthEnd = new Date(year, month + 1, 0);

            const from = s < monthStart ? 1 : s.getDate();
            const to = e > monthEnd ? monthEnd.getDate() : e.getDate();

            for (let d = from; d <= to; d++) {
                if (!map.has(d)) map.set(d, []);
                map.get(d)!.push(item);
            }
        }
        return map;
    }, [calendarItems, year, month]);

    // Hücre tıklaması
    const handleCellClick = useCallback((day: number) => {
        const dateStr = toIsoDate(new Date(year, month, day));
        setSelectedDate(dateStr);
        setDialogOpen(true);
    }, [year, month]);

    // Yönetici onay/red
    const handleDecision = async (leaveId: string, approve: boolean) => {
        setFeedback(null);
        try {
            await decision.mutateAsync({
                leaveId,
                decision: approve ? 'Approved' : 'Rejected',
            });
            setFeedback({
                type: 'success',
                message: approve ? 'İzin talebi onaylandı.' : 'İzin talebi reddedildi.',
            });
        } catch (err) {
            setFeedback({ type: 'error', message: problemMessage(err) });
        }
    };

    // Takvim grid'i oluştur
    const cells: (number | null)[] = [];
    for (let i = 0; i < firstDayOfWeek; i++) cells.push(null);
    for (let d = 1; d <= daysInMonth; d++) cells.push(d);
    while (cells.length % 7 !== 0) cells.push(null);

    const todayStr = toIsoDate(now);

    return (
        <Stack spacing={2.5}>
            <Box>
                <Typography variant="h1">İzin Takvimi</Typography>
                <Typography variant="body2" color="text.secondary">
                    Ekip izin takibi ve takvim görünümü
                </Typography>
            </Box>

            {feedback && (
                <Alert severity={feedback.type} onClose={() => setFeedback(null)}>
                    {feedback.message}
                </Alert>
            )}

            {/* ── Ay navigasyonu ─────────────────────────────────────────── */}
            <Card>
                <CardContent sx={{ py: 1.5, '&:last-child': { pb: 1.5 } }}>
                    <Stack direction="row" alignItems="center" justifyContent="space-between">
                        <Stack direction="row" alignItems="center" spacing={1}>
                            <IconButton onClick={goToPrev} size="small" aria-label="Önceki ay">
                                <ChevronLeftIcon />
                            </IconButton>
                            <Typography variant="h3" sx={{ minWidth: 160, textAlign: 'center' }}>
                                {MONTH_NAMES[month]} {year}
                            </Typography>
                            <IconButton onClick={goToNext} size="small" aria-label="Sonraki ay">
                                <ChevronRightIcon />
                            </IconButton>
                            <Tooltip title="Bugüne dön">
                                <IconButton onClick={goToToday} size="small">
                                    <TodayIcon />
                                </IconButton>
                            </Tooltip>
                        </Stack>

                        <Stack direction="row" spacing={1} alignItems="center">
                            {/* Renk açıklaması */}
                            {(Object.keys(leaveTypeLabels) as LeaveType[]).map((t) => (
                                <Chip
                                    key={t}
                                    label={leaveTypeLabels[t]}
                                    size="small"
                                    sx={{ bgcolor: leaveTypeColors[t], color: '#fff', fontSize: '0.7rem' }}
                                />
                            ))}
                            <Button
                                variant="contained"
                                size="small"
                                startIcon={<AddIcon />}
                                onClick={() => { setSelectedDate(undefined); setDialogOpen(true); }}
                            >
                                Yeni İzin
                            </Button>
                        </Stack>
                    </Stack>
                </CardContent>
            </Card>

            {/* ── Takvim Grid ────────────────────────────────────────────── */}
            <Card>
                <CardContent sx={{ p: 0, '&:last-child': { pb: 0 } }}>
                    {isLoading && <Box sx={{ p: 2 }}><LoadingSkeleton rows={5} /></Box>}
                    {isError && <Box sx={{ p: 2 }}><ErrorState error={error} onRetry={() => void refetch()} /></Box>}

                    {calendarItems && (
                        <Box
                            sx={{
                                display: 'grid',
                                gridTemplateColumns: 'repeat(7, 1fr)',
                                borderTop: '1px solid',
                                borderLeft: '1px solid',
                                borderColor: 'divider',
                            }}
                        >
                            {/* Gün başlıkları */}
                            {DAY_NAMES.map((name) => (
                                <Box
                                    key={name}
                                    sx={{
                                        p: 1,
                                        textAlign: 'center',
                                        fontWeight: 600,
                                        fontSize: '0.75rem',
                                        bgcolor: 'action.hover',
                                        borderBottom: '1px solid',
                                        borderRight: '1px solid',
                                        borderColor: 'divider',
                                    }}
                                >
                                    {name}
                                </Box>
                            ))}

                            {/* Gün hücreleri */}
                            {cells.map((day, idx) => {
                                const isToday =
                                    day !== null && toIsoDate(new Date(year, month, day)) === todayStr;
                                const leaves = day !== null ? dayLeaves.get(day) ?? [] : [];
                                const isWeekend = idx % 7 >= 5;

                                return (
                                    <Box
                                        key={idx}
                                        onClick={() => day !== null && !isWeekend && handleCellClick(day)}
                                        sx={{
                                            minHeight: 90,
                                            p: 0.5,
                                            borderBottom: '1px solid',
                                            borderRight: '1px solid',
                                            borderColor: 'divider',
                                            bgcolor: isWeekend
                                                ? 'action.disabledBackground'
                                                : isToday
                                                    ? 'primary.50'
                                                    : 'background.paper',
                                            cursor: day !== null && !isWeekend ? 'pointer' : 'default',
                                            '&:hover': day !== null && !isWeekend
                                                ? { bgcolor: isToday ? 'primary.100' : 'action.hover' }
                                                : {},
                                            transition: 'background-color 0.15s',
                                        }}
                                    >
                                        {day !== null && (
                                            <>
                                                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 0.25 }}>
                                                    <Typography
                                                        variant="caption"
                                                        sx={{
                                                            fontWeight: isToday ? 700 : 400,
                                                            color: isToday ? 'primary.main' : isWeekend ? 'text.disabled' : 'text.primary',
                                                        }}
                                                    >
                                                        {day}
                                                    </Typography>
                                                </Box>

                                                {dayHolidays.has(day) && (
                                                    <Tooltip title={dayHolidays.get(day)!}>
                                                        <Box
                                                            sx={{
                                                                bgcolor: 'error.main',
                                                                color: '#fff',
                                                                fontSize: '0.65rem',
                                                                px: 0.5,
                                                                py: 0.15,
                                                                borderRadius: 0.5,
                                                                mb: 0.25,
                                                                overflow: 'hidden',
                                                                whiteSpace: 'nowrap',
                                                                textOverflow: 'ellipsis',
                                                            }}
                                                        >
                                                            {dayHolidays.get(day)!}
                                                        </Box>
                                                    </Tooltip>
                                                )}

                                                {leaves.slice(0, 3).map((leave) => (
                                                    <Tooltip
                                                        key={leave.id}
                                                        title={`${leave.userDisplayName} — ${leaveTypeLabels[leave.type]} (${leaveStatusLabels[leave.status]})`}
                                                    >
                                                        <Box
                                                            sx={{
                                                                bgcolor: leaveTypeColors[leave.type],
                                                                opacity: leave.status === 'Pending' ? 0.6 : 1,
                                                                color: '#fff',
                                                                fontSize: '0.65rem',
                                                                px: 0.5,
                                                                py: 0.15,
                                                                borderRadius: 0.5,
                                                                mb: 0.25,
                                                                overflow: 'hidden',
                                                                whiteSpace: 'nowrap',
                                                                textOverflow: 'ellipsis',
                                                                border: leave.status === 'Pending' ? '1px dashed rgba(255,255,255,0.6)' : 'none',
                                                            }}
                                                        >
                                                            {leave.userDisplayName.split(' ')[0]}
                                                        </Box>
                                                    </Tooltip>
                                                ))}
                                                {leaves.length > 3 && (
                                                    <Typography variant="caption" color="text.secondary" sx={{ fontSize: '0.6rem' }}>
                                                        +{leaves.length - 3} kişi
                                                    </Typography>
                                                )}
                                            </>
                                        )}
                                    </Box>
                                );
                            })}
                        </Box>
                    )}
                </CardContent>
            </Card>

            {/* ── Bekleyen Talepler (Yönetici) ───────────────────────────── */}
            {isManager && pendingLeaves && pendingLeaves.length > 0 && (
                <Card>
                    <CardContent>
                        <Typography variant="h3" sx={{ mb: 2 }}>
                            Onay Bekleyen İzin Talepleri ({pendingLeaves.length})
                        </Typography>
                        <Stack spacing={1.5}>
                            {pendingLeaves.map((leave) => (
                                <Stack
                                    key={leave.id}
                                    direction="row"
                                    alignItems="center"
                                    justifyContent="space-between"
                                    sx={{
                                        p: 1.5,
                                        borderRadius: 1,
                                        bgcolor: 'action.hover',
                                    }}
                                >
                                    <Stack spacing={0.25}>
                                        <Typography variant="subtitle2">{leave.userDisplayName}</Typography>
                                        <Typography variant="body2" color="text.secondary">
                                            {leaveTypeLabels[leave.type]} · {formatDate(leave.startDate)} — {formatDate(leave.endDate)} · {leave.dayCount} iş günü
                                        </Typography>
                                        {leave.description && (
                                            <Typography variant="caption" color="text.secondary">
                                                {leave.description}
                                            </Typography>
                                        )}
                                    </Stack>
                                    <Stack direction="row" spacing={1}>
                                        <Tooltip title="Onayla">
                                            <IconButton
                                                color="success"
                                                onClick={() => void handleDecision(leave.id, true)}
                                                disabled={decision.isPending}
                                            >
                                                <CheckCircleOutlineIcon />
                                            </IconButton>
                                        </Tooltip>
                                        <Tooltip title="Reddet">
                                            <IconButton
                                                color="error"
                                                onClick={() => void handleDecision(leave.id, false)}
                                                disabled={decision.isPending}
                                            >
                                                <CancelOutlinedIcon />
                                            </IconButton>
                                        </Tooltip>
                                    </Stack>
                                </Stack>
                            ))}
                        </Stack>
                    </CardContent>
                </Card>
            )}

            {/* ── İzin Oluşturma Dialog'u ────────────────────────────────── */}
            <CreateLeaveDialog
                open={dialogOpen}
                onClose={() => setDialogOpen(false)}
                initialDate={selectedDate}
            />
        </Stack>
    );
}