export interface AntiSpoofPlugin {
  /**
   * Generate an attestation token for the current device.
   * iOS: DCAppAttestService — returns a base64-encoded assertion.
   * Android: Play Integrity API — returns a JSON integrity token.
   * Backend validates the token before allowing /missions/complete.
   */
  getAttestationToken(options: AttestOptions): Promise<AttestationResult>;
}

export interface AttestOptions {
  /** Challenge nonce from the server (hex string). */
  challenge: string;
}

export interface AttestationResult {
  platform: 'ios' | 'android' | 'web';
  token: string;
}
