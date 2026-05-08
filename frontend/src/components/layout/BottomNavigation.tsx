"use client";

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import { motion, useReducedMotion } from 'framer-motion';
import { Home, Map, Users, User } from 'lucide-react';

const NAV_LINKS = [
  { href: '/', icon: Home, labelKey: 'nav.home', color: 'text-blue-400' },
  { href: '/missions', icon: Map, labelKey: 'nav.missions', color: 'text-purple-400' },
  { href: '/friends', icon: Users, labelKey: 'nav.friends', color: 'text-green-400' },
  { href: '/profile', icon: User, labelKey: 'nav.profile', color: 'text-yellow-400' },
] as const;

export default function BottomNavigation() {
  const pathname = usePathname();
  const { t } = useTranslation();
  const shouldReduceMotion = useReducedMotion();

  if (pathname.startsWith('/login') || pathname.startsWith('/register')) return null;

  return (
    <nav className="bg-gradient-to-r from-slate-800 via-gray-800 to-slate-800 border-t-2 border-purple-500 shadow-2xl backdrop-blur-lg">
      <div className="flex justify-around items-center h-16 max-w-6xl mx-auto px-2">
        {NAV_LINKS.map(({ href, icon: Icon, labelKey, color }) => {
          const isActive = pathname === href || (href !== '/' && pathname.startsWith(href));
          return (
            <Link
              key={href}
              href={href}
              className="flex flex-col items-center justify-center flex-1 h-full relative group py-2"
            >
              <motion.div
                className="p-1"
                whileHover={shouldReduceMotion ? {} : { scale: 1.05 }}
                whileTap={shouldReduceMotion ? {} : { scale: 0.95 }}
                transition={{ duration: 0.1 }}
              >
                <Icon className={`h-6 w-6 transition-colors duration-200 ${isActive ? color : 'text-gray-400 group-hover:text-purple-300'}`} />
              </motion.div>
              <span className={`text-xs font-medium transition-colors duration-200 ${isActive ? color : 'text-gray-400 group-hover:text-purple-300'}`}>
                {t(labelKey)}
              </span>
              {isActive && (
                <motion.div
                  layoutId="activeIndicator"
                  className="absolute bottom-0 left-1/2 -translate-x-1/2 w-8 h-1 bg-gradient-to-r from-purple-500 to-blue-500 rounded-full"
                  transition={{ duration: 0.2 }}
                />
              )}
            </Link>
          );
        })}
      </div>
    </nav>
  );
}
