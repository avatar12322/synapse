"use client";

import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { motion } from 'framer-motion';
import { Star, Zap, Clock, TrendingUp } from 'lucide-react';
import { reputationApi, ReputationTransaction } from '@/lib/api';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';

const LEVEL_COLORS = ['', 'text-gray-400', 'text-blue-400', 'text-emerald-400', 'text-amber-400', 'text-purple-400'];
const LEVEL_BG = ['', 'bg-gray-500/20', 'bg-blue-500/20', 'bg-emerald-500/20', 'bg-amber-500/20', 'bg-purple-500/20'];
const REASON_ICON: Record<string, string> = {
  MissionCompleted: '🎯',
  NfcVerification: '📡',
  FirstMission: '🚀',
  Streak: '🔥',
};

function TransactionRow({ tx }: { tx: ReputationTransaction }) {
  const { t } = useTranslation();
  const icon = REASON_ICON[tx.reason] ?? '⭐';
  const date = new Date(tx.createdAt).toLocaleDateString();

  return (
    <div className="flex items-center gap-3 py-3 border-b border-slate-700/50 last:border-0">
      <span className="text-xl w-8 shrink-0 text-center">{icon}</span>
      <div className="flex-1 min-w-0">
        <p className="text-white text-sm font-medium">
          {t(`reputation.reasons.${tx.reason}`, tx.reason)}
        </p>
        {tx.missionId && (
          <p className="text-gray-500 text-xs">Mission #{tx.missionId} · {date}</p>
        )}
        {!tx.missionId && (
          <p className="text-gray-500 text-xs">{date}</p>
        )}
      </div>
      <span className="text-emerald-400 font-bold text-sm shrink-0">+{tx.points}</span>
    </div>
  );
}

export default function ReputationPage() {
  const { t } = useTranslation();

  const { data: rep, isLoading: repLoading } = useQuery({
    queryKey: ['reputation'],
    queryFn: reputationApi.getMyReputation,
  });

  const { data: transactions = [], isLoading: txLoading } = useQuery({
    queryKey: ['reputation-transactions'],
    queryFn: reputationApi.getTransactions,
  });

  const isLoading = repLoading || txLoading;

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-purple-400 animate-pulse text-lg">{t('common.loading')}</div>
      </div>
    );
  }

  const level = rep?.repLevel ?? 1;
  const points = rep?.totalPoints ?? 0;
  const progress = rep?.progressPercent ?? 0;
  const toNext = rep?.pointsToNextLevel ?? 100;
  const isMaxLevel = level === 5;
  const levelName = t(`reputation.levels.${level}`);

  return (
    <div className="min-h-screen pt-6 pb-24 px-4">
      <div className="max-w-lg mx-auto space-y-5">
        {/* Header */}
        <div className="text-center pt-6 mb-2">
          <h1 className="text-3xl font-black text-transparent bg-clip-text bg-gradient-to-r from-amber-400 to-purple-400">
            {t('reputation.title')}
          </h1>
        </div>

        {/* Level card */}
        <motion.div
          initial={{ scale: 0.92, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          transition={{ duration: 0.3 }}
        >
          <Card className="bg-slate-800/70 border border-purple-500/20 overflow-hidden">
            <CardContent className="p-6">
              <div className="flex items-center gap-4 mb-5">
                <div className={`w-16 h-16 rounded-2xl ${LEVEL_BG[level]} flex items-center justify-center shrink-0`}>
                  <Star className={`h-8 w-8 ${LEVEL_COLORS[level]}`} />
                </div>
                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-1">
                    <Badge className={`${LEVEL_BG[level]} ${LEVEL_COLORS[level]} border-0 font-bold`}>
                      {t('reputation.level')} {level}
                    </Badge>
                    <span className={`text-sm font-bold ${LEVEL_COLORS[level]}`}>{levelName}</span>
                  </div>
                  <p className="text-white text-2xl font-black">
                    {points.toLocaleString()} <span className="text-gray-400 text-base font-normal">{t('reputation.points')}</span>
                  </p>
                </div>
              </div>

              {/* Progress bar */}
              {!isMaxLevel ? (
                <div>
                  <div className="flex justify-between text-xs text-gray-400 mb-2">
                    <span className="flex items-center gap-1"><TrendingUp className="h-3 w-3" />{t('reputation.nextLevel')}</span>
                    <span className="font-bold text-amber-400">{toNext} {t('reputation.points')}</span>
                  </div>
                  <div className="h-3 bg-slate-700 rounded-full overflow-hidden">
                    <motion.div
                      className="h-full bg-gradient-to-r from-amber-500 to-purple-500 rounded-full"
                      initial={{ width: 0 }}
                      animate={{ width: `${Math.min(progress, 100)}%` }}
                      transition={{ duration: 0.8, ease: 'easeOut' }}
                    />
                  </div>
                  <div className="flex justify-between text-xs text-gray-500 mt-1">
                    <span>Poz. {level}</span>
                    <span>{Math.round(progress)}%</span>
                    <span>Poz. {level + 1}</span>
                  </div>
                </div>
              ) : (
                <div className="text-center py-2">
                  <p className="text-amber-400 font-bold text-sm flex items-center justify-center gap-2">
                    <Zap className="h-4 w-4" />
                    {t('reputation.maxLevel')}
                  </p>
                </div>
              )}
            </CardContent>
          </Card>
        </motion.div>

        {/* Transactions */}
        <Card className="bg-slate-800/70 border border-slate-700">
          <CardHeader className="pb-2">
            <CardTitle className="text-white text-base font-bold flex items-center gap-2">
              <Clock className="h-4 w-4 text-purple-400" />
              {t('reputation.transactionsTitle')}
            </CardTitle>
          </CardHeader>
          <CardContent className="px-4 pb-4">
            {transactions.length === 0 ? (
              <p className="text-gray-400 text-sm text-center py-6">{t('reputation.noTransactions')}</p>
            ) : (
              <div>
                {transactions.map(tx => (
                  <TransactionRow key={tx.id} tx={tx} />
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
