export interface ScreenLockGuardPlugin {
  /**
   * Start enforcing screen lock for a mission.
   * iOS: FamilyControls shield + DeviceActivityMonitor extension.
   * Android: Foreground Service (FOREGROUND_SERVICE_HEALTH) + KeyguardManager.
   */
  startGuard(options: StartGuardOptions): Promise<void>;

  /** Release the lock guard when mission completes or cancels. */
  stopGuard(): Promise<void>;

  /** Returns whether the screen is currently locked. */
  isLocked(): Promise<{ locked: boolean }>;

  /** Cumulative seconds the screen has been locked since startGuard(). */
  getLockedSeconds(): Promise<{ seconds: number }>;
}

export interface StartGuardOptions {
  missionId: number;
  /** Required lock duration in minutes. Used to display countdown. */
  requiredMinutes: number;
}
