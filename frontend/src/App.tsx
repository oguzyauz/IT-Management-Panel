import { Navigate, Route, Routes } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import type { ReactElement } from 'react';
import { AppShell } from './components/AppShell';
import { useAuth } from './auth/AuthContext';
import { LoginPage } from './pages/LoginPage';
import { ErrorPage, NotFoundPage, UnauthorizedPage } from './pages/StatusPages';
import { ManagerDashboard } from './pages/ManagerDashboard';
import { TicketsPage } from './pages/TicketsPage';
import { TicketDetailPage } from './pages/TicketDetailPage';
import { TeamSchedulePage } from './pages/TeamSchedulePage';
import { RemindersPage } from './pages/RemindersPage';
import { ReminderHistoryPage } from './pages/ReminderHistoryPage';
import { MySchedulePage } from './pages/MySchedulePage';
import { MyTicketsPage } from './pages/MyTicketsPage';
import { WidgetPage } from './pages/WidgetPage';
import { AdminPage } from './pages/AdminPage';
import { ChangePasswordPage } from './pages/ChangePasswordPage';

function FullPageSpinner() {
  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', alignItems: 'center', justifyContent: 'center' }}>
      <CircularProgress />
    </Box>
  );
}

function RequireAuth({
  children,
  managerOnly = false,
  allowPendingPassword = false,
}: {
  children: ReactElement;
  managerOnly?: boolean;
  /** Parola değiştirme sayfasının kendisi bu kontrolden muaftır, yoksa döngüye girer. */
  allowPendingPassword?: boolean;
}) {
  const { isAuthenticated, isLoading, isManager, mustChangePassword } = useAuth();

  if (isLoading) return <FullPageSpinner />;
  if (!isAuthenticated) return <Navigate to="/login" replace />;

  // Yönetici geçici parola verdiyse başka hiçbir sayfa açılmaz.
  if (mustChangePassword && !allowPendingPassword) return <Navigate to="/parola-degistir" replace />;

  if (managerOnly && !isManager) return <Navigate to="/unauthorized" replace />;

  return <AppShell>{children}</AppShell>;
}

function HomeRedirect() {
  const { isAuthenticated, isLoading, isManager } = useAuth();

  if (isLoading) return <FullPageSpinner />;
  if (!isAuthenticated) return <Navigate to="/login" replace />;

  return <Navigate to={isManager ? '/manager/dashboard' : '/employee/my-tickets'} replace />;
}

export function App() {
  return (
    <Routes>
      <Route path="/" element={<HomeRedirect />} />

      {/* Masaüstü özet kutusu: AppShell kullanmaz, kendi dar düzeni vardır. */}
      <Route path="/widget" element={<WidgetPage />} />

      <Route path="/login" element={<LoginPage />} />
      <Route path="/unauthorized" element={<UnauthorizedPage />} />
      <Route path="/error" element={<ErrorPage />} />

      <Route
        path="/manager/dashboard"
        element={
          <RequireAuth managerOnly>
            <ManagerDashboard />
          </RequireAuth>
        }
      />
      <Route
        path="/manager/tickets"
        element={
          <RequireAuth managerOnly>
            <TicketsPage />
          </RequireAuth>
        }
      />
      <Route
        path="/manager/tickets/:id"
        element={
          <RequireAuth managerOnly>
            <TicketDetailPage />
          </RequireAuth>
        }
      />
      <Route
        path="/manager/team-schedule"
        element={
          <RequireAuth managerOnly>
            <TeamSchedulePage />
          </RequireAuth>
        }
      />
      <Route
        path="/manager/reminders"
        element={
          <RequireAuth managerOnly>
            <RemindersPage />
          </RequireAuth>
        }
      />
      <Route
        path="/manager/reminder-history"
        element={
          <RequireAuth managerOnly>
            <ReminderHistoryPage />
          </RequireAuth>
        }
      />

      <Route
        path="/manager/admin"
        element={
          <RequireAuth managerOnly>
            <AdminPage />
          </RequireAuth>
        }
      />

      {/* Zorunlu parola değişimi bu sayfaya yönlendirir; herkese açık olmalı. */}
      <Route
        path="/parola-degistir"
        element={
          <RequireAuth allowPendingPassword>
            <ChangePasswordPage />
          </RequireAuth>
        }
      />

      <Route
        path="/employee/my-tickets"
        element={
          <RequireAuth>
            <MyTicketsPage />
          </RequireAuth>
        }
      />
      <Route
        path="/employee/my-schedule"
        element={
          <RequireAuth>
            <MySchedulePage />
          </RequireAuth>
        }
      />

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
