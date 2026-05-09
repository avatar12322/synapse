package com.synapse.screenlockguard

import android.app.KeyguardManager
import android.content.Context
import android.content.Intent
import com.getcapacitor.JSObject
import com.getcapacitor.Plugin
import com.getcapacitor.PluginCall
import com.getcapacitor.PluginMethod
import com.getcapacitor.annotation.CapacitorPlugin

// Android 14+: foreground service requires foregroundServiceType="health" in manifest + runtime permission.
// The ForegroundLockService handles SCREEN_OFF/ON broadcasts and tracks lock time.
// (CLAUDE.md §4.2)

@CapacitorPlugin(name = "ScreenLockGuard")
class ScreenLockGuardPlugin : Plugin() {

    private var guardStartMs: Long? = null

    @PluginMethod
    fun startGuard(call: PluginCall) {
        val missionId = call.getInt("missionId") ?: run {
            call.reject("missionId is required"); return
        }
        val requiredMinutes = call.getInt("requiredMinutes") ?: 30

        guardStartMs = System.currentTimeMillis()

        val intent = Intent(context, ScreenLockForegroundService::class.java).apply {
            putExtra("missionId", missionId)
            putExtra("requiredMinutes", requiredMinutes)
        }
        context.startForegroundService(intent)
        call.resolve()
    }

    @PluginMethod
    fun stopGuard(call: PluginCall) {
        guardStartMs = null
        context.stopService(Intent(context, ScreenLockForegroundService::class.java))
        call.resolve()
    }

    @PluginMethod
    fun isLocked(call: PluginCall) {
        val km = context.getSystemService(Context.KEYGUARD_SERVICE) as KeyguardManager
        val locked = km.isKeyguardLocked
        val result = JSObject().put("locked", locked)
        call.resolve(result)
    }

    @PluginMethod
    fun getLockedSeconds(call: PluginCall) {
        val start = guardStartMs
        val seconds = if (start != null) ((System.currentTimeMillis() - start) / 1000).toInt() else 0
        val result = JSObject().put("seconds", seconds)
        call.resolve(result)
    }
}
