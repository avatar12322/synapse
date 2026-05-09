import Foundation
import Capacitor
import CoreNFC

// NDEF payload format:
// { "v": 1, "bid": "<businessId>", "mid": "<missionId>", "ts": <unixEpoch>, "sig": "<hmac-hex>" }
// HMAC-SHA256(business.NfcSecret, "v=1|bid=<bid>|mid=<mid>|ts=<ts>")
// Verification happens server-side via POST /api/missions/{id}/verify-nfc

@objc(NfcPresencePlugin)
public class NfcPresencePlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "NfcPresencePlugin"
    public let jsName = "NfcPresence"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "startScan", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "stopScan", returnType: CAPPluginReturnPromise),
    ]

    private var readerSession: NFCNDEFReaderSession?
    private var pendingCall: CAPPluginCall?
    private var scanOptions: ScanOptions?
    private var timeoutWork: DispatchWorkItem?

    @objc func startScan(_ call: CAPPluginCall) {
        guard NFCNDEFReaderSession.readingAvailable else {
            call.reject("NFC is not available on this device.")
            return
        }

        guard let missionId = call.getString("missionId"),
              let expectedBusinessId = call.getString("expectedBusinessId") else {
            call.reject("missionId and expectedBusinessId are required.")
            return
        }

        let timeoutMs = call.getInt("timeoutMs") ?? 30000

        pendingCall = call
        scanOptions = ScanOptions(missionId: missionId, expectedBusinessId: expectedBusinessId)

        let session = NFCNDEFReaderSession(delegate: self, queue: nil, invalidateAfterFirstRead: true)
        session.alertMessage = "Hold your iPhone near the quest tag at the venue."
        readerSession = session
        session.begin()

        // Auto-cancel after timeout
        let work = DispatchWorkItem { [weak self] in
            self?.readerSession?.invalidate(errorMessage: "Scan timed out.")
            self?.readerSession = nil
            self?.pendingCall?.reject("Scan timed out.")
            self?.pendingCall = nil
        }
        timeoutWork = work
        DispatchQueue.main.asyncAfter(deadline: .now() + .milliseconds(timeoutMs), execute: work)
    }

    @objc func stopScan(_ call: CAPPluginCall) {
        timeoutWork?.cancel()
        readerSession?.invalidate()
        readerSession = nil
        pendingCall?.reject("Scan cancelled by user.")
        pendingCall = nil
        call.resolve()
    }

    private struct ScanOptions {
        let missionId: String
        let expectedBusinessId: String
    }
}

extension NfcPresencePlugin: NFCNDEFReaderSessionDelegate {
    public func readerSession(_ session: NFCNDEFReaderSession, didInvalidateWithError error: Error) {
        // Session ended — either by user, timeout, or error. pendingCall already resolved/rejected.
    }

    public func readerSession(_ session: NFCNDEFReaderSession, didDetectNDEFs messages: [NFCNDEFMessage]) {
        timeoutWork?.cancel()

        guard let record = messages.first?.records.first,
              record.typeNameFormat == .nfcWellKnown,
              let type = String(data: record.type, encoding: .utf8), type == "T",
              let payload = extractTextPayload(record.payload) else {
            pendingCall?.reject("No readable NDEF Text record found on tag.")
            pendingCall = nil
            return
        }

        guard let data = payload.data(using: .utf8),
              let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let bid = json["bid"] as? String,
              let mid = json["mid"] as? String,
              let ts = json["ts"] as? Int else {
            pendingCall?.reject("Invalid NDEF payload format.")
            pendingCall = nil
            return
        }

        pendingCall?.resolve([
            "verified": true,
            "businessId": bid,
            "missionId": mid,
            "tagTimestamp": String(ts),
            "rawPayload": payload,
        ])
        pendingCall = nil
    }

    // NDEF Text record: language-code prefix (1 byte length + N bytes lang) then text
    private func extractTextPayload(_ data: Data) -> String? {
        guard data.count > 1 else { return nil }
        let langLength = Int(data[0] & 0x3F)
        let textStart = 1 + langLength
        guard textStart < data.count else { return nil }
        return String(data: data[textStart...], encoding: .utf8)
    }
}
