package com.synapse.presenceverifier

import android.Manifest
import android.app.PendingIntent
import android.content.Intent
import android.content.pm.PackageManager
import androidx.core.content.ContextCompat
import com.getcapacitor.JSObject
import com.getcapacitor.Plugin
import com.getcapacitor.PluginCall
import com.getcapacitor.PluginMethod
import com.getcapacitor.annotation.CapacitorPlugin
import com.getcapacitor.annotation.Permission
import com.getcapacitor.annotation.PermissionCallback
import com.google.android.gms.location.ActivityRecognition
import com.google.android.gms.location.ActivityRecognitionClient
import com.google.android.gms.location.ActivityTransition
import com.google.android.gms.location.ActivityTransitionRequest
import com.google.android.gms.location.DetectedActivity
import com.google.android.gms.location.Geofence
import com.google.android.gms.location.GeofencingClient
import com.google.android.gms.location.GeofencingRequest
import com.google.android.gms.location.LocationServices

@CapacitorPlugin(
    name = "PresenceVerifier",
    permissions = [
        Permission(strings = [Manifest.permission.ACCESS_FINE_LOCATION], alias = "location"),
        Permission(strings = [Manifest.permission.ACTIVITY_RECOGNITION], alias = "activityRecognition"),
    ]
)
class PresenceVerifierPlugin : Plugin() {

    private lateinit var geofencingClient: GeofencingClient
    private lateinit var activityClient: ActivityRecognitionClient
    private var geofencePendingIntent: PendingIntent? = null
    private var activityPendingIntent: PendingIntent? = null

    companion object {
        private var insideGeofence = false
        private var currentActivity = "unknown"

        fun onGeofenceTransition(transition: Int) {
            insideGeofence = transition == Geofence.GEOFENCE_TRANSITION_ENTER
        }

        fun onActivityTransition(activityType: Int, transitionType: Int) {
            if (transitionType == ActivityTransition.ACTIVITY_TRANSITION_ENTER) {
                currentActivity = when (activityType) {
                    DetectedActivity.STILL        -> "still"
                    DetectedActivity.WALKING      -> "walking"
                    DetectedActivity.RUNNING      -> "running"
                    DetectedActivity.IN_VEHICLE   -> "vehicle"
                    else                          -> "unknown"
                }
            }
        }
    }

    override fun load() {
        geofencingClient = LocationServices.getGeofencingClient(context)
        activityClient = ActivityRecognition.getClient(context)
    }

    @PluginMethod
    fun startVerification(call: PluginCall) {
        val lat = call.getDouble("venueLat") ?: run { call.reject("venueLat required"); return }
        val lng = call.getDouble("venueLng") ?: run { call.reject("venueLng required"); return }
        val radius = call.getFloat("radiusMetres") ?: 100f

        if (!hasRequiredPermissions()) {
            requestAllPermissions(call, "permissionsCallback")
            return
        }

        registerGeofence(lat, lng, radius)
        registerActivityTransitions()
        call.resolve()
    }

    @PermissionCallback
    private fun permissionsCallback(call: PluginCall) {
        if (!hasRequiredPermissions()) {
            call.reject("Location and activity recognition permissions are required")
            return
        }
        startVerification(call)
    }

    @PluginMethod
    fun stopVerification(call: PluginCall) {
        geofencePendingIntent?.let { geofencingClient.removeGeofences(it) }
        activityPendingIntent?.let { activityClient.removeActivityTransitionUpdates(it) }
        insideGeofence = false
        currentActivity = "unknown"
        call.resolve()
    }

    @PluginMethod
    fun getStatus(call: PluginCall) {
        val confirmed = insideGeofence && currentActivity == "still"
        val result = JSObject()
            .put("insideGeofence", insideGeofence)
            .put("activityType", currentActivity)
            .put("confirmed", confirmed)
        call.resolve(result)
    }

    private fun registerGeofence(lat: Double, lng: Double, radius: Float) {
        val geofence = Geofence.Builder()
            .setRequestId("synapse_venue")
            .setCircularRegion(lat, lng, radius)
            .setExpirationDuration(Geofence.NEVER_EXPIRE)
            .setTransitionTypes(Geofence.GEOFENCE_TRANSITION_ENTER or Geofence.GEOFENCE_TRANSITION_EXIT)
            .build()

        val request = GeofencingRequest.Builder()
            .setInitialTrigger(GeofencingRequest.INITIAL_TRIGGER_ENTER)
            .addGeofence(geofence)
            .build()

        val intent = Intent(context, GeofenceBroadcastReceiver::class.java)
        geofencePendingIntent = PendingIntent.getBroadcast(
            context, 0, intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_MUTABLE
        )

        if (ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION)
            == PackageManager.PERMISSION_GRANTED) {
            geofencingClient.addGeofences(request, geofencePendingIntent!!)
        }
    }

    private fun registerActivityTransitions() {
        val transitions = listOf(
            DetectedActivity.STILL, DetectedActivity.WALKING,
            DetectedActivity.RUNNING, DetectedActivity.IN_VEHICLE
        ).flatMap { type ->
            listOf(
                ActivityTransition.Builder()
                    .setActivityType(type)
                    .setActivityTransition(ActivityTransition.ACTIVITY_TRANSITION_ENTER)
                    .build()
            )
        }

        val request = ActivityTransitionRequest(transitions)
        val intent = Intent(context, ActivityTransitionReceiver::class.java)
        activityPendingIntent = PendingIntent.getBroadcast(
            context, 1, intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_MUTABLE
        )

        if (ContextCompat.checkSelfPermission(context, Manifest.permission.ACTIVITY_RECOGNITION)
            == PackageManager.PERMISSION_GRANTED) {
            activityClient.requestActivityTransitionUpdates(request, activityPendingIntent!!)
        }
    }

    private fun hasRequiredPermissions(): Boolean {
        val loc = ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION)
        val act = ContextCompat.checkSelfPermission(context, Manifest.permission.ACTIVITY_RECOGNITION)
        return loc == PackageManager.PERMISSION_GRANTED && act == PackageManager.PERMISSION_GRANTED
    }
}
