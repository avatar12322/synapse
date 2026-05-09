"use client";

import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import Link from 'next/link';
import { motion } from 'framer-motion';
import { Map, Plus, Clock, ChevronRight, AlertCircle, Loader2 } from 'lucide-react';
import { missionApi, MissionSummary, MissionStatus } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

const STATUS_COLORS: Record<MissionStatus, string> = {
  Pending: 'bg-amber-500/20 text-amber-300 border-amber-500/30',
  Accepted: 'bg-blue-500/20 text-blue-300 border-blue-500/30',
  InProgress: 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30',
  Completed: 'bg-purple-500/20 text-purple-300 border-purple-500/30',
  Expired: 'bg-red-500/20 text-red-400 border-red-500/30',
  Cancelled: 'bg-slate-500/20 text-slate-400 border-slate-500/30',
};

const CARD_GLOW: Record<MissionStatus, string> = {
  Pending: 'border-amber-500/30',
  Accepted: 'border-blue-500/40',
  InProgress: 'border-emerald-500/50',
  Completed: 'border-purple-500/30',
  Expired: 'border-slate-600',
  Cancelled: 'border-slate-600',
};

function timeUntil(dateStr: string): string {
  const diff = new Date(dateStr).getTime() - Date.now();
  if (diff <= 0) return 'expired';
  const h = Math.floor(diff / 3600000);
  const m = Math.floor((diff % 3600000) / 60000);
  if (h > 0) return `${h}h ${m}m`;
  return `${m}m`;
}

function StatusBadge({ status }: { status: MissionStatus }) {
  const { t } = useTranslation();
  const key = (status.charAt(0).toLowerCase() + status.slice(1)) as string;
  return (
    <span className={`text-xs font-bold px-2 py-0.5 rounded-full border ${STATUS_COLORS[status]}`}>
      {t(`missions.status.${key}`)}
    </span>
  );
}

function MissionCard({ mission }: { mission: MissionSummary }) {
  const { t } = useTranslation();
  const isActive = ['Pending', 'Accepted', 'InProgress'].includes(mission.status);

  return (
    <Link href={`/missions/${mission.id}`}>
      <motion.div whileHover={{ scale: 1.01 }} whileTap={{ scale: 0.99 }} transition={{ duration: 0.1 }}>
        <Card className={`bg-slate-800/60 border ${CARD_GLOW[mission.status]} hover:bg-slate-800/80 transition-colors cursor-pointer`}>
          <CardContent className="p-4">
            <div className="flex items-start justify-between gap-3">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-1.5 flex-wrap">
                  <StatusBadge status={mission.status} />
                  {mission.isMyTurn && (
                    <span className="text-xs font-bold px-2 py-0.5 rounded-full bg-orange-500/20 text-orange-300 border border-orange-500/30 flex items-center gap-1">
                      <AlertCircle className="h-3 w-3" />
                      {t('missions.myTurn')}
                    </span>
                  )}
                </div>
                <p className="text-white font-semibold text-sm leading-tight">{mission.title}</p>
                <p className="text-gray-400 text-xs mt-0.5">{mission.businessName}</p>
              </div>
              <div className="flex flex-col items-end gap-2 shrink-0">
                <span className="text-emerald-400 font-bold text-sm">{mission.discountPercent}% off</span>
                {isActive && (
                  <div className="flex items-center gap-1 text-gray-500 text-xs">
                    <Clock className="h-3 w-3" />
                    <span>{timeUntil(mission.expiresAt)}</span>
                  </div>
                )}
                <ChevronRight className="h-4 w-4 text-gray-500" />
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>
    </Link>
  );
}

export default function MissionsPage() {
  const { t } = useTranslation();

  const { data: missions = [], isLoading, isError } = useQuery({
    queryKey: ['missions'],
    queryFn: missionApi.getAll,
    refetchInterval: 15000,
  });

  const active = missions.filter(m => ['Pending', 'Accepted', 'InProgress'].includes(m.status));
  const finished = missions.filter(m => ['Completed', 'Expired', 'Cancelled'].includes(m.status));

  return (
    <div className="min-h-screen pt-6 pb-24 px-4">
      <div className="max-w-lg mx-auto">
        <div className="flex items-center justify-between mb-6 pt-6">
          <div className="flex items-center gap-3">
            <div className="relative">
              <div className="absolute inset-0 bg-purple-500 blur-md opacity-40 rounded-full" />
              <Map className="h-7 w-7 text-purple-400 relative" />
            </div>
            <h1 className="text-2xl font-black text-white">{t('missions.title')}</h1>
          </div>
          <Link href="/match">
            <Button size="sm" className="bg-gradient-to-r from-purple-600 to-blue-600 hover:from-purple-700 hover:to-blue-700 text-white font-bold gap-1.5">
              <Plus className="h-4 w-4" />
              {t('missions.findMatch')}
            </Button>
          </Link>
        </div>

        {isLoading && (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="h-8 w-8 text-purple-400 animate-spin" />
          </div>
        )}

        {isError && (
          <Card className="bg-red-900/20 border border-red-500/30">
            <CardContent className="flex items-center gap-3 p-4">
              <AlertCircle className="h-5 w-5 text-red-400 shrink-0" />
              <p className="text-red-300 text-sm">{t('common.error')}</p>
            </CardContent>
          </Card>
        )}

        {!isLoading && !isError && missions.length === 0 && (
          <Card className="bg-slate-800/50 border border-purple-500/20">
            <CardContent className="flex flex-col items-center justify-center py-14 text-center">
              <Map className="h-12 w-12 text-purple-400/40 mb-4" />
              <p className="text-gray-300 font-semibold mb-1">{t('missions.noMissions')}</p>
              <p className="text-gray-500 text-sm mb-5">{t('missions.noMissionsSubtext')}</p>
              <Link href="/match">
                <Button className="bg-gradient-to-r from-purple-600 to-blue-600 hover:from-purple-700 hover:to-blue-700 text-white font-bold">
                  {t('missions.findMatch')}
                </Button>
              </Link>
            </CardContent>
          </Card>
        )}

        {active.length > 0 && (
          <div className="mb-6">
            <p className="text-xs font-bold text-gray-400 uppercase tracking-widest mb-3">Active</p>
            <div className="space-y-3">
              {active.map(m => <MissionCard key={m.id} mission={m} />)}
            </div>
          </div>
        )}

        {finished.length > 0 && (
          <div>
            <p className="text-xs font-bold text-gray-400 uppercase tracking-widest mb-3">History</p>
            <div className="space-y-3">
              {finished.map(m => <MissionCard key={m.id} mission={m} />)}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
