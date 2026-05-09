import { WebPlugin } from '@capacitor/core';
import type { AntiSpoofPlugin, AttestOptions, AttestationResult } from './definitions';

export class AntiSpoofWeb extends WebPlugin implements AntiSpoofPlugin {
  async getAttestationToken(options: AttestOptions): Promise<AttestationResult> {
    console.warn('[AntiSpoof] Web stub — no real attestation on web', options);
    return { platform: 'web', token: 'web-stub-no-attestation' };
  }
}
