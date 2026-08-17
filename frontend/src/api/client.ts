import axios, { AxiosError } from 'axios';

export const TOKEN_STORAGE_KEY = 'it-cockpit.token';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api',
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_STORAGE_KEY);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

/**
 * Sunucu oturumu reddettiyse (süresi dolmuş, iptal edilmiş, kullanıcı pasifleştirilmiş)
 * istemcideki token da atılır. Aksi halde kullanıcı giriş ekranına düşer ama ölü token
 * saklanmaya devam eder ve her istekte 401 üretir.
 *
 * Giriş uçlarının kendi 401'i muaftır: orada "parola yanlış" demek gerekir, oturum düşürmek değil.
 */
const AUTH_ENDPOINTS = ['/auth/login', '/auth/initial-setup', '/auth/mock-login'];

api.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    const url = error.config?.url ?? '';
    const isAuthCall = AUTH_ENDPOINTS.some((endpoint) => url.includes(endpoint));

    if (error.response?.status === 401 && !isAuthCall) {
      localStorage.removeItem(TOKEN_STORAGE_KEY);
    }

    return Promise.reject(error);
  },
);

export interface ApiProblem {
  status: number;
  title: string;
  detail?: string;
  code?: string;
}

/** ProblemDetails yanıtını okunabilir bir hataya çevirir. */
export function toApiProblem(error: unknown): ApiProblem {
  if (error instanceof AxiosError) {
    const data = error.response?.data as
      | { title?: string; detail?: string; type?: string; status?: number }
      | undefined;

    return {
      status: error.response?.status ?? 0,
      title: data?.title ?? error.message,
      detail: data?.detail,
      code: data?.type,
    };
  }

  return { status: 0, title: 'Bilinmeyen hata' };
}

/**
 * Kullanıcıya gösterilecek mesaj. Sunucu bir açıklama döndürdüyse o kullanılır;
 * döndürmediyse ham axios metni ("Request failed with status code 403") yerine
 * durumu anlatan Türkçe bir karşılık verilir.
 */
export function problemMessage(error: unknown): string {
  const problem = toApiProblem(error);
  if (problem.detail) return problem.detail;

  switch (problem.status) {
    case 401:
      return 'Oturumunuz sona ermiş görünüyor. Tekrar giriş yapın.';
    case 403:
      return 'Bu bilgiye erişim yetkiniz yok.';
    case 404:
      return 'Kayıt bulunamadı.';
    case 409:
      return 'Bu kayıt zaten var.';
    case 0:
      return 'Sunucuya ulaşılamıyor. Uygulamanın çalıştığından emin olun.';
    default:
      return problem.title;
  }
}
