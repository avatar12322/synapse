package com.synapse.nfcpresence

import android.app.Activity
import android.nfc.NfcAdapter
import android.nfc.Tag
import android.nfc.tech.Ndef
import com.getcapacitor.JSObject
import com.getcapacitor.Plugin
import com.getcapacitor.PluginCall
import com.getcapacitor.PluginMethod
import com.getcapacitor.annotation.CapacitorPlugin
import org.json.JSONObject
import java.util.concurrent.Executors

// NDEF payload format:
// { "v": 1, "bid": "<businessId>", "mid": "<missionId>", "ts": <unixEpoch>, "sig": "<hmac-hex>" }
// Server-side HMAC-SHA256 verification via POST /api/missions/{id}/verify-nfc

@CapacitorPlugin(name = "NfcPresence")
class NfcPresencePlugin : Plugin() {

    private var nfcAdapter: NfcAdapter? = null
    private var pendingCall: PluginCall? = null
    private var timeoutRunnable: Runnable? = null
    private val executor = Executors.newSingleThreadExecutor()

    private val readerCallback = NfcAdapter.ReaderCallback { tag ->
        handleTag(tag)
    }

    @PluginMethod
    fun startScan(call: PluginCall) {
        val activity = activity ?: run {
            call.reject("Activity not available.")
            return
        }

        nfcAdapter = NfcAdapter.getDefaultAdapter(context)
        if (nfcAdapter == null || !nfcAdapter!!.isEnabled) {
            call.reject("NFC is not available or disabled on this device.")
            return
        }

        val missionId = call.getString("missionId") ?: run {
            call.reject("missionId is required.")
            return
        }
        val expectedBusinessId = call.getString("expectedBusinessId") ?: run {
            call.reject("expectedBusinessId is required.")
            return
        }
        val timeoutMs = call.getInt("timeoutMs") ?: 30000

        pendingCall = call
        call.setKeepAlive(true)

        val flags = NfcAdapter.FLAG_READER_NFC_A or
                NfcAdapter.FLAG_READER_NFC_B or
                NfcAdapter.FLAG_READER_NFC_F or
                NfcAdapter.FLAG_READER_NFC_V or
                NfcAdapter.FLAG_READER_SKIP_NDEF_CHECK.inv()

        nfcAdapter!!.enableReaderMode(activity, readerCallback, flags, null)

        // Auto-cancel after timeout
        val timeout = Runnable {
            stopReaderMode(activity)
            pendingCall?.reject("Scan timed out.")
            pendingCall = null
        }
        timeoutRunnable = timeout
        android.os.Handler(android.os.Looper.getMainLooper()).postDelayed(timeout, timeoutMs.toLong())
    }

    @PluginMethod
    fun stopScan(call: PluginCall) {
        timeoutRunnable?.let {
            android.os.Handler(android.os.Looper.getMainLooper()).removeCallbacks(it)
        }
        timeoutRunnable = null
        activity?.let { stopReaderMode(it) }
        pendingCall?.reject("Scan cancelled by user.")
        pendingCall = null
        call.resolve()
    }

    private fun handleTag(tag: Tag) {
        timeoutRunnable?.let {
            android.os.Handler(android.os.Looper.getMainLooper()).removeCallbacks(it)
        }
        timeoutRunnable = null
        activity?.let { stopReaderMode(it) }

        executor.execute {
            try {
                val ndef = Ndef.get(tag) ?: run {
                    pendingCall?.reject("Tag does not contain NDEF data.")
                    pendingCall = null
                    return@execute
                }

                ndef.connect()
                val ndefMessage = ndef.cachedNdefMessage ?: ndef.ndefMessage
                ndef.close()

                if (ndefMessage == null || ndefMessage.records.isEmpty()) {
                    pendingCall?.reject("Empty NDEF message on tag.")
                    pendingCall = null
                    return@execute
                }

                val record = ndefMessage.records[0]
                val payload = decodeTextRecord(record.payload) ?: run {
                    pendingCall?.reject("Failed to decode NDEF text record.")
                    pendingCall = null
                    return@execute
                }

                val json = JSONObject(payload)
                val bid = json.optString("bid")
                val mid = json.optString("mid")
                val ts = json.optLong("ts")

                if (bid.isEmpty() || mid.isEmpty()) {
                    pendingCall?.reject("Invalid NDEF payload: missing bid or mid.")
                    pendingCall = null
                    return@execute
                }

                val result = JSObject()
                    .put("verified", true)
                    .put("businessId", bid)
                    .put("missionId", mid)
                    .put("tagTimestamp", ts.toString())
                    .put("rawPayload", payload)

                pendingCall?.resolve(result)
                pendingCall = null

            } catch (e: Exception) {
                pendingCall?.reject("NFC read error: ${e.message}")
                pendingCall = null
            }
        }
    }

    // NDEF Text record: byte[0] = status byte (bit7=UTF16, bits[0..5]=lang length)
    private fun decodeTextRecord(payload: ByteArray): String? {
        if (payload.isEmpty()) return null
        val statusByte = payload[0].toInt() and 0xFF
        val langLength = statusByte and 0x3F
        val isUtf16 = (statusByte and 0x80) != 0
        val textStart = 1 + langLength
        if (textStart >= payload.size) return null
        val textBytes = payload.copyOfRange(textStart, payload.size)
        return if (isUtf16) String(textBytes, Charsets.UTF_16) else String(textBytes, Charsets.UTF_8)
    }

    private fun stopReaderMode(activity: Activity) {
        try { nfcAdapter?.disableReaderMode(activity) } catch (_: Exception) {}
    }
}
