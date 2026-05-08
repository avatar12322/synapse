"use client";

import { useTranslation } from 'react-i18next';
import { Users } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';

export default function FriendsPage() {
  const { t } = useTranslation();
  return (
    <div className="pt-6 pb-24">
      <h1 className="text-2xl font-black text-purple-300 mb-6">{t('nav.friends')}</h1>
      <Card className="bg-slate-800/50 border border-purple-500/20">
        <CardContent className="flex flex-col items-center justify-center py-16 text-center">
          <Users className="h-16 w-16 text-purple-400/40 mb-4" />
          <p className="text-gray-400">Friends system coming soon.</p>
        </CardContent>
      </Card>
    </div>
  );
}
