export interface PresenceVerifierPlugin {
  /**
   * Start monitoring presence at a venue geofence.
   * iOS: CLLocationManager geofence + CMMotionActivityManager (STILL detection).
   * Android: GeofencingClient + ActivityRecognitionClient.
   */
  startVerification(options: VerifyOptions): Promise<void>;

  stopVerification(): Promise<void>;

  /** Returns current presence status. */
  getStatus(): Promise<PresenceStatus>;
}

export interface VerifyOptions {
  missionId: number;
  venueLat: number;
  venueLng: number;
  /** Geofence radius in metres. Default 100. */
  radiusMetres?: number;
}

export interface PresenceStatus {
  insideGeofence: boolean;
  activityType: 'still' | 'walking' | 'running' | 'vehicle' | 'unknown';
  /** True only when insideGeofence AND activityType === 'still' */
  confirmed: boolean;
}
