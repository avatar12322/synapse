"use client";

import { useTranslation } from 'react-i18next';
import { Map, Zap } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import Link from 'next/link';

export default function HomePage() {
  const { t } = useTranslation();

  return (
    <div className="min-h-screen pt-6 pb-24">
      {/* Header */}
      <div className="text-center mb-8 pt-8">
        <div className="relative inline-block mb-4">
          <div className="absolute inset-0 bg-gradient-to-r from-purple-500 to-blue-500 blur-xl opacity-40 rounded-full" />
          <Zap className="h-16 w-16 text-purple-400 relative" />
        </div>
        <h1 className="text-4xl font-black text-transparent bg-clip-text bg-gradient-to-r from-purple-400 to-blue-400">
          Synapse
        </h1>
        <p className="text-gray-400 mt-2 text-sm">{t('home.title')}</p>
      </div>

      {/* Active missions placeholder */}
      <div className="space-y-4">
        <Card className="bg-slate-800/50 border border-purple-500/20">
          <CardContent className="flex flex-col items-center justify-center py-12 text-center">
            <Map className="h-12 w-12 text-purple-400/50 mb-4" />
            <p className="text-gray-400">{t('home.noMissions')}</p>
            <Link href="/missions" className="mt-4">
              <Button className="bg-gradient-to-r from-purple-600 to-blue-600 hover:from-purple-700 hover:to-blue-700 text-white font-bold">
                {t('home.findMatch')}
              </Button>
            </Link>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
