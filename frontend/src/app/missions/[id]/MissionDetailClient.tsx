"use client";

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { motion } from 'framer-motion';
import {
  ArrowLeft, MapPin, Lock, CheckCircle2, Clock, Zap, Copy,
  AlertCircle, Loader2, XCircle, Tag, Radio
} from 'lucide-react';
import { missionApi, Mission } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { usePresence } from '@/hooks/usePresence';
import { authStorage } from '@/lib/auth';

function formatElapsed(lockedAt: string): string {
  const elapsedMs = Date.now() - new Date(lockedAt).getTime();
  const totalSec = Math.floor(elapsedMs / 1000);
  const m = Math.floor(totalSec / 60);
  const s = totalSec % 60;
  return `${m}:${s.toString().padStart(2, '0')}`;
}

function mapsUrl(lat: number, lng: number, name: string): string {
  return `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}&destination_place_id=${encodeURIComponent(name)}`;
}

function PendingState({ mission, onAccept, onCancel, isMutating }: {
  mission: Mission;
  onAccept: () => void;
  onCancel: () => void;
  isMutating: boolean;
}) {
  const { t } = useTranslation();
  const myTurn = !mission.userAAccepted || !mission.userBAccepted;

  return (
    <div className="space-y-4">
      <Card className="bg-amber-900/20 border border-amber-500/30">
        <CardContent className="p-4 text-center">
          <div className="flex items-center justify-center gap-2 mb-3">
            <Clock className="h-5 w-5 text-amber-400" />
            <p className="text-amber-300 font-bold text-sm">
              {mission.userAAccepted && mission.userBAccepted
                ? t('missionDetail.bothAccepted')
                : myTurn
                ? t('missionDetail.waitingForPartner')
                : t('missionDetail.youAccepted')}
            </p>
          </div>
          <div className="flex items-center justify-center gap-4 text-sm">
            <div className={`flex items-center gap-1 ${mission.userAAccepted ? 'text-emerald-400' : 'text-gray-500'}`}>
              <CheckCircle2 className="h-4 w-4" />
              <span>User A</span>
            </div>
            <div className={`flex items-center gap-1 ${mission.userBAccepted ? 'text-emerald-400' : 'text-gray-500'}`}>
              <CheckCircle2 className="h-4 w-4" />
              <span>User B</span>
            </div>
          </div>
        </CardContent>
      </Card>

      {myTurn && (
        <Button
          onClick={onAccept}
          disabled={isMutating}
          className="w-full bg-gradient-to-r from-purple-600 to-blue-600 hover:from-purple-700 hover:to-blue-700 text-white font-bold py-6 text-lg"
        >
          {isMutating ? <Loader2 className="h-5 w-5 animate-spin" /> : t('missionDetail.accept')}
        </Button>
      )}

      <Button
        onClick={onCancel}
        disabled={isMutating}
        variant="outline"
        className="w-full border-red-500/30 text-red-400 hover:bg-red-900/20"
      >
        <XCircle className="h-4 w-4 mr-2" />
        {t('missionDetail.cancel')}
      </Button>
    </div>
  );
}

