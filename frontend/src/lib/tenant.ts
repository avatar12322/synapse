import { API_BASE_URL } from './config';

export interface Branding {
  name: string;
  logoUrl: string | null;
  primaryColor: string;
  secondaryColor: string;
}

const DEFAULT_BRANDING: Branding = {
  name: 'Synapse',
  logoUrl: null,
  primaryColor: '#7c3aed',
  secondaryColor: '#1e293b',
};

export async function fetchBranding(): Promise<Branding> {
  try {
    const res = await fetch(`${API_BASE_URL}/tenant/branding`, {
      cache: 'no-store',
    });
    if (!res.ok) return DEFAULT_BRANDING;
    return res.json();
  } catch {
    return DEFAULT_BRANDING;
  }
}
