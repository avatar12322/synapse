"use client";

import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { motion, useReducedMotion } from 'framer-motion';
import { queryClient } from '@/lib/queryClient';
import BottomNavigation from '@/components/layout/BottomNavigation';
import NotificationBell from '@/components/layout/NotificationBell';
import LanguageSwitcher from '@/components/LanguageSwitcher';
import { Toaster } from 'sonner';
import { useEffect, useState } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import '@/i18n/config';
import { authStorage } from '@/lib/auth';
import i18n from '@/i18n/config';

export default function ClientLayout({ children }: { children: React.ReactNode }) {
  const [isClient, setIsClient] = useState(false);
  const shouldReduceMotion = useReducedMotion();
  const pathname = usePathname();
  const router = useRouter();
  const isAuthPage = pathname.startsWith('/login') || pathname.startsWith('/register');

  useEffect(() => {
    setIsClient(true);
    const user = authStorage.getUser();
    if (user?.language) i18n.changeLanguage(user.language);
  }, []);

  useEffect(() => {
    if (!isClient) return;
    const user = authStorage.getUser();
    if (!user && !isAuthPage) {
      router.replace('/login');
    } else if (user && isAuthPage) {
      router.replace('/');
    }
  }, [isClient, isAuthPage, router]);

  if (!isClient) {
    return (
      <QueryClientProvider client={queryClient}>
        <div className="min-h-screen bg-gradient-to-br from-slate-900 via-purple-900 to-slate-900">
          <div className="p-4 pb-20">{children}</div>
        </div>
      </QueryClientProvider>
    );
  }

  return (
    <QueryClientProvider client={queryClient}>
      <div className="flex flex-col min-h-screen bg-gradient-to-br from-slate-900 via-purple-900/30 to-slate-900">
        {!isAuthPage && (
          <div className="fixed top-4 right-4 z-50 flex items-center gap-3">
            <LanguageSwitcher />
            <NotificationBell />
          </div>
        )}

        <main className="flex-1 overflow-y-auto pb-20">
          <div className="container mx-auto max-w-2xl px-4">
            {children}
          </div>
        </main>

        {!isAuthPage && (
          <motion.div
            initial={{ y: 100, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            transition={{ duration: shouldReduceMotion ? 0.1 : 0.3, ease: 'easeOut', delay: 0.2 }}
            className="fixed bottom-0 left-0 right-0 z-50"
          >
            <BottomNavigation />
          </motion.div>
        )}
      </div>

      <Toaster
        position="top-right"
        toastOptions={{
          duration: 3000,
          style: {
            background: 'rgba(30, 41, 59, 0.95)',
            border: '1px solid rgba(139, 92, 246, 0.3)',
            color: 'white',
            backdropFilter: 'blur(8px)',
            borderRadius: '8px',
            fontSize: '14px',
          },
        }}
      />

      {process.env.NODE_ENV === 'development' && (
        <ReactQueryDevtools initialIsOpen={false} buttonPosition="bottom-left" />
      )}
    </QueryClientProvider>
  );
}