function InProgressState({ mission, onCancel, onVerifyNfc, isMutating }: {
  mission: Mission;
  onCancel: () => void;
  onVerifyNfc: () => void;
  isMutating: boolean;
}) {
  const { t } = useTranslation();
  const user = authStorage.getUser();
  const { isPartnerNearby } = usePresence(mission.id, user?.id || 0);
  const [elapsed, setElapsed] = useState('0:00');
  const [copied, setCopied] = useState(false);

  const requiredMs = mission.requiredLockMinutes * 60 * 1000;
  const elapsedMs = mission.lockedAt
    ? Date.now() - new Date(mission.lockedAt).getTime()
    : 0;
  const progress = Math.min((elapsedMs / requiredMs) * 100, 100);
  const done = elapsedMs >= requiredMs;

  useEffect(() => {
    const id = setInterval(() => {
      if (mission.lockedAt) setElapsed(formatElapsed(mission.lockedAt));
    }, 1000);
    if (mission.lockedAt) setElapsed(formatElapsed(mission.lockedAt));
    return () => clearInterval(id);
  }, [mission.lockedAt]);

  const copyCode = async () => {
    if (mission.verificationCode) {
      await navigator.clipboard.writeText(mission.verificationCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  return (
    <div className="space-y-4">
      {/* Timer */}
      <Card className="bg-emerald-900/20 border border-emerald-500/40">
        <CardContent className="p-5 text-center">
          <p className="text-emerald-400 text-xs font-bold uppercase tracking-widest mb-2">
            {t('missionDetail.inProgress')}
          </p>
          <div className="text-5xl font-black text-white font-mono mb-3">{elapsed}</div>
          <Progress value={progress} className="h-2 bg-slate-700" />
          <div className="flex justify-between text-xs text-gray-500 mt-1.5">
            <span>{t('missionDetail.timeElapsed')}</span>
            <span>{mission.requiredLockMinutes} {t('missionDetail.minutes')}</span>
          </div>
        </CardContent>
      </Card>

      {/* Partner Presence */}
      <Card className={`border transition-colors ${isPartnerNearby ? 'bg-blue-900/20 border-blue-500/40' : 'bg-slate-900/20 border-slate-700'}`}>
        <CardContent className="p-3 flex items-center justify-center gap-3">
          <div className={`h-2 w-2 rounded-full animate-pulse ${isPartnerNearby ? 'bg-blue-400' : 'bg-gray-500'}`} />
          <p className={`text-sm font-bold ${isPartnerNearby ? 'text-blue-300' : 'text-gray-500'}`}>
            {isPartnerNearby ? t('missionDetail.partnerNearby') : t('missionDetail.partnerAway')}
          </p>
        </CardContent>
      </Card>

      {/* Google Maps */}
      <a
        href={mapsUrl(mission.businessLatitude, mission.businessLongitude, mission.businessName)}
        target="_blank"
        rel="noopener noreferrer"
      >
        <Button variant="outline" className="w-full border-blue-500/40 text-blue-300 hover:bg-blue-900/20 gap-2">
          <MapPin className="h-4 w-4" />
          {t('missionDetail.goToVenue')}
        </Button>
      </a>

      {/* NFC Verification */}
      <Button
        onClick={onVerifyNfc}
        disabled={isMutating}
        className="w-full bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-700 hover:to-indigo-700 text-white font-bold py-6 text-lg gap-2"
      >
        <Radio className="h-5 w-5" />
        {t('missionDetail.scanNfc')}
      </Button>

      {/* Verification code */}
      {mission.verificationCode && (
        <Card className="bg-slate-800/80 border border-purple-500/40">
          <CardContent className="p-5 text-center">
            <p className="text-purple-300 text-xs font-bold uppercase tracking-widest mb-3">
              {t('missionDetail.verificationCode')}
            </p>
            <button onClick={copyCode} className="group relative w-full">
              <div className="flex items-center justify-center gap-3">
                <span className="text-5xl font-black text-white font-mono tracking-[0.2em]">
                  {mission.verificationCode}
                </span>
                <Copy className="h-5 w-5 text-gray-500 group-hover:text-purple-400 transition-colors" />
              </div>
              <p className="text-gray-500 text-xs mt-2">
                {copied ? t('missionDetail.copiedCode') : t('missionDetail.tapToCopy')}
              </p>
            </button>
            {done && (
              <p className="text-emerald-400 text-sm font-bold mt-3 flex items-center justify-center gap-2">
                <CheckCircle2 className="h-4 w-4" />
                {t('missionDetail.timesUp')}
              </p>
            )}
            {!done && (
              <p className="text-gray-400 text-xs mt-3">{t('missionDetail.showCodeToStaff')}</p>
            )}
          </CardContent>
        </Card>
      )}

      <Button
        onClick={onCancel}
        disabled={isMutating}
        variant="outline"
        className="w-full border-red-500/30 text-red-400 hover:bg-red-900/20"
      >
        <XCircle className="h-4 w-4 mr-2" />
        {t('missionDetail.cancel')}
      </Button>
    </div>
  );
}

function CompletedState({ mission }: { mission: Mission }) {
  const { t } = useTranslation();

  return (
    <div className="space-y-4">
      <motion.div
        initial={{ scale: 0.8, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        transition={{ type: 'spring', bounce: 0.4 }}
      >
        <Card className="bg-purple-900/30 border-2 border-purple-500/60">
          <CardContent className="p-6 text-center">
            <div className="relative inline-block mb-4">
              <div className="absolute inset-0 bg-purple-500 blur-xl opacity-40 rounded-full" />
              <CheckCircle2 className="h-16 w-16 text-purple-400 relative" />
            </div>
            <h2 className="text-2xl font-black text-white mb-1">{t('missionDetail.completed')}</h2>
            <p className="text-emerald-400 text-xl font-bold mb-4">
              {t('missionDetail.completedSubtext', { percent: mission.discountPercent })}
            </p>
            <p className="text-gray-400 text-sm">{t('missionDetail.rewardInfo')}</p>
          </CardContent>
        </Card>
      </motion.div>
    </div>
  );
}

export default function MissionDetailClient() {
  const params = useParams();
  const router = useRouter();
  const { t } = useTranslation();
  const qc = useQueryClient();
  const missionId = Number(params.id);

  const { data: mission, isLoading, isError } = useQuery({
    queryKey: ['mission', missionId],
    queryFn: () => missionApi.getById(missionId),
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === 'InProgress' || status === 'Pending' ? 5000 : false;
    },
    enabled: !isNaN(missionId),
  });

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['mission', missionId] });
    qc.invalidateQueries({ queryKey: ['missions'] });
  };

  const acceptMutation = useMutation({
    mutationFn: () => missionApi.accept(missionId),
    onSuccess: invalidate,
  });

  const cancelMutation = useMutation({
    mutationFn: () => missionApi.cancel(missionId),
    onSuccess: () => { invalidate(); router.push('/missions'); },
  });

  const verifyNfcMutation = useMutation({
    mutationFn: () => {
      const payload = JSON.stringify({
        v: 1,
        bid: mission!.businessId.toString(),
        mid: mission!.id.toString(),
        ts: Math.floor(Date.now() / 1000),
        sig: "mock_sig"
      });
      return missionApi.verifyNfc(missionId, payload);
    },
    onSuccess: invalidate,
  });

  const isMutating = acceptMutation.isPending || cancelMutation.isPending || verifyNfcMutation.isPending;
  const mutationError = acceptMutation.error || cancelMutation.error || verifyNfcMutation.error;

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 text-purple-400 animate-spin" />
      </div>
    );
  }

  if (isError || !mission) {
    return (
      <div className="p-4 pt-16 text-center">
        <AlertCircle className="h-12 w-12 text-red-400 mx-auto mb-4" />
        <p className="text-red-300">{t('common.error')}</p>
        <Button onClick={() => router.push('/missions')} variant="ghost" className="mt-4 text-gray-400">
          {t('common.back')}
        </Button>
      </div>
    );
  }

  const statusKey = (mission.status.charAt(0).toLowerCase() + mission.status.slice(1)) as string;

  const tags: string[] = (() => {
    try { return mission.interestTags ? JSON.parse(mission.interestTags) : []; }
    catch { return []; }
  })();

  const isInProgress = mission.status === 'InProgress' || mission.status === 'Accepted';

  return (
    <div className="min-h-screen pb-28 px-4">
      <div className="max-w-lg mx-auto">
        <div className="flex items-center gap-3 pt-8 pb-4">
          <button onClick={() => router.push('/missions')} className="text-gray-400 hover:text-white p-1">
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div className="flex-1 min-w-0">
            <span className={`text-xs font-bold px-2 py-0.5 rounded-full border ${
              isInProgress ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30' :
              mission.status === 'Completed' ? 'bg-purple-500/20 text-purple-300 border-purple-500/30' :
              'bg-amber-500/20 text-amber-300 border-amber-500/30'
            }`}>
              {t(`missions.status.${statusKey}`)}
            </span>
          </div>
        </div>

        <div className="mb-5">
          <h1 className="text-xl font-black text-white mb-2 leading-tight">{mission.title}</h1>
          <p className="text-gray-400 text-sm leading-relaxed">{mission.description}</p>
        </div>

        <div className="grid grid-cols-3 gap-2 mb-5">
          <div className="bg-slate-800/60 rounded-xl p-3 text-center border border-slate-700">
            <MapPin className="h-4 w-4 text-blue-400 mx-auto mb-1" />
            <p className="text-white text-xs font-bold leading-tight truncate">{mission.businessName}</p>
            <p className="text-gray-500 text-xs">{t('missionDetail.venue')}</p>
          </div>
          <div className="bg-slate-800/60 rounded-xl p-3 text-center border border-slate-700">
            <Zap className="h-4 w-4 text-emerald-400 mx-auto mb-1" />
            <p className="text-emerald-400 text-xs font-black">{mission.discountPercent}%</p>
            <p className="text-gray-500 text-xs">{t('missionDetail.discount')}</p>
          </div>
          <div className="bg-slate-800/60 rounded-xl p-3 text-center border border-slate-700">
            <Lock className="h-4 w-4 text-purple-400 mx-auto mb-1" />
            <p className="text-white text-xs font-bold">{mission.requiredLockMinutes}</p>
            <p className="text-gray-500 text-xs">{t('missionDetail.minutes')}</p>
          </div>
        </div>

        {tags.length > 0 && (
          <div className="flex items-center gap-2 mb-5 flex-wrap">
            <Tag className="h-3.5 w-3.5 text-gray-500" />
            {tags.map(tag => (
              <span key={tag} className="text-xs px-2 py-0.5 rounded-full bg-slate-700 text-gray-300 border border-slate-600">
                {tag}
              </span>
            ))}
          </div>
        )}

        {mutationError && (
          <div className="mb-4 bg-red-900/30 border border-red-500/30 rounded-xl p-3 flex items-center gap-2">
            <AlertCircle className="h-4 w-4 text-red-400 shrink-0" />
            <p className="text-red-300 text-sm">
              {mutationError instanceof Error ? mutationError.message : t('common.error')}
            </p>
          </div>
        )}

        {mission.status === 'Pending' && (
          <PendingState
            mission={mission}
            onAccept={() => acceptMutation.mutate()}
            onCancel={() => cancelMutation.mutate()}
            isMutating={isMutating}
          />
        )}
        {isInProgress && (
          <InProgressState
            mission={mission}
            onCancel={() => cancelMutation.mutate()}
            onVerifyNfc={() => verifyNfcMutation.mutate()}
            isMutating={isMutating}
          />
        )}
        {mission.status === 'Completed' && <CompletedState mission={mission} />}
        {(mission.status === 'Expired' || mission.status === 'Cancelled') && (
          <Card className="bg-slate-800/50 border border-slate-600">
            <CardContent className="p-4 text-center">
              <XCircle className="h-8 w-8 text-gray-500 mx-auto mb-2" />
              <p className="text-gray-400 font-semibold">
                {t(`missions.status.${statusKey}`)}
              </p>
              <Button onClick={() => router.push('/missions')} variant="ghost" className="mt-3 text-gray-400">
                {t('common.back')}
              </Button>
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}
