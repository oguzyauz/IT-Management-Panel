import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api, TOKEN_STORAGE_KEY } from '../api/client';
import { useMe } from '../api/hooks';
import type { CurrentUserDto } from '../api/types';

interface AuthContextValue {
  user: CurrentUserDto | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  isManager: boolean;
  isEmployee: boolean;
  /** Yönetici geçici parola verdi; kullanıcı değiştirmeden panele giremez. */
  mustChangePassword: boolean;
  login: (token: string) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [token, setToken] = useState<string | null>(() => localStorage.getItem(TOKEN_STORAGE_KEY));

  const { data, isLoading, isError } = useMe(Boolean(token));

  const login = useCallback(
    (newToken: string) => {
      localStorage.setItem(TOKEN_STORAGE_KEY, newToken);
      setToken(newToken);
      void queryClient.invalidateQueries();
    },
    [queryClient],
  );

  const logout = useCallback(() => {
    // Sunucudaki oturum da kapatılır; token'ı yalnızca istemciden silmek yetmez.
    // İstek başarısız olsa bile yerel oturum temizlenir — kullanıcı çıkışta takılı kalmamalı.
    void api.post('/auth/logout').catch(() => undefined);

    localStorage.removeItem(TOKEN_STORAGE_KEY);
    setToken(null);
    queryClient.clear();
  }, [queryClient]);

  const value = useMemo<AuthContextValue>(() => {
    const user = isError ? null : (data ?? null);
    const roles = user?.roles ?? [];

    return {
      user,
      isLoading: Boolean(token) && isLoading,
      isAuthenticated: Boolean(token) && Boolean(user),
      isManager: roles.includes('MANAGER') || roles.includes('ADMIN'),
      isEmployee: roles.includes('EMPLOYEE'),
      mustChangePassword: user?.mustChangePassword ?? false,
      login,
      logout,
    };
  }, [data, isError, isLoading, login, logout, token]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth, AuthProvider içinde kullanılmalıdır.');
  return context;
}
