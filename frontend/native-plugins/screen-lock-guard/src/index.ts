import { registerPlugin } from '@capacitor/core';
import type { ScreenLockGuardPlugin } from './definitions';

const ScreenLockGuard = registerPlugin<ScreenLockGuardPlugin>('ScreenLockGuard', {
  web: () => import('./web').then((m) => new m.ScreenLockGuardWeb()),
});

export * from './definitions';
export { ScreenLockGuard };
