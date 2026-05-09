import Foundation
import Capacitor
import DeviceCheck

@objc(AntiSpoofPlugin)
public class AntiSpoofPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "AntiSpoofPlugin"
    public let jsName = "AntiSpoof"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "getAttestationToken", returnType: CAPPluginReturnPromise),
    ]

    @objc func getAttestationToken(_ call: CAPPluginCall) {
        guard let challengeHex = call.getString("challenge") else {
            call.reject("challenge is required"); return
        }

        guard DCAppAttestService.shared.isSupported else {
            // Simulator or unsupported device — return stub token
            call.resolve(["platform": "ios", "token": "simulator-not-supported"])
            return
        }

        let challengeData = Data(hexString: challengeHex) ?? Data(challengeHex.utf8)

        DCAppAttestService.shared.generateKey { keyId, error in
            if let error {
                call.reject("Key generation failed: \(error.localizedDescription)"); return
            }
            guard let keyId else { call.reject("No key ID"); return }

            // Hash the challenge as required by App Attest
            let hash = Data(SHA256.hash(data: challengeData))

            DCAppAttestService.shared.attestKey(keyId, clientDataHash: hash) { attestation, error in
                if let error {
                    call.reject("Attestation failed: \(error.localizedDescription)"); return
                }
                guard let attestation else { call.reject("No attestation"); return }

                let token = attestation.base64EncodedString()
                call.resolve(["platform": "ios", "token": token])
            }
        }
    }
}

private extension Data {
    init?(hexString: String) {
        let len = hexString.count
        guard len % 2 == 0 else { return nil }
        var data = Data(capacity: len / 2)
        var i = hexString.startIndex
        while i < hexString.endIndex {
            let j = hexString.index(i, offsetBy: 2)
            guard let byte = UInt8(hexString[i..<j], radix: 16) else { return nil }
            data.append(byte)
            i = j
        }
        self = data
    }
}

// SHA256 helper (CryptoKit available iOS 13+)
import CryptoKit
private enum SHA256 {
    static func hash(data: Data) -> [UInt8] {
        Array(CryptoKit.SHA256.hash(data: data))
    }
}
