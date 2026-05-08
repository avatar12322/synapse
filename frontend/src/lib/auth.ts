import Cookies from 'js-cookie';
import { API_BASE_URL } from './config';

const USER_KEY = 'synapse_user';

export const authStorage = {
  getUser: () => {
    if (typeof window === 'undefined') return null;
    const user = Cookies.get(USER_KEY);
    return user ? JSON.parse(user) : null;
  },

  setUser: (user: any) => {
    if (typeof window === 'undefined') return;
    Cookies.set(USER_KEY, JSON.stringify(user), { expires: 7 });
  },

  removeUser: () => {
    if (typeof window === 'undefined') return;
    Cookies.remove(USER_KEY);
  },

  clear: () => {
    authStorage.removeUser();
    fetch(`${API_BASE_URL}/auth/logout`, { method: 'POST', credentials: 'include' }).catch(() => {});
    if (typeof window !== 'undefined') {
      setTimeout(() => { window.location.href = '/login'; }, 100);
    }
  },

  getUserRole: () => {
    const user = authStorage.getUser();
    return (user?.role as string) || 'User';
  },

  isAdmin: () => authStorage.getUserRole() === 'Admin',
  isBusiness: () => authStorage.getUserRole() === 'Business',

  refreshUserData: async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/auth/me`, { credentials: 'include' });
      if (response.ok) {
        const userData = await response.json();
        authStorage.setUser(userData);
      }
    } catch {
      // ignore
    }
  },
};
