import { authStorage } from './auth';
import { API_BASE_URL } from './config';

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

async function tryRefreshToken(): Promise<boolean> {
  try {
    const res = await fetch(`${API_BASE_URL}/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
    });
    return res.ok;
  } catch {
    return false;
  }
}

export async function apiRequest<T>(endpoint: string, options?: RequestInit, isRetry = false): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...options?.headers },
    credentials: 'include',
  });

  if (!response.ok) {
    if (response.status === 401 && !isRetry) {
      const refreshed = await tryRefreshToken();
      if (refreshed) return apiRequest<T>(endpoint, options, true);
      authStorage.clear();
      return {} as T;
    }
    let errorMessage = `API Error: ${response.statusText}`;
    try { const text = await response.text(); if (text) errorMessage = text; } catch { /* ignore */ }
    throw new ApiError(response.status, errorMessage);
  }

  if (response.status === 204) return {} as T;
  return response.json();
}

export interface Notification {
  id: number;
  title: string;
  message: string;
  type: 0 | 1 | 2 | 3 | 4;
  isRead: boolean;
  createdAt: string;
}

export interface UserProfile {
  id: number;
  email: string;
  username: string;
  role: string;
  language: string;
}

export const notificationApi = {
  getUnread: () => apiRequest<Notification[]>('/notifications'),
  markAllRead: () => apiRequest<void>('/notifications/read-all', { method: 'POST' }),
};

export const userApi = {
  getProfile: () => apiRequest<UserProfile>('/users/profile'),
};
