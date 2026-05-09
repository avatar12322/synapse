package com.synapse.antispoof

import com.getcapacitor.JSObject
import com.getcapacitor.Plugin
import com.getcapacitor.PluginCall
import com.getcapacitor.PluginMethod
import com.getcapacitor.annotation.CapacitorPlugin
import com.google.android.play.core.integrity.IntegrityManagerFactory
import com.google.android.play.core.integrity.StandardIntegrityManager
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

@CapacitorPlugin(name = "AntiSpoof")
class AntiSpoofPlugin : Plugin() {

    private var integrityManager: StandardIntegrityManager? = null
    private var tokenProvider: StandardIntegrityManager.StandardIntegrityTokenProvider? = null

    override fun load() {
        integrityManager = IntegrityManagerFactory.createStandard(context)
        // Warm up the token provider in the background to reduce first-call latency
        CoroutineScope(Dispatchers.IO).launch {
            integrityManager?.prepareIntegrityToken(
                StandardIntegrityManager.PrepareIntegrityTokenRequest.builder()
                    .setCloudProjectNumber(0L) // TODO: set from build config
                    .build()
            )?.addOnSuccessListener { provider ->
                tokenProvider = provider
            }
        }
    }

    @PluginMethod
    fun getAttestationToken(call: PluginCall) {
        val challenge = call.getString("challenge") ?: run {
            call.reject("challenge is required"); return
        }

        val provider = tokenProvider ?: run {
            call.resolve(JSObject().put("platform", "android").put("token", "provider-not-ready"))
            return
        }

        provider.request(
            StandardIntegrityManager.StandardIntegrityTokenRequest.builder()
                .setRequestHash(challenge)
                .build()
        ).addOnSuccessListener { response ->
            val result = JSObject()
                .put("platform", "android")
                .put("token", response.token())
            call.resolve(result)
        }.addOnFailureListener { exc ->
            call.reject("Play Integrity failed: ${exc.message}")
        }
    }
}
