"use client";

import { useState, useEffect, useRef } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'framer-motion';
import { ArrowLeft, Zap, MapPin, Loader2, CheckCircle2, AlertCircle, Radio } from 'lucide-react';
import { matchApi, missionApi } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

type Step = 'form' | 'locating' | 'searching' | 'matched' | 'no_venues' | 'error';

const CATEGORIES = ['any', 'coffee', 'lunch', 'sports', 'culture', 'learning', 'networking'] as const;

export default function MatchPage() {
  const router = useRouter();
  const { t } = useTranslation();

  const [step, setStep] = useState<Step>('form');
  const [category, setCategory] = useState<string>('any');
  const [errorMsg, setErrorMsg] = useState('');
  const [missionId, setMissionId] = useState<number | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Poll missions list while in queue — backend creates the mission for us when partner joins
  useEffect(() => {
    if (step !== 'searching') {
      if (pollRef.current) clearInterval(pollRef.current);
      return;
    }

    const check = async () => {
      try {
        const missions = await missionApi.getAll();
        const active = Array.isArray(missions)
          ? missions.find(m => m.status === 'Pending' || m.status === 'InProgress')
          : null;
        if (active) {
          if (pollRef.current) clearInterval(pollRef.current);
          setMissionId(active.id);
          setStep('matched');
          setTimeout(() => router.push(`/missions/${active.id}`), 1500);
        }
      } catch { /* ignore, keep polling */ }
    };

    check(); // immediate first check
    pollRef.current = setInterval(check, 3000);
    return () => { if (pollRef.current) clearInterval(pollRef.current); };
  }, [step, router]);

  const startMatch = async () => {
    setStep('locating');

    let lat: number;
    let lng: number;

    try {
      const pos = await new Promise<GeolocationPosition>((resolve, reject) =>
        navigator.geolocation.getCurrentPosition(resolve, reject, {
          timeout: 10000,
          maximumAge: 60000,
        })
      );
      lat = pos.coords.latitude;
      lng = pos.coords.longitude;
    } catch {
      setErrorMsg(t('match.locationError'));
      setStep('error');
      return;
    }

    setStep('searching');

    try {
      const result = await matchApi.request(
        lat,
        lng,
        category === 'any' ? undefined : category,
        2000
      );

      if (result.status === 'matched' && result.missionId) {
        setMissionId(result.missionId);
        setStep('matched');
        setTimeout(() => router.push(`/missions/${result.missionId}`), 1500);
      } else if (result.status === 'no_venues') {
        setStep('no_venues');
      }
      // status === 'searching' — already set above, polling useEffect kicks in
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : t('common.error'));
      setStep('error');
    }
  };

  return (
    <div className="min-h-screen pb-28 px-4">
      <div className="max-w-lg mx-auto">
        {/* Header */}
        <div className="flex items-center gap-3 pt-8 pb-6">
          <button onClick={() => router.push('/missions')} className="text-gray-400 hover:text-white p-1">
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div>
            <h1 className="text-xl font-black text-white">{t('match.title')}</h1>
            <p className="text-gray-400 text-sm">{t('match.subtitle')}</p>
          </div>
        </div>

        <AnimatePresence mode="wait">
          {/* Form */}
          {step === 'form' && (
            <motion.div
              key="form"
              initial={{ opacity: 0, y: 16 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -16 }}
              className="space-y-5"
            >
              {/* Category picker */}
              <div>
                <p className="text-sm font-bold text-gray-300 mb-3">{t('match.category')}</p>
                <div className="grid grid-cols-4 gap-2">
                  {CATEGORIES.map(cat => (
                    <button
                      key={cat}
                      onClick={() => setCategory(cat)}
                      className={`rounded-xl py-2 px-1 text-xs font-bold border transition-all ${
                        category === cat
                          ? 'bg-purple-600 border-purple-400 text-white shadow-lg shadow-purple-500/20'
                          : 'bg-slate-800/60 border-slate-600 text-gray-400 hover:border-purple-500/50 hover:text-gray-200'
                      }`}
                    >
                      {t(`match.categories.${cat}`)}
                    </button>
                  ))}
                </div>
              </div>

              {/* Info card */}
              <Card className="bg-slate-800/50 border border-purple-500/20">
                <CardContent className="p-4 space-y-2">
                  <div className="flex items-start gap-3">
                    <MapPin className="h-4 w-4 text-purple-400 mt-0.5 shrink-0" />
                    <p className="text-gray-400 text-sm">Your location is used to find matches and nearby venues. It is not stored beyond this session.</p>
                  </div>
                  <div className="flex items-start gap-3">
                    <Radio className="h-4 w-4 text-blue-400 mt-0.5 shrink-0" />
                    <p className="text-gray-400 text-sm">If no one is searching right now, you'll be added to the queue and notified when matched.</p>
                  </div>
                </CardContent>
              </Card>

              <Button
                onClick={startMatch}
                className="w-full bg-gradient-to-r from-purple-600 to-blue-600 hover:from-purple-700 hover:to-blue-700 text-white font-bold py-6 text-lg gap-2"
              >
                <Zap className="h-5 w-5" />
                {t('match.startSearch')}
              </Button>
            </motion.div>
          )}

          {/* Locating */}
          {step === 'locating' && (
            <motion.div
              key="locating"
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              exit={{ opacity: 0, scale: 0.95 }}
            >
              <Card className="bg-slate-800/50 border border-purple-500/20">
                <CardContent className="flex flex-col items-center justify-center py-16 text-center">
                  <div className="relative mb-6">
                    <div className="absolute inset-0 bg-blue-500 blur-xl opacity-30 rounded-full animate-pulse" />
                    <MapPin className="h-14 w-14 text-blue-400 relative" />
                  </div>
                  <p className="text-white font-bold text-lg">{t('match.findingLocation')}</p>
                </CardContent>
              </Card>
            </motion.div>
          )}

          {/* Searching / in queue */}
          {step === 'searching' && (
            <motion.div
              key="searching"
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              exit={{ opacity: 0, scale: 0.95 }}
            >
              <Card className="bg-slate-800/50 border border-purple-500/30">
                <CardContent className="flex flex-col items-center justify-center py-16 text-center">
                  <div className="relative mb-6">
                    <div className="absolute inset-0 bg-purple-500 blur-xl opacity-30 rounded-full animate-pulse" />
                    <Loader2 className="h-14 w-14 text-purple-400 relative animate-spin" />
                  </div>
                  <p className="text-white font-bold text-lg mb-2">{t('match.searching')}</p>
                  <p className="text-gray-400 text-sm">{t('match.searchingSubtext')}</p>
                  <Button
                    onClick={() => router.push('/missions')}
                    variant="ghost"
                    className="mt-6 text-gray-400 hover:text-white"
                  >
                    {t('common.back')}
                  </Button>
                </CardContent>
              </Card>
            </motion.div>
          )}

          {/* Matched */}
          {step === 'matched' && (
            <motion.div
              key="matched"
              initial={{ opacity: 0, scale: 0.8 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ type: 'spring', bounce: 0.5 }}
            >
              <Card className="bg-emerald-900/20 border-2 border-emerald-500/50">
                <CardContent className="flex flex-col items-center justify-center py-16 text-center">
                  <div className="relative mb-6">
                    <div className="absolute inset-0 bg-emerald-500 blur-xl opacity-40 rounded-full" />
                    <CheckCircle2 className="h-16 w-16 text-emerald-400 relative" />
                  </div>
                  <p className="text-emerald-300 font-black text-2xl mb-2">{t('match.matched')}</p>
                  <p className="text-gray-400 text-sm">{t('match.matchedSubtext')}</p>
                  {missionId && (
                    <Button
                      onClick={() => router.push(`/missions/${missionId}`)}
                      className="mt-6 bg-gradient-to-r from-emerald-600 to-teal-600 text-white font-bold"
                    >
                      {t('match.openMission')}
                    </Button>
                  )}
                </CardContent>
              </Card>
            </motion.div>
          )}

          {/* No venues */}
          {step === 'no_venues' && (
            <motion.div
              key="no_venues"
              initial={{ opacity: 0, y: 16 }}
              animate={{ opacity: 1, y: 0 }}
            >
              <Card className="bg-amber-900/20 border border-amber-500/30">
                <CardContent className="flex flex-col items-center justify-center py-14 text-center">
                  <MapPin className="h-12 w-12 text-amber-400/60 mb-4" />
                  <p className="text-amber-300 font-bold text-lg mb-2">{t('match.noVenues')}</p>
                  <p className="text-gray-400 text-sm mb-6">{t('match.noVenuesSubtext')}</p>
                  <Button
                    onClick={() => setStep('form')}
                    className="bg-gradient-to-r from-purple-600 to-blue-600 text-white font-bold"
                  >
                    {t('match.tryAgain')}
                  </Button>
                </CardContent>
              </Card>
            </motion.div>
          )}

          {/* Error */}
          {step === 'error' && (
            <motion.div
              key="error"
              initial={{ opacity: 0, y: 16 }}
              animate={{ opacity: 1, y: 0 }}
            >
              <Card className="bg-red-900/20 border border-red-500/30">
                <CardContent className="flex flex-col items-center justify-center py-14 text-center">
                  <AlertCircle className="h-12 w-12 text-red-400 mb-4" />
                  <p className="text-red-300 font-bold text-lg mb-2">{t('common.error')}</p>
                  <p className="text-gray-400 text-sm mb-6">{errorMsg}</p>
                  <Button
                    onClick={() => setStep('form')}
                    className="bg-gradient-to-r from-purple-600 to-blue-600 text-white font-bold"
                  >
                    {t('match.tryAgain')}
                  </Button>
                </CardContent>
              </Card>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </div>
  );
}
