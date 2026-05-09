import { registerPlugin } from '@capacitor/core';
import type { AntiSpoofPlugin } from './definitions';

const AntiSpoof = registerPlugin<AntiSpoofPlugin>('AntiSpoof', {
  web: () => import('./web').then((m) => new m.AntiSpoofWeb()),
});

export * from './definitions';
export { AntiSpoof };
