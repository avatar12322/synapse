import { registerPlugin } from '@capacitor/core';
import type { PresenceVerifierPlugin } from './definitions';

const PresenceVerifier = registerPlugin<PresenceVerifierPlugin>('PresenceVerifier', {
  web: () => import('./web').then((m) => new m.PresenceVerifierWeb()),
});

export * from './definitions';
export { PresenceVerifier };
