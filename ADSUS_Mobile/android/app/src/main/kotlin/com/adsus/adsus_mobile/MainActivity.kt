package com.adsus.adsus_mobile

import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

// T-5.2: onNewIntent để xử lý deep-link từ widget (adsus://reminders).
class MainActivity : FlutterActivity() {

    private val CHANNEL = "com.adsus.adsus_mobile/deep_link"

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        // ADSUS Medication Widget (T-5.2): Đăng ký MethodChannel để MainActivity
        // có thể forward intent sang Flutter router.
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL)
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "getInitialDeepLink" -> {
                        result.success(intent?.data?.toString())
                    }
                    else -> result.notImplemented()
                }
            }
    }

    override fun onNewIntent(intent: android.content.Intent) {
        super.onNewIntent(intent)

        // 1. Xử lý tap widget → mở tab Thuốc
        if (intent.getBooleanExtra("widget_open_medication", false)) {
            flutterEngine?.dartExecutor?.binaryMessenger?.let { messenger ->
                MethodChannel(messenger, CHANNEL).invokeMethod("openMedicationTab", null)
            }
        }

        // 2. Forward deep-link intent sang Flutter router (adsus://reminders?intakeId=...)
        val deepLink = intent.data?.toString()
        if (deepLink != null) {
            flutterEngine?.dartExecutor?.binaryMessenger?.let { messenger ->
                MethodChannel(messenger, CHANNEL).invokeMethod("onDeepLink", deepLink)
            }
        }
    }
}
