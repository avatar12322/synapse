'use client';

import { useTranslation } from 'react-i18next';
import { useState } from 'react';
import { API_BASE_URL } from '@/lib/config';
import { authStorage } from '@/lib/auth';

const LANGUAGES = [
  { code: 'en', name: 'English', flag: '🇬🇧' },
  { code: 'pl', name: 'Polski', flag: '🇵🇱' },
];

export default function LanguageSwitcher() {
  const { i18n } = useTranslation();
  const [isOpen, setIsOpen] = useState(false);
  const current = LANGUAGES.find(l => l.code === i18n.language) ?? LANGUAGES[0];

  const handleChange = async (code: string) => {
    setIsOpen(false);
    await i18n.changeLanguage(code);
    if (authStorage.getUser()) {
      try {
        await fetch(`${API_BASE_URL}/auth/language`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({ language: code }),
        });
        const user = authStorage.getUser();
        if (user) authStorage.setUser({ ...user, language: code });
      } catch { /* non-critical */ }
    }
  };

  return (
    <div className="relative">
      <button onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-1 px-2 py-1.5 rounded-lg bg-purple-600/20 hover:bg-purple-600/30 transition-colors border border-purple-500/30">
        <span className="text-base font-bold text-purple-300">{current.flag}</span>
      </button>
      {isOpen && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setIsOpen(false)} />
          <div className="absolute right-0 mt-2 w-48 rounded-lg bg-slate-800 shadow-xl border border-purple-500/50 z-50 overflow-hidden">
            {LANGUAGES.map(lang => (
              <button key={lang.code} onClick={() => handleChange(lang.code)}
                className={`w-full flex items-center gap-3 px-4 py-3 hover:bg-purple-600/20 transition-colors ${current.code === lang.code ? 'bg-purple-600/30 border-l-4 border-purple-400' : ''}`}>
                <span className="text-lg font-bold text-purple-300">{lang.flag}</span>
                <span className="text-sm font-medium text-white">{lang.name}</span>
              </button>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
