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

export type MissionStatus = 'Pending' | 'Accepted' | 'InProgress' | 'Completed' | 'Expired' | 'Cancelled';

export interface MissionSummary {
  id: number;
  title: string;
  status: MissionStatus;
  category: string;
  businessName: string;
  discountPercent: number;
  expiresAt: string;
  isMyTurn: boolean;
}

export interface Mission {
  id: number;
  title: string;
  description: string;
  status: MissionStatus;
  category: string;
  businessId: number;
  businessName: string;
  businessAddress: string;
  businessLatitude: number;
  businessLongitude: number;
  userAId: number | null;
  userBId: number | null;
  userAAccepted: boolean;
  userBAccepted: boolean;
  discountDescription: string | null;
  discountPercent: number;
  verificationCode: string | null;
  lockedAt: string | null;
  completedAt: string | null;
  requiredLockMinutes: number;
  createdAt: string;
  expiresAt: string;
  interestTags: string | null;
}

export interface NfcVerifyRequest {
  rawPayload: string;
}

export interface Business {
  id: number;
  name: string;
  address: string;
  city: string;
  category: string;
  latitude: number;
  longitude: number;
  ownerId: number;
  isActive: boolean;
  logoUrl?: string;
}

export interface CreateBusinessRequest {
  name: string;
  address: string;
  city: string;
  category: string;
  latitude: number;
  longitude: number;
}

export interface UpdateBusinessRequest extends Partial<CreateBusinessRequest> {
  isActive?: boolean;
}

export interface MatchResult {
  status: 'matched' | 'searching' | 'no_venues';
  missionId: number | null;
  message: string | null;
}

export const notificationApi = {
  getUnread: () => apiRequest<Notification[]>('/notifications'),
  markAllRead: () => apiRequest<void>('/notifications/read-all', { method: 'POST' }),
};

export const userApi = {
  getProfile: () => apiRequest<UserProfile>('/users/profile'),
};

export const missionApi = {
  getAll: () => apiRequest<MissionSummary[]>('/missions'),
  getById: (id: number) => apiRequest<Mission>(`/missions/${id}`),
  accept: (id: number) => apiRequest<Mission>(`/missions/${id}/accept`, { method: 'POST' }),
  cancel: (id: number) => apiRequest<Mission>(`/missions/${id}/cancel`, { method: 'POST' }),
  verify: (code: string) => apiRequest<Mission>('/missions/verify', {
    method: 'POST',
    body: JSON.stringify({ code }),
  }),
  verifyNfc: (id: number, rawPayload: string) => apiRequest<Mission>(`/missions/${id}/verify-nfc`, {
    method: 'POST',
    body: JSON.stringify({ RawPayload: rawPayload }),
  }),
};

export const businessApi = {
  getNearby: (lat: number, lng: number, radius = 2000, category?: string) =>
    apiRequest<Business[]>(`/businesses/nearby?lat=${lat}&lng=${lng}&radius=${radius}${category ? `&category=${category}` : ''}`),
  getById: (id: number) => apiRequest<Business>(`/businesses/${id}`),
  create: (req: CreateBusinessRequest) => apiRequest<Business>('/businesses', {
    method: 'POST',
    body: JSON.stringify(req),
  }),
  update: (id: number, req: UpdateBusinessRequest) => apiRequest<Business>(`/businesses/${id}`, {
    method: 'PUT',
    body: JSON.stringify(req),
  }),
  getMine: () => apiRequest<Business>('/businesses/mine'),
  seed: () => apiRequest<{ message: string }>('/businesses/seed'),
};

export const stripeApi = {
  onboard: () => apiRequest<{ url: string }>('/stripe/connect/onboard', { method: 'POST' }),
  complete: (accountId: string) => apiRequest<{ chargesEnabled: boolean }>(`/stripe/connect/complete?accountId=${accountId}`, { method: 'POST' }),
};

export const matchApi = {
  request: (latitude: number, longitude: number, category?: string, radiusMetres = 2000) =>
    apiRequest<MatchResult>('/match', {
      method: 'POST',
      body: JSON.stringify({ latitude, longitude, category, radiusMetres }),
    }),
};

export interface UserReputation {
  userId: number;
  totalPoints: number;
  repLevel: number;
  pointsToNextLevel: number;
  progressPercent: number;
  updatedAt: string;
}

export interface ReputationTransaction {
  id: number;
  points: number;
  reason: string;
  missionId: number | null;
  createdAt: string;
}

export const reputationApi = {
  getMyReputation: () => apiRequest<UserReputation>('/reputation'),
  getTransactions: () => apiRequest<ReputationTransaction[]>('/reputation/transactions'),
};
