import { WebPlugin } from '@capacitor/core';
import type { NfcPresencePlugin, ScanOptions, ScanResult } from './definitions';

export class NfcPresenceWeb extends WebPlugin implements NfcPresencePlugin {
  async startScan(_options: ScanOptions): Promise<ScanResult> {
    console.warn('[NfcPresence] Web stub: NFC is not available in the browser. Use a physical device.');
    throw this.unimplemented('NFC scanning requires a native device.');
  }

  async stopScan(): Promise<void> {
    console.warn('[NfcPresence] Web stub: stopScan called in browser context.');
  }
}
