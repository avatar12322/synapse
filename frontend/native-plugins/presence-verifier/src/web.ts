import { WebPlugin } from '@capacitor/core';
import type { PresenceVerifierPlugin, PresenceStatus, VerifyOptions } from './definitions';

export class PresenceVerifierWeb extends WebPlugin implements PresenceVerifierPlugin {
  async startVerification(options: VerifyOptions): Promise<void> {
    console.warn('[PresenceVerifier] Web stub: startVerification', options);
  }

  async stopVerification(): Promise<void> {
    console.warn('[PresenceVerifier] Web stub: stopVerification');
  }

  async getStatus(): Promise<PresenceStatus> {
    return { insideGeofence: false, activityType: 'unknown', confirmed: false };
  }
}
