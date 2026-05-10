"use client";

import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { 
  Store, CreditCard, Save, Loader2, CheckCircle2, 
  AlertCircle, ArrowLeft, ExternalLink, Globe 
} from 'lucide-react';
import { businessApi, stripeApi, Business, UpdateBusinessRequest } from '@/lib/api';
import { authStorage } from '@/lib/auth';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';

export default function BusinessSettingsPage() {
  const { t } = useTranslation();
  const router = useRouter();
  
  const [business, setBusiness] = useState<Business | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isStripeLoading, setIsStripeLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const [formData, setFormData] = useState<UpdateBusinessRequest>({
    name: '',
    address: '',
    city: '',
    category: ''
  });

  useEffect(() => {
    if (!authStorage.isBusiness() && !authStorage.isAdmin()) {
      router.push('/missions');
      return;
    }

    const loadBusiness = async () => {
      try {
        const b = await businessApi.getMine();
        setBusiness(b);
        setFormData({
          name: b.name,
          address: b.address,
          city: b.city,
          category: b.category
        });
      } catch (err) {
        setError(t('common.error'));
      } finally {
        setIsLoading(false);
      }
    };

    loadBusiness();
  }, [router, t]);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!business) return;
    
    setIsSaving(true);
    setError('');
    setSuccess('');
    
    try {
      const updated = await businessApi.update(business.id, formData);
      setBusiness(updated);
      setSuccess(t('common.save'));
      setTimeout(() => setSuccess(''), 3000);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('common.error'));
    } finally {
      setIsSaving(false);
    }
  };

  const handleStripeOnboard = async () => {
    setIsStripeLoading(true);
    setError('');
    try {
      const { url } = await stripeApi.onboard();
      window.location.href = url;
    } catch (err) {
      setError(err instanceof Error ? err.message : t('common.error'));
      setIsStripeLoading(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 text-purple-400 animate-spin" />
      </div>
    );
  }

  return (
    <div className="min-h-screen pb-28 px-4 bg-slate-950 text-white">
      <div className="max-w-2xl mx-auto">
        {/* Header */}
        <div className="flex items-center gap-4 pt-10 pb-8">
          <button onClick={() => router.back()} className="p-2 rounded-full bg-slate-900 hover:bg-slate-800 transition-colors">
            <ArrowLeft className="h-5 w-5 text-gray-400" />
          </button>
          <div>
            <h1 className="text-2xl font-black text-white">{t('business.settingsTitle')}</h1>
            <p className="text-gray-400 text-sm">{business?.name}</p>
          </div>
        </div>

        {error && (
          <div className="mb-6 bg-red-900/30 border border-red-500/30 rounded-xl p-4 flex items-center gap-3">
            <AlertCircle className="h-5 w-5 text-red-400 shrink-0" />
            <p className="text-red-300 text-sm">{error}</p>
          </div>
        )}

        {success && (
          <div className="mb-6 bg-emerald-900/30 border border-emerald-500/30 rounded-xl p-4 flex items-center gap-3">
            <CheckCircle2 className="h-5 w-5 text-emerald-400 shrink-0" />
            <p className="text-emerald-300 text-sm">{success}</p>
          </div>
        )}

        <div className="space-y-6">
          {/* Stripe Card */}
          <Card className="bg-slate-900 border-purple-500/20 overflow-hidden">
            <div className="h-1 bg-gradient-to-r from-purple-600 to-blue-600" />
            <CardHeader>
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <CreditCard className="h-5 w-5 text-blue-400" />
                  <CardTitle className="text-white text-lg">{t('business.stripeOnboarding')}</CardTitle>
                </div>
                <div className={`px-2 py-0.5 rounded-full text-[10px] font-black uppercase tracking-wider ${
                  business?.isActive ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' : 'bg-amber-500/20 text-amber-400 border border-amber-500/30'
                }`}>
                  {business?.isActive ? t('business.stripeStatusEnabled') : t('business.stripeStatusDisabled')}
                </div>
              </div>
              <CardDescription className="text-gray-400 mt-1">
                Configure your payouts and verify your identity to accept missions.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Button 
                onClick={handleStripeOnboard}
                disabled={isStripeLoading}
                className="w-full bg-blue-600 hover:bg-blue-700 text-white font-bold gap-2 py-6"
              >
                {isStripeLoading ? <Loader2 className="h-5 w-5 animate-spin" /> : <ExternalLink className="h-5 w-5" />}
                {t('business.stripeOnboardButton')}
              </Button>
            </CardContent>
          </Card>

          {/* Profile Form */}
          <Card className="bg-slate-900 border-slate-800">
            <CardHeader>
              <div className="flex items-center gap-3">
                <Store className="h-5 w-5 text-purple-400" />
                <CardTitle className="text-white text-lg">{t('business.editProfile')}</CardTitle>
              </div>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleSave} className="space-y-4">
                <div className="space-y-2">
                  <label className="text-xs font-bold text-gray-500 uppercase tracking-wider">{t('business.businessName')}</label>
                  <Input 
                    value={formData.name}
                    onChange={e => setFormData({...formData, name: e.target.value})}
                    className="bg-slate-800 border-slate-700 text-white"
                  />
                </div>
                
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <label className="text-xs font-bold text-gray-500 uppercase tracking-wider">{t('business.businessAddress')}</label>
                    <Input 
                      value={formData.address}
                      onChange={e => setFormData({...formData, address: e.target.value})}
                      className="bg-slate-800 border-slate-700 text-white"
                    />
                  </div>
                  <div className="space-y-2">
                    <label className="text-xs font-bold text-gray-500 uppercase tracking-wider">{t('business.businessCity')}</label>
                    <Input 
                      value={formData.city}
                      onChange={e => setFormData({...formData, city: e.target.value})}
                      className="bg-slate-800 border-slate-700 text-white"
                    />
                  </div>
                </div>

                <div className="pt-4">
                  <Button 
                    type="submit"
                    disabled={isSaving}
                    className="w-full bg-gradient-to-r from-purple-600 to-blue-600 hover:from-purple-700 hover:to-blue-700 text-white font-bold gap-2 py-6"
                  >
                    {isSaving ? <Loader2 className="h-5 w-5 animate-spin" /> : <Save className="h-5 w-5" />}
                    {t('business.saveChanges')}
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
