"use client";

import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'framer-motion';
import { Store, CheckCircle2, AlertCircle, Loader2 } from 'lucide-react';
import { missionApi, Mission } from '@/lib/api';
import { authStorage } from '@/lib/auth';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';

export default function BusinessVerifyPage() {
  const { t } = useTranslation();
  const [code, setCode] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [completed, setCompleted] = useState<Mission | null>(null);
  const [isBusiness, setIsBusiness] = useState<boolean | null>(null);

  useEffect(() => {
    setIsBusiness(authStorage.isBusiness() || authStorage.isAdmin());
  }, []);

  if (isBusiness === null) return null; // waiting for client hydration

  if (!isBusiness) {
    return (
      <div className="flex flex-col items-center justify-center min-h-screen px-4 text-center">
        <AlertCircle className="h-12 w-12 text-red-400 mb-4" />
        <p className="text-red-300 font-bold">Access denied. Business account required.</p>
      </div>
    );
  }

  const handleVerify = async (e: React.FormEvent) => {
    e.preventDefault();
    if (code.length !== 6) return;
    setError('');
    setIsLoading(true);
    try {
      const mission = await missionApi.verify(code.trim());
      setCompleted(mission);
      setCode('');
    } catch (err) {
      setError(err instanceof Error ? err.message : t('business.error'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen pb-28 px-4">
      <div className="max-w-md mx-auto">
        {/* Header */}
        <div className="flex items-center gap-3 pt-10 pb-6">
          <div className="relative">
            <div className="absolute inset-0 bg-purple-500 blur-md opacity-40 rounded-full" />
            <Store className="h-7 w-7 text-purple-400 relative" />
          </div>
          <div>
            <h1 className="text-xl font-black text-white">{t('business.verifyTitle')}</h1>
            <p className="text-gray-400 text-sm">{t('business.verifySubtitle')}</p>
          </div>
        </div>

        {/* Success state */}
        {completed && (
          <motion.div
            initial={{ scale: 0.9, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            transition={{ type: 'spring', bounce: 0.4 }}
            className="mb-5"
          >
            <Card className="bg-emerald-900/20 border-2 border-emerald-500/50">
              <CardContent className="p-5 text-center">
                <CheckCircle2 className="h-12 w-12 text-emerald-400 mx-auto mb-3" />
                <p className="text-emerald-300 font-black text-xl mb-1">{t('business.success')}</p>
                <p className="text-gray-400 text-sm">
                  {t('business.successSubtext', { id: completed.id })}
                </p>
              </CardContent>
            </Card>
          </motion.div>
        )}

        {/* Verify form */}
        <Card className="bg-slate-800/60 border border-purple-500/20">
          <CardHeader>
            <CardTitle className="text-white text-base">{t('business.code')}</CardTitle>
            <CardDescription className="text-gray-400">
              {t('business.verifySubtitle')}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleVerify} className="space-y-4">
              {error && (
                <div className="bg-red-900/30 border border-red-500/30 rounded-xl p-3 flex items-center gap-2">
                  <AlertCircle className="h-4 w-4 text-red-400 shrink-0" />
                  <p className="text-red-300 text-sm">{error}</p>
                </div>
              )}

              <div>
                <Input
                  value={code}
                  onChange={e => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  placeholder={t('business.codePlaceholder')}
                  inputMode="numeric"
                  pattern="[0-9]{6}"
                  maxLength={6}
                  disabled={isLoading}
                  className="bg-slate-700 border-2 border-purple-400/50 text-white text-center text-3xl font-black tracking-[0.3em] placeholder:text-gray-600 placeholder:text-base placeholder:tracking-normal focus:border-purple-300 h-16"
                />
              </div>

              <Button
                type="submit"
                disabled={isLoading || code.length !== 6}
                className="w-full bg-gradient-to-r from-purple-600 to-blue-600 hover:from-purple-700 hover:to-blue-700 text-white font-bold py-5 text-base"
              >
                {isLoading
                  ? <><Loader2 className="h-5 w-5 mr-2 animate-spin" />{t('common.loading')}</>
                  : t('business.verify')
                }
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
