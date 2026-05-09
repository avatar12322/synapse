export interface NfcPresencePlugin {
  /**
   * Start scanning for an NFC NDEF tag.
   * Resolves when a valid tag is found and read.
   * Rejects on timeout, NFC unavailable, or user cancellation.
   */
  startScan(options: ScanOptions): Promise<ScanResult>;

  /** Stop an in-progress scan session. */
  stopScan(): Promise<void>;
}

export interface ScanOptions {
  /** Mission ID expected in the tag payload — validated client-side for UX, server-side for security. */
  missionId: string;
  /** Business ID expected in the tag payload. */
  expectedBusinessId: string;
  /** Scan timeout in milliseconds. Default: 30000. */
  timeoutMs?: number;
}

export interface ScanResult {
  /** True when the tag was read successfully. Server-side HMAC verification is done separately. */
  verified: boolean;
  /** businessId extracted from the NDEF payload. */
  businessId: string;
  /** missionId extracted from the NDEF payload. */
  missionId: string;
  /** ISO 8601 timestamp from the tag payload (Unix epoch as string). */
  tagTimestamp: string;
  /**
   * Raw JSON string of the NDEF payload.
   * Pass this to POST /api/missions/{id}/verify-nfc for server-side HMAC verification.
   */
  rawPayload: string;
}
