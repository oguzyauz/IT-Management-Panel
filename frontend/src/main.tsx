import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CssBaseline, ThemeProvider } from '@mui/material';
import { App } from './App';
import { theme } from './theme';
import { AuthProvider } from './auth/AuthContext';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
});

const container = document.getElementById('root');
if (!container) throw new Error('#root bulunamadı');

// Uygulamanın "yüklenebilir uygulama" olarak kurulabilmesi için service worker gerekir.
// Önbellek yapmaz (bkz. public/sw.js); yalnızca kurulabilirliği sağlar.
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    void navigator.serviceWorker.register('/sw.js').catch(() => {
      /* kurulum başarısız olursa uygulama normal web sayfası olarak çalışmaya devam eder */
    });
  });
}

createRoot(container).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <AuthProvider>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </AuthProvider>
      </ThemeProvider>
    </QueryClientProvider>
  </StrictMode>,
);
