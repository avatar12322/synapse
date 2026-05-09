import { WebPlugin } from '@capacitor/core';
import type { ScreenLockGuardPlugin, StartGuardOptions } from './definitions';

/** Browser stub — real implementation is in Swift / Kotlin native code. */
export class ScreenLockGuardWeb extends WebPlugin implements ScreenLockGuardPlugin {
  private guardStart: number | null = null;

  async startGuard(options: StartGuardOptions): Promise<void> {
    console.warn('[ScreenLockGuard] Web stub: startGuard', options);
    this.guardStart = Date.now();
  }

  async stopGuard(): Promise<void> {
    console.warn('[ScreenLockGuard] Web stub: stopGuard');
    this.guardStart = null;
  }

  async isLocked(): Promise<{ locked: boolean }> {
    return { locked: false };
  }

  async getLockedSeconds(): Promise<{ seconds: number }> {
    if (!this.guardStart) return { seconds: 0 };
    return { seconds: Math.floor((Date.now() - this.guardStart) / 1000) };
  }
}
