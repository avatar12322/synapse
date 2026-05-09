import { registerPlugin } from '@capacitor/core';
import type { NfcPresencePlugin } from './definitions';

const NfcPresence = registerPlugin<NfcPresencePlugin>('NfcPresence', {
  web: () => import('./web').then((m) => new m.NfcPresenceWeb()),
});

export * from './definitions';
export { NfcPresence };
