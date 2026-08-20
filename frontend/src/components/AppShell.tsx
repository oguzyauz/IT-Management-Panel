import { useState } from 'react';
import type { ReactNode } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import {
  AppBar,
  Avatar,
  Box,
  Chip,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Stack,
  Toolbar,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import ConfirmationNumberOutlinedIcon from '@mui/icons-material/ConfirmationNumberOutlined';
import CalendarMonthOutlinedIcon from '@mui/icons-material/CalendarMonthOutlined';
import NotificationsActiveOutlinedIcon from '@mui/icons-material/NotificationsActiveOutlined';
import HistoryOutlinedIcon from '@mui/icons-material/HistoryOutlined';
import EventAvailableOutlinedIcon from '@mui/icons-material/EventAvailableOutlined';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import LogoutIcon from '@mui/icons-material/Logout';
import PasswordIcon from '@mui/icons-material/Password';
import { useAuth } from '../auth/AuthContext';

const DRAWER_WIDTH = 264;

interface NavItem {
  label: string;
  to: string;
  icon: ReactNode;
}

const managerNav: NavItem[] = [
  { label: 'Dashboard', to: '/manager/dashboard', icon: <DashboardOutlinedIcon /> },
  { label: "Ticket'lar", to: '/manager/tickets', icon: <ConfirmationNumberOutlinedIcon /> },
  { label: 'Ekip takvimi', to: '/manager/team-schedule', icon: <CalendarMonthOutlinedIcon /> },
  { label: 'Hatırlatma gönder', to: '/manager/reminders', icon: <NotificationsActiveOutlinedIcon /> },
  { label: 'Hatırlatma geçmişi', to: '/manager/reminder-history', icon: <HistoryOutlinedIcon /> },
  { label: 'Yönetim', to: '/manager/admin', icon: <SettingsOutlinedIcon /> },
];

const employeeNav: NavItem[] = [
  { label: "Ticket'larım", to: '/employee/my-tickets', icon: <ConfirmationNumberOutlinedIcon /> },
  { label: 'Çalışma planım', to: '/employee/my-schedule', icon: <EventAvailableOutlinedIcon /> },
];

export function AppShell({ children }: { children: ReactNode }) {
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'));
  const [mobileOpen, setMobileOpen] = useState(false);
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const { user, isManager, logout, authProvider } = useAuth();
  const location = useLocation();

  const items = isManager ? [...managerNav, ...employeeNav] : employeeNav;

  const sidebar = (
    <Box sx={{ width: DRAWER_WIDTH, height: '100%', display: 'flex', flexDirection: 'column' }}>
      <Toolbar sx={{ px: 2 }}>
        <Stack spacing={0.25}>
          <Typography variant="subtitle1" fontWeight={700} lineHeight={1.2}>
            IT Yönetim Paneli
          </Typography>
          <Typography variant="caption" color="text.secondary">
            Mail tabanlı ticket takibi
          </Typography>
        </Stack>
      </Toolbar>
      <Divider />
      <List sx={{ px: 1, py: 1.5, flexGrow: 1 }}>
        {items.map((item) => (
          <ListItemButton
            key={item.to}
            component={NavLink}
            to={item.to}
            selected={location.pathname.startsWith(item.to)}
            onClick={() => setMobileOpen(false)}
            sx={{ borderRadius: 1.5, mb: 0.5 }}
          >
            <ListItemIcon sx={{ minWidth: 40 }}>{item.icon}</ListItemIcon>
            <ListItemText primary={item.label} primaryTypographyProps={{ fontSize: 14 }} />
          </ListItemButton>
        ))}
      </List>
      <Divider />
      <Box sx={{ p: 2 }}>
        <Typography variant="caption" color="text.secondary">
          Tixbox'a yazma işlemi yapılmaz. Panel yalnızca takip amaçlıdır.
        </Typography>
      </Box>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar
        position="fixed"
        color="inherit"
        elevation={0}
        sx={{
          zIndex: (t) => t.zIndex.drawer + 1,
          borderBottom: '1px solid',
          borderColor: 'divider',
        }}
      >
        <Toolbar>
          {!isDesktop && (
            <IconButton edge="start" onClick={() => setMobileOpen(true)} sx={{ mr: 1 }} aria-label="Menü">
              <MenuIcon />
            </IconButton>
          )}
          <Typography variant="subtitle1" fontWeight={700} sx={{ flexGrow: 1 }}>
            IT Yönetim Paneli
          </Typography>

          {user && (
            <Stack direction="row" spacing={1.5} alignItems="center">
              <Chip
                label={isManager ? 'Yönetici' : 'Çalışan'}
                size="small"
                color={isManager ? 'primary' : 'default'}
              />
              <Tooltip title={user.email}>
                <IconButton onClick={(e) => setAnchorEl(e.currentTarget)} size="small" aria-label="Hesap">
                  <Avatar sx={{ width: 32, height: 32, bgcolor: 'primary.main', fontSize: 14 }}>
                    {user.displayName.charAt(0)}
                  </Avatar>
                </IconButton>
              </Tooltip>
              <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={() => setAnchorEl(null)}>
                <MenuItem disabled>
                  <Stack>
                    <Typography variant="body2" fontWeight={600}>
                      {user.displayName}
                    </Typography>
                    <Typography variant="caption">{user.title ?? user.email}</Typography>
                  </Stack>
                </MenuItem>
                <Divider />
                {authProvider !== 'Ldap' && (
                  <MenuItem
                    component={NavLink}
                    to="/parola-degistir"
                    onClick={() => setAnchorEl(null)}
                  >
                    <ListItemIcon>
                      <PasswordIcon fontSize="small" />
                    </ListItemIcon>
                    Parola değiştir
                  </MenuItem>
                )}
                <MenuItem
                  onClick={() => {
                    setAnchorEl(null);
                    logout();
                  }}
                >
                  <ListItemIcon>
                    <LogoutIcon fontSize="small" />
                  </ListItemIcon>
                  Çıkış yap
                </MenuItem>
              </Menu>
            </Stack>
          )}
        </Toolbar>
      </AppBar>

      <Box component="nav" sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}>
        <Drawer
          variant={isDesktop ? 'permanent' : 'temporary'}
          open={isDesktop || mobileOpen}
          onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{
            '& .MuiDrawer-paper': {
              width: DRAWER_WIDTH,
              boxSizing: 'border-box',
              borderRight: '1px solid',
              borderColor: 'divider',
            },
          }}
        >
          {isDesktop && <Toolbar />}
          {sidebar}
        </Drawer>
      </Box>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          width: { md: `calc(100% - ${DRAWER_WIDTH}px)` },
          p: { xs: 2, md: 3 },
          pt: { xs: 10, md: 11 },
        }}
      >
        {children}
      </Box>
    </Box>
  );
}
