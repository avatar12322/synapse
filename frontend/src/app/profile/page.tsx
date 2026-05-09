"use client";

import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { User } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { authStorage } from '@/lib/auth';
import { Button } from '@/components/ui/button';

export default function ProfilePage() {
  const { t } = useTranslation();
  const [user, setUser] = useState<Record<string, unknown> | null>(null);

  useEffect(() => {
    setUser(authStorage.getUser());
  }, []);

  return (
    <div className="pt-6 pb-24">
      <h1 className="text-2xl font-black text-purple-300 mb-6">{t('nav.profile')}</h1>
      <Card className="bg-slate-800/50 border border-purple-500/20">
        <CardContent className="py-8 text-center">
          <User className="h-16 w-16 text-purple-400/40 mx-auto mb-4" />
          {user ? (
            <div className="space-y-2">
              <p className="text-white font-bold text-lg">{user.username}</p>
              <p className="text-gray-400 text-sm">{user.email}</p>
              <p className="text-gray-500 text-xs mt-1">Role: {user.role}</p>
              <Button onClick={() => authStorage.clear()}
                className="mt-6 bg-red-600/20 border border-red-500/50 text-red-300 hover:bg-red-600/30">
                Sign Out
              </Button>
            </div>
          ) : (
            <p className="text-gray-400">Not logged in</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
