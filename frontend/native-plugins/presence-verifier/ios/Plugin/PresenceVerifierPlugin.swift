import Foundation
import Capacitor
import CoreLocation
import CoreMotion

@objc(PresenceVerifierPlugin)
public class PresenceVerifierPlugin: CAPPlugin, CAPBridgedPlugin, CLLocationManagerDelegate {
    public let identifier = "PresenceVerifierPlugin"
    public let jsName = "PresenceVerifier"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "startVerification", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "stopVerification", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "getStatus", returnType: CAPPluginReturnPromise),
    ]

    private let locationManager = CLLocationManager()
    private let motionManager = CMMotionActivityManager()
    private var insideGeofence = false
    private var activityType = "unknown"
    private var activeRegion: CLCircularRegion?

    override public func load() {
        locationManager.delegate = self
    }

    @objc func startVerification(_ call: CAPPluginCall) {
        guard let lat = call.getDouble("venueLat"),
              let lng = call.getDouble("venueLng") else {
            call.reject("venueLat and venueLng required"); return
        }
        let missionId = call.getInt("missionId") ?? 0
        let radius = call.getDouble("radiusMetres") ?? 100.0

        locationManager.requestAlwaysAuthorization()

        let center = CLLocationCoordinate2D(latitude: lat, longitude: lng)
        let region = CLCircularRegion(
            center: center,
            radius: radius,
            identifier: "synapse.mission.\(missionId)"
        )
        region.notifyOnEntry = true
        region.notifyOnExit = true
        activeRegion = region
        locationManager.startMonitoring(for: region)

        // Activity recognition — geofence handles the "inside" check, OS does the heavy lifting
        motionManager.startActivityUpdates(to: .main) { [weak self] activity in
            guard let a = activity else { return }
            if a.stationary { self?.activityType = "still" }
            else if a.walking { self?.activityType = "walking" }
            else if a.running { self?.activityType = "running" }
            else if a.automotive { self?.activityType = "vehicle" }
            else { self?.activityType = "unknown" }
        }

        call.resolve()
    }

    @objc func stopVerification(_ call: CAPPluginCall) {
        if let region = activeRegion { locationManager.stopMonitoring(for: region) }
        motionManager.stopActivityUpdates()
        insideGeofence = false
        activityType = "unknown"
        call.resolve()
    }

    @objc func getStatus(_ call: CAPPluginCall) {
        let confirmed = insideGeofence && activityType == "still"
        call.resolve([
            "insideGeofence": insideGeofence,
            "activityType": activityType,
            "confirmed": confirmed,
        ])
    }

    public func locationManager(_ manager: CLLocationManager, didEnterRegion region: CLRegion) {
        if region.identifier.starts(with: "synapse.mission.") { insideGeofence = true }
    }

    public func locationManager(_ manager: CLLocationManager, didExitRegion region: CLRegion) {
        if region.identifier.starts(with: "synapse.mission.") { insideGeofence = false }
    }
}
