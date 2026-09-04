package com.adsus.adsus_mobile

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.Context
import android.content.Intent
import android.view.View
import android.widget.RemoteViews
import es.antonborri.home_widget.HomeWidgetPlugin
import org.json.JSONArray
import org.json.JSONObject
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter

/**
 * Android AppWidgetProvider cho Medication Widget.
 *
 * Widget đọc dữ liệu JSON từ SharedPreferences (do Flutter ghi qua home_widget).
 * Layout được inflate từ res/layout/medication_widget.xml.
 *
 * T-1.3 — ADSUS Medication Widget
 */
class MedicationWidgetProvider : AppWidgetProvider() {

    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray
    ) {
        for (appWidgetId in appWidgetIds) {
            updateAppWidget(context, appWidgetManager, appWidgetId)
        }
    }

    override fun onReceive(context: Context, intent: Intent) {
        super.onReceive(context, intent)

        // Xử lý refresh từ tap header icon
        if (intent.action == ACTION_REFRESH) {
            val appWidgetManager = AppWidgetManager.getInstance(context)
            val componentName = intent.getParcelableExtra<android.content.ComponentName>(EXTRA_COMPONENT_NAME)
            componentName?.let {
                appWidgetManager.getAppWidgetIds(it)?.forEach { widgetId ->
                    updateAppWidget(context, appWidgetManager, widgetId)
                }
            }
        }
    }

    companion object {
        const val ACTION_REFRESH = "com.adsus.adsus_mobile.ACTION_REFRESH_WIDGET"
        const val EXTRA_COMPONENT_NAME = "component_name"

        // SharedPreferences key do Flutter ghi
        const val PREFS_NAME = "HomeWidgetPreferences"
        const val KEY_WIDGET_DATA = "widget_data"

        // Widget size constraints (dp) — 4 rows cho widget 4×3
        const val DOSE_ROW_COUNT = 4

        fun updateAppWidget(
            context: Context,
            appWidgetManager: AppWidgetManager,
            appWidgetId: Int
        ) {
            val views = RemoteViews(context.packageName, R.layout.medication_widget)

            // Đọc JSON từ SharedPreferences
            val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            val jsonString = prefs.getString(KEY_WIDGET_DATA, null)

            when {
                jsonString == null || jsonString.isEmpty() -> {
                    renderNotLoggedIn(views)
                }
                jsonString == "loading" -> {
                    renderLoading(views)
                }
                jsonString == "error" -> {
                    renderError(views)
                }
                jsonString == "all_done" -> {
                    renderAllDone(views)
                }
                jsonString == "no_prescriptions" -> {
                    renderNoPrescriptions(views)
                }
                else -> {
                    renderDoses(views, jsonString)
                }
            }

            // Setup tap cho header refresh icon
            val refreshIntent = Intent(context, MedicationWidgetProvider::class.java).apply {
                action = ACTION_REFRESH
                putExtra(EXTRA_COMPONENT_NAME, android.content.ComponentName(context, MedicationWidgetProvider::class.java))
            }
            val refreshPendingIntent = PendingIntent.getBroadcast(
                context,
                0,
                refreshIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
            views.setOnClickPendingIntent(R.id.btn_refresh, refreshPendingIntent)

            // Tap vào root widget → mở app ở tab Thuốc (index=1)
            // Option A: Bỏ deep-link data URI. Thêm extra widget_open_medication=true.
            // MainActivity đọc extra → gọi MethodChannel "openMedicationTab" → Flutter set tab.
            val rootIntent = context.packageManager.getLaunchIntentForPackage(context.packageName)?.apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
                putExtra("widget_open_medication", true)
            }
            val rootPendingIntent = PendingIntent.getActivity(
                context,
                1,
                rootIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
            views.setOnClickPendingIntent(R.id.widget_root, rootPendingIntent)

            // Setup tap cho từng dose row → deep link với intakeId
            setupDoseRowClicks(context, views)

            appWidgetManager.updateAppWidget(appWidgetId, views)
        }

        private fun renderLoading(views: RemoteViews) {
            views.setViewVisibility(R.id.state_message, View.VISIBLE)
            views.setTextViewText(R.id.state_message, "Đang tải...")
            views.setViewVisibility(R.id.doses_container, View.GONE)
            views.setViewVisibility(R.id.text_more, View.GONE)
            views.setViewVisibility(R.id.text_count, View.GONE)
        }

        private fun renderError(views: RemoteViews) {
            views.setViewVisibility(R.id.state_message, View.VISIBLE)
            views.setTextViewText(R.id.state_message, "Không tải được dữ liệu")
            views.setViewVisibility(R.id.doses_container, View.GONE)
            views.setViewVisibility(R.id.text_more, View.GONE)
            views.setViewVisibility(R.id.text_count, View.GONE)
        }

        private fun renderNotLoggedIn(views: RemoteViews) {
            views.setViewVisibility(R.id.state_message, View.VISIBLE)
            views.setTextViewText(R.id.state_message, "Mở app để đăng nhập")
            views.setViewVisibility(R.id.doses_container, View.GONE)
            views.setViewVisibility(R.id.text_more, View.GONE)
            views.setViewVisibility(R.id.text_count, View.GONE)
        }

        private fun renderAllDone(views: RemoteViews) {
            views.setViewVisibility(R.id.state_message, View.VISIBLE)
            views.setTextViewText(R.id.state_message, "Bạn đã hoàn thành tất cả liều thuốc của hôm nay. Good Job!")
            views.setViewVisibility(R.id.doses_container, View.GONE)
            views.setViewVisibility(R.id.text_more, View.GONE)
            views.setViewVisibility(R.id.text_count, View.GONE)
        }

        private fun renderNoPrescriptions(views: RemoteViews) {
            views.setViewVisibility(R.id.state_message, View.VISIBLE)
            views.setTextViewText(R.id.state_message, "Hôm nay không có liều thuốc nào")
            views.setViewVisibility(R.id.doses_container, View.GONE)
            views.setViewVisibility(R.id.text_more, View.GONE)
            views.setViewVisibility(R.id.text_count, View.GONE)
        }

        private fun renderDoses(views: RemoteViews, jsonString: String) {
            views.setViewVisibility(R.id.state_message, View.GONE)

            val doses = try {
                JSONArray(jsonString)
            } catch (e: Exception) {
                JSONArray()
            }

            if (doses.length() == 0) {
                views.setViewVisibility(R.id.doses_container, View.GONE)
                views.setViewVisibility(R.id.state_message, View.VISIBLE)
                views.setTextViewText(R.id.state_message, "Hôm nay không có liều thuốc nào")
                views.setViewVisibility(R.id.text_more, View.GONE)
                return
            }

            views.setViewVisibility(R.id.doses_container, View.VISIBLE)

            // Hiển thị count badge trong header
            views.setViewVisibility(R.id.text_count, View.VISIBLE)
            views.setTextViewText(R.id.text_count, "${doses.length()} liều")

            // Render tối đa 4 dose rows
            val displayCount = minOf(doses.length(), DOSE_ROW_COUNT)

            for (i in 1..DOSE_ROW_COUNT) {
                val rowId = when (i) {
                    1 -> R.id.dose_row_1
                    2 -> R.id.dose_row_2
                    3 -> R.id.dose_row_3
                    4 -> R.id.dose_row_4
                    else -> 0
                }
                val timeId = when (i) {
                    1 -> R.id.text_time_1
                    2 -> R.id.text_time_2
                    3 -> R.id.text_time_3
                    4 -> R.id.text_time_4
                    else -> 0
                }
                val nameId = when (i) {
                    1 -> R.id.text_name_1
                    2 -> R.id.text_name_2
                    3 -> R.id.text_name_3
                    4 -> R.id.text_name_4
                    else -> 0
                }
                val dosageId = when (i) {
                    1 -> R.id.text_dosage_1
                    2 -> R.id.text_dosage_2
                    3 -> R.id.text_dosage_3
                    4 -> R.id.text_dosage_4
                    else -> 0
                }
                val statusId = when (i) {
                    1 -> R.id.text_status_1
                    2 -> R.id.text_status_2
                    3 -> R.id.text_status_3
                    4 -> R.id.text_status_4
                    else -> 0
                }
                val borderId = when (i) {
                    1 -> R.id.border_1
                    2 -> R.id.border_2
                    3 -> R.id.border_3
                    4 -> R.id.border_4
                    else -> 0
                }

                if (i <= displayCount) {
                    val dose = doses.getJSONObject(i - 1)
                    val scheduledTime = dose.optString("scheduledTime", "")
                    val medicineName = dose.optString("medicineName", "")
                    val dosage = dose.optString("dosage", "")
                    val status = dose.optString("status", "pending")

                    val timeDisplay = formatTime(scheduledTime)
                    val isOvertime = status.lowercase() == "overtime"

                    views.setViewVisibility(rowId, View.VISIBLE)
                    views.setTextViewText(timeId, timeDisplay)
                    views.setTextViewText(nameId, medicineName)
                    views.setTextViewText(dosageId, dosage)

                    // Border-left màu: danger cho overtime, amber cho pending
                    // Dùng setInt vì setBackgroundColor chỉ có từ API 31
                    val borderColor = if (isOvertime) "#D8453B" else "#E8963C"
                    views.setInt(borderId, "setBackgroundColor", android.graphics.Color.parseColor(borderColor))

                    // Status badge text + color
                    when (status.lowercase()) {
                        "overtime" -> {
                            views.setTextViewText(statusId, "Quá giờ")
                            views.setTextColor(statusId, android.graphics.Color.parseColor("#D8453B"))
                        }
                        "pending" -> {
                            views.setTextViewText(statusId, "Sắp tới")
                            views.setTextColor(statusId, android.graphics.Color.parseColor("#E8963C"))
                        }
                        else -> {
                            views.setTextViewText(statusId, "Chưa uống")
                            views.setTextColor(statusId, android.graphics.Color.parseColor("#E8963C"))
                        }
                    }
                } else {
                    views.setViewVisibility(rowId, View.GONE)
                }
            }

            // "và X liều khác" nếu có nhiều hơn 4
            val remaining = doses.length() - DOSE_ROW_COUNT
            if (remaining > 0) {
                views.setViewVisibility(R.id.text_more, View.VISIBLE)
                views.setTextViewText(R.id.text_more, "và $remaining liều khác")
            } else {
                views.setViewVisibility(R.id.text_more, View.GONE)
            }
        }

        private fun formatTime(isoTime: String): String {
            // Parse "2026-09-03T08:00:00Z" (UTC) → convert sang Asia/Ho_Chi_Minh → "15:00"
            // Backend lưu giờ UTC, user ở VN nên phải +7h.
            return try {
                if (isoTime.isEmpty()) return ""
                val utcTime = OffsetDateTime.parse(isoTime)
                val localTime = utcTime.atZoneSameInstant(ZoneId.of("Asia/Ho_Chi_Minh"))
                localTime.format(DateTimeFormatter.ofPattern("HH:mm"))
            } catch (e: Exception) {
                // Fallback nếu parse fail (string format khác): substring raw
                try {
                    val parts = isoTime.split("T")
                    if (parts.size >= 2) {
                        val timePart = parts[1]
                        val hh = timePart.substring(0, 2)
                        val mm = timePart.substring(3, 5)
                        "$hh:$mm"
                    } else {
                        isoTime.take(5)
                    }
                } catch (e2: Exception) {
                    isoTime.take(5)
                }
            }
        }

        private fun setupDoseRowClicks(context: Context, views: RemoteViews) {
            // Đọc intakeId từ SharedPreferences để setup PendingIntent
            val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            val jsonString = prefs.getString(KEY_WIDGET_DATA, null) ?: return

            val doses = try {
                JSONArray(jsonString)
            } catch (e: Exception) {
                return
            }

            val rowIds = listOf(R.id.dose_row_1, R.id.dose_row_2, R.id.dose_row_3, R.id.dose_row_4)
            for (i in 0 until minOf(doses.length(), DOSE_ROW_COUNT)) {
                val dose = doses.getJSONObject(i)
                val intakeId = dose.optString("intakeId", "")
                if (intakeId.isEmpty()) continue

                val rowId = rowIds[i]
                val deepLinkUri = "adsus://reminders?intakeId=$intakeId"
                val intent = context.packageManager.getLaunchIntentForPackage(context.packageName)?.apply {
                    flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
                    data = android.net.Uri.parse(deepLinkUri)
                }
                val pendingIntent = PendingIntent.getActivity(
                    context,
                    (i + 10), // unique request code
                    intent,
                    PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
                )
                views.setOnClickPendingIntent(rowId, pendingIntent)
            }
        }
    }
}
