"use client";

import { useTranslation } from 'react-i18next';
import { Map } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';

export default function MissionsPage() {
  const { t } = useTranslation();
  return (
    <div className="pt-6 pb-24">
      <h1 className="text-2xl font-black text-purple-300 mb-6">{t('missions.title')}</h1>
      <Card className="bg-slate-800/50 border border-purple-500/20">
        <CardContent className="flex flex-col items-center justify-center py-16 text-center">
          <Map className="h-16 w-16 text-purple-400/40 mb-4" />
          <p className="text-gray-400">Mission matching coming soon.</p>
          <p className="text-gray-500 text-sm mt-2">AI agents will generate missions for you based on your interests.</p>
        </CardContent>
      </Card>
    </div>
  );
}
