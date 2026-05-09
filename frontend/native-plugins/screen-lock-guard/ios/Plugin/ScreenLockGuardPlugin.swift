import Foundation
import Capacitor
import ManagedSettings
import FamilyControls
import DeviceActivity

// IMPORTANT: DeviceActivityMonitorExtension has a 6 MB RAM budget — keep all types Codable
// with manual init(from:) instead of JSONDecoder on full blobs. (CLAUDE.md §4.2)

@objc(ScreenLockGuardPlugin)
public class ScreenLockGuardPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "ScreenLockGuardPlugin"
    public let jsName = "ScreenLockGuard"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "startGuard", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "stopGuard", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "isLocked", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "getLockedSeconds", returnType: CAPPluginReturnPromise),
    ]

    private let store = ManagedSettingsStore()
    private var guardStartDate: Date?

    @objc func startGuard(_ call: CAPPluginCall) {
        guard let missionId = call.getInt("missionId"),
              let requiredMinutes = call.getInt("requiredMinutes") else {
            call.reject("missionId and requiredMinutes are required")
            return
        }

        Task {
            do {
                // Request FamilyControls authorization (must be called from UI context on first run)
                try await AuthorizationCenter.shared.requestAuthorization(for: .individual)

                // Shield all apps except our own app token
                let policy = ManagedSettingsStore.ShieldSettings(
                    applications: ApplicationToken.all
                )
                self.store.shield.applications = policy.applications
                self.guardStartDate = Date()

                // Schedule a DeviceActivity to automatically call stopGuard after requiredMinutes
                let schedule = DeviceActivitySchedule(
                    intervalStart: DateComponents(hour: 0, minute: 0),
                    intervalEnd: DateComponents(hour: 23, minute: 59),
                    repeats: false
                )
                let center = DeviceActivityCenter()
                try center.startMonitoring(
                    DeviceActivityName("synapse.mission.\(missionId)"),
                    during: schedule
                )
                call.resolve()
            } catch {
                call.reject("FamilyControls authorization failed: \(error.localizedDescription)")
            }
        }
    }

    @objc func stopGuard(_ call: CAPPluginCall) {
        store.shield.applications = nil
        guardStartDate = nil
        DeviceActivityCenter().stopMonitoring()
        call.resolve()
    }

    @objc func isLocked(_ call: CAPPluginCall) {
        // FamilyControls shield is applied — treat as "locked"
        let locked = store.shield.applications != nil
        call.resolve(["locked": locked])
    }

    @objc func getLockedSeconds(_ call: CAPPluginCall) {
        guard let start = guardStartDate else {
            call.resolve(["seconds": 0])
            return
        }
        let seconds = Int(Date().timeIntervalSince(start))
        call.resolve(["seconds": seconds])
    }
}
