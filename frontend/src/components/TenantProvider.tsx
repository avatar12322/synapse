'use client';

import { useEffect } from 'react';
import { fetchBranding } from '@/lib/tenant';

export default function TenantProvider({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    fetchBranding().then(b => {
      const root = document.documentElement;
      root.style.setProperty('--color-brand-primary', b.primaryColor);
      root.style.setProperty('--color-brand-secondary', b.secondaryColor);
    });
  }, []);

  return <>{children}</>;
}
