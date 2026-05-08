"use client";

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useTranslation } from 'react-i18next';
import { authStorage } from '@/lib/auth';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Zap, Loader2, Shield } from 'lucide-react';
import { API_BASE_URL } from '@/lib/config';
import LanguageSwitcher from '@/components/LanguageSwitcher';

export default function LoginPage() {
  const router = useRouter();
  const { t } = useTranslation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);
    try {
      const response = await fetch(`${API_BASE_URL}/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ email, password }),
      });
      if (!response.ok) throw new Error(await response.text() || 'Login failed');
      const data = await response.json();
      authStorage.setUser(data.user);
      router.push('/');
    } catch (err: any) {
      setError(err.message || 'Failed to login. Please check your credentials.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-purple-900/40 to-slate-900 flex items-center justify-center p-4">
      <div className="absolute top-4 right-4 z-50"><LanguageSwitcher /></div>

      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <div className="relative inline-block">
            <div className="absolute inset-0 bg-gradient-to-r from-purple-500 to-blue-500 blur-xl opacity-50 rounded-full" />
            <Zap className="h-16 w-16 text-purple-400 relative" />
          </div>
          <h1 className="text-5xl font-black text-transparent bg-clip-text bg-gradient-to-r from-purple-400 to-blue-400 mt-4 mb-2">
            Synapse
          </h1>
          <p className="text-purple-300 font-medium">{t('auth.loginSubtitle')}</p>
        </div>

        <Card className="bg-slate-800/80 border-2 border-purple-500/50 shadow-2xl backdrop-blur">
          <CardHeader className="text-center">
            <div className="flex justify-center mb-3">
              <Shield className="h-10 w-10 text-purple-400" />
            </div>
            <CardTitle className="text-2xl font-black text-purple-300">{t('auth.login')}</CardTitle>
            <CardDescription className="text-gray-400">{t('auth.welcomeBack')}</CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleLogin} className="space-y-5">
              {error && (
                <div className="bg-red-900/50 border border-red-500 text-red-200 px-4 py-3 rounded-lg text-sm">
                  {error}
                </div>
              )}
              <div>
                <label className="text-sm font-bold text-purple-300 block mb-2">{t('auth.email')}</label>
                <Input type="email" value={email} onChange={e => setEmail(e.target.value)}
                  placeholder="your@email.com" required disabled={isLoading}
                  className="bg-slate-700 border-2 border-purple-400/50 text-white placeholder:text-gray-500 focus:border-purple-300" />
              </div>
              <div>
                <label className="text-sm font-bold text-purple-300 block mb-2">{t('auth.password')}</label>
                <Input type="password" value={password} onChange={e => setPassword(e.target.value)}
                  placeholder="••••••••" required disabled={isLoading}
                  className="bg-slate-700 border-2 border-purple-400/50 text-white placeholder:text-gray-500 focus:border-purple-300" />
              </div>
              <Button type="submit" disabled={isLoading}
                className="w-full bg-gradient-to-r from-purple-600 to-blue-600 hover:from-purple-700 hover:to-blue-700 text-white font-bold py-6 text-lg">
                {isLoading ? <><Loader2 className="h-5 w-5 mr-2 animate-spin" />{t('common.loading')}</> : t('auth.loginButton')}
              </Button>
              <p className="text-center text-sm text-gray-400 pt-2">
                {t('auth.noAccount')}{' '}
                <Link href="/register" className="text-purple-400 hover:text-purple-300 font-bold hover:underline">
                  {t('auth.signUp')}
                </Link>
              </p>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
