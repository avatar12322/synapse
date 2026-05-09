"use client";

import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import Link from 'next/link';
import { motion } from 'framer-motion';
import { Zap, Map, Plus, ChevronRight, AlertCircle, Clock } from 'lucide-react';
import { missionApi, MissionSummary, MissionStatus } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

const STATUS_COLORS: Record<MissionStatus, string> = {
  Pending: 'text-amber-300',
  Accepted: 'text-blue-300',
  InProgress: 'text-emerald-300',
  Completed: 'text-purple-300',
  Expired: 'text-red-400',
  Cancelled: 'text-gray-400',
};

const STATUS_DOT: Record<MissionStatus, string> = {
  Pending: 'bg-amber-400',
  Accepted: 'bg-blue-400',
  InProgress: 'bg-emerald-400 animate-pulse',
  Completed: 'bg-purple-400',
  Expired: 'bg-red-400',
  Cancelled: 'bg-gray-500',
};

function timeUntil(dateStr: string): string {
  const diff = new Date(dateStr).getTime() - Date.now();
  if (diff <= 0) return '';
  const h = Math.floor(diff / 3600000);
  const m = Math.floor((diff % 3600000) / 60000);
  if (h > 0) return `${h}h ${m}m`;
  return `${m}m`;
}

function ActiveMissionCard({ mission }: { mission: MissionSummary }) {
  const { t } = useTranslation();
  const remaining = timeUntil(mission.expiresAt);

  return (
    <Link href={`/missions/${mission.id}`}>
      <motion.div whileHover={{ scale: 1.01 }} whileTap={{ scale: 0.99 }} transition={{ duration: 0.1 }}>
        <div className="bg-slate-800/60 rounded-xl border border-slate-700 p-3 flex items-center gap-3 cursor-pointer hover:bg-slate-800/80 transition-colors">
          <div className={`w-2 h-2 rounded-full shrink-0 ${STATUS_DOT[mission.status]}`} />
          <div className="flex-1 min-w-0">
            <p className="text-white text-sm font-semibold truncate">{mission.title}</p>
            <p className={`text-xs ${STATUS_COLORS[mission.status]}`}>
              {t(`missions.status.${mission.status.charAt(0).toLowerCase() + mission.status.slice(1)}`)}
              {' · '}{mission.businessName}
            </p>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            {remaining && (
              <div className="flex items-center gap-1 text-gray-500 text-xs">
                <Clock className="h-3 w-3" />
                <span>{remaining}</span>
              </div>
            )}
            <span className="text-emerald-400 text-xs font-bold">{mission.discountPercent}%</span>
            <ChevronRight className="h-4 w-4 text-gray-500" />
          </div>
        </div>
      </motion.div>
    </Link>
  );
}

export default function HomePage() {
  const { t } = useTranslation();

  const { data: missions = [], isError } = useQuery({
    queryKey: ['missions'],
    queryFn: missionApi.getAll,
    refetchInterval: 20000,
  });

  const active = missions.filter(m => ['Pending', 'Accepted', 'InProgress'].includes(m.status));

  return (
    <div className="min-h-screen pt-6 pb-24 px-4">
      <div className="max-w-lg mx-auto">
        {/* Hero */}
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

        {/* Active missions */}
        {isError && (
          <Card className="bg-red-900/20 border border-red-500/20 mb-4">
            <CardContent className="flex items-center gap-2 p-3">
              <AlertCircle className="h-4 w-4 text-red-400 shrink-0" />
              <p className="text-red-300 text-sm">{t('common.error')}</p>
            </CardContent>
          </Card>
        )}

        {active.length > 0 && (
          <div className="mb-5">
            <div className="flex items-center justify-between mb-3">
              <p className="text-xs font-bold text-gray-400 uppercase tracking-widest">
                {t('home.activeMissions')}
              </p>
              <Link href="/missions" className="text-purple-400 text-xs font-bold hover:text-purple-300">
                {t('home.viewAll')}
              </Link>
            </div>
            <div className="space-y-2">
              {active.slice(0, 3).map(m => <ActiveMissionCard key={m.id} mission={m} />)}
            </div>
          </div>
        )}

        {/* Empty / CTA */}
        {active.length === 0 && (
          <Card className="bg-slate-800/50 border border-purple-500/20">
            <CardContent className="flex flex-col items-center justify-center py-12 text-center">
              <Map className="h-12 w-12 text-purple-400/50 mb-4" />
              <p className="text-gray-400 mb-5">{t('home.noMissions')}</p>
              <Link href="/match">
                <Button className="bg-gradient-to-r from-purple-600 to-blue-600 hover:from-purple-700 hover:to-blue-700 text-white font-bold gap-2">
                  <Plus className="h-4 w-4" />
                  {t('home.findMatch')}
                </Button>
              </Link>
            </CardContent>
          </Card>
        )}

        {active.length > 0 && (
          <Link href="/match">
            <Button className="w-full bg-gradient-to-r from-purple-600 to-blue-600 hover:from-purple-700 hover:to-blue-700 text-white font-bold gap-2 py-5">
              <Plus className="h-4 w-4" />
              {t('home.findMatch')}
            </Button>
          </Link>
        )}
      </div>
    </div>
  );
}
