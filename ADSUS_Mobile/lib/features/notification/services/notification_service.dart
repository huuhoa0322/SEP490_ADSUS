import 'dart:io';

import 'package:dio/dio.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

import '../../../core/constants/api_constants.dart';

/// Service to handle FCM push notifications.
/// Initializes Firebase Cloud Messaging and handles token registration to backend.
class NotificationService {
  NotificationService._();

  static final NotificationService _instance = NotificationService._();
  factory NotificationService() => _instance;

  final FirebaseMessaging _messaging = FirebaseMessaging.instance;
  final FlutterLocalNotificationsPlugin _localNotifications = FlutterLocalNotificationsPlugin();
  String? _fcmToken;

  /// Dio instance for FCM token registration.
  /// Lazy-initialized on first use.
  Dio? _dio;

  /// Current FCM token for this device.
  String? get fcmToken => _fcmToken;

  /// Initialize the notification service.
  /// Call this in main() after Firebase.initializeApp().
  Future<void> initialize() async {
    // Initialize local notifications first
    await _initializeLocalNotifications();

    // Request permission for iOS (no-op on Android)
    final settings = await _messaging.requestPermission(
      alert: true,
      badge: true,
      sound: true,
    );

    debugPrint('[NotificationService] Permission status: ${settings.authorizationStatus}');

    // Get FCM token
    _fcmToken = await _messaging.getToken();
    debugPrint('[NotificationService] FCM Token: $_fcmToken');

    // Listen for token refresh
    _messaging.onTokenRefresh.listen(_handleTokenRefresh);

    // Handle foreground messages
    FirebaseMessaging.onMessage.listen(_handleForegroundMessage);

    // Handle background messages (when app is opened from notification)
    FirebaseMessaging.onMessageOpenedApp.listen(_handleMessageOpenedApp);
  }

  Future<void> _initializeLocalNotifications() async {
    const androidSettings = AndroidInitializationSettings('@mipmap/ic_launcher');
    const iosSettings = DarwinInitializationSettings(
      requestAlertPermission: true,
      requestBadgePermission: true,
      requestSoundPermission: true,
    );
    const initSettings = InitializationSettings(
      android: androidSettings,
      iOS: iosSettings,
    );
    await _localNotifications.initialize(initSettings);
    debugPrint('[NotificationService] Local notifications initialized');
  }

  void _handleTokenRefresh(String token) {
    _fcmToken = token;
    debugPrint('[NotificationService] Token refreshed: $token');
    // Token will be re-registered when user logs in again
  }

  Future<void> _handleForegroundMessage(RemoteMessage message) async {
    debugPrint('[NotificationService] Foreground message: ${message.notification?.title}');

    // Show local notification for foreground messages
    if (message.notification != null) {
      await _showLocalNotification(
        title: message.notification!.title ?? '',
        body: message.notification!.body ?? '',
        data: message.data.map((k, v) => MapEntry(k, v.toString())),
      );
    }
  }

  Future<void> _showLocalNotification({
    required String title,
    required String body,
    Map<String, String>? data,
  }) async {
    const androidDetails = AndroidNotificationDetails(
      'default',
      'General Notifications',
      channelDescription: 'General app notifications',
      importance: Importance.high,
      priority: Priority.high,
      showWhen: true,
    );
    const iosDetails = DarwinNotificationDetails(
      presentAlert: true,
      presentBadge: true,
      presentSound: true,
    );
    const details = NotificationDetails(
      android: androidDetails,
      iOS: iosDetails,
    );

    await _localNotifications.show(
      DateTime.now().millisecondsSinceEpoch ~/ 1000,
      title,
      body,
      details,
      payload: data?['deepLink'],
    );
    debugPrint('[NotificationService] Local notification shown: $title');
  }

  void _handleMessageOpenedApp(RemoteMessage message) {
    debugPrint('[NotificationService] App opened from notification: ${message.notification?.title}');
    // TODO: Navigate to relevant screen based on message data
  }

  /// Register FCM token to backend.
  /// Call this after user successfully logs in.
  /// Requires access token to be stored in secure storage.
  Future<void> registerTokenWithBackend(String accessToken) async {
    final token = _fcmToken;
    if (token == null || token.isEmpty) {
      debugPrint('[NotificationService] No FCM token to register');
      return;
    }

    try {
      _dio ??= Dio(BaseOptions(
        baseUrl: ApiConstants.baseUrl,
        connectTimeout: ApiConstants.timeout,
        receiveTimeout: ApiConstants.timeout,
        headers: {
          'Content-Type': 'application/json; charset=utf-8',
          'Accept-Charset': 'utf-8',
          'Authorization': 'Bearer $accessToken',
        },
      ));

      final deviceType = Platform.isAndroid ? 'android' : 'ios';

      await _dio!.put(
        ApiConstants.fcmToken,
        data: {
          'fcmToken': token,
          'deviceType': deviceType,
        },
      );

      debugPrint('[NotificationService] Token registered with backend: $token (device: $deviceType)');
    } catch (e, st) {
      debugPrint('[NotificationService] Failed to register token: $e\n$st');
      // Non-critical - notification still works via polling
    }
  }

  /// Unregister FCM token from backend.
  /// Call this when user logs out.
  Future<void> unregisterTokenFromBackend(String accessToken) async {
    try {
      _dio ??= Dio(BaseOptions(
        baseUrl: ApiConstants.baseUrl,
        connectTimeout: ApiConstants.timeout,
        receiveTimeout: ApiConstants.timeout,
        headers: {
          'Content-Type': 'application/json; charset=utf-8',
          'Accept-Charset': 'utf-8',
          'Authorization': 'Bearer $accessToken',
        },
      ));

      await _dio!.delete(
        ApiConstants.fcmToken,
        data: {'fcmToken': _fcmToken},
      );

      debugPrint('[NotificationService] Token unregistered from backend');
    } catch (e) {
      debugPrint('[NotificationService] Failed to unregister token: $e');
      // Non-critical - ignore errors on logout
    }
  }
}

/// Global instance
final notificationService = NotificationService();
