import 'package:flutter/material.dart';

/// Bộ nhận diện ADSUS.
///
/// Cùng bảng màu với bản web (rút từ template Medizco của nhóm), để hai nền tảng nhìn
/// như một sản phẩm:
///   #223a66 navy  màu chủ đạo
///   #1cba9f teal   màu nhấn, nút bấm
///   #f13a66 hồng   lỗi, cảnh báo
class AppColors {
  const AppColors._();

  static const Color navy = Color(0xFF223A66);
  static const Color teal = Color(0xFF1CBA9F);
  static const Color primary = Color(0xFF1CBA9F); // teal alias
  static const Color accent = Color(0xFF558DCA);  // blue accent
  static const Color blue = Color(0xFF558DCA);
  static const Color danger = Color(0xFFF13A66);

  /// Màu cho chatbot AI (viền + badge ASSISTANT bubble).
  /// Khớp với --ai-violet trong design tokens ADSUS.
  static const Color aiViolet = Color(0xFF7C5CD9);

  /// Màu nền banner/viền AI (tint của aiViolet).
  static const Color aiVioletTint = Color(0xFFF1EDFC);

  /// Màu nền danger card (safety response).
  static const Color dangerTint = Color(0xFFFBEAE9);

  // ── Context badge colors (intent-based, inside AI bubble) ──────────────────
  /// Mặc định khi không rõ intent.
  static const Color contextDefault = AppColors.aiVioletTint;
  static const Color contextPrescription = Color(0xFFE8F5E9); // xanh lá nhạt
  static const Color contextAppointment  = Color(0xFFE3F2FD); // xanh dương nhạt
  static const Color contextCaseHistory  = Color(0xFFFFF3E0); // cam nhạt
  static const Color contextAllergy      = Color(0xFFFFEBEE); // đỏ nhạt
  static const Color contextDisease       = Color(0xFFFFF3E0); // cam nhạt
  static const Color contextHealthLog    = Color(0xFFE0F7FA); // cyan nhạt
  static const Color contextBlog         = Color(0xFFF3E5F5); // tím nhạt
  static const Color contextGreeting     = Color(0xFFF1EDFC); // aiViolet tint
  static const Color contextGeneral      = Color(0xFFF1EDFC); // aiViolet tint

  /// Text color tương ứng cho context badge.
  static const Color contextTextPrescription = Color(0xFF2E7D32); // xanh đậm
  static const Color contextTextAppointment  = Color(0xFF1565C0); // xanh dương đậm
  static const Color contextTextCaseHistory  = Color(0xFFE65100); // cam đậm
  static const Color contextTextAllergy      = Color(0xFFC62828); // đỏ đậm
  static const Color contextTextDisease       = Color(0xFFE65100); // cam đậm
  static const Color contextTextHealthLog    = Color(0xFF00695C); // cyan đậm
  static const Color contextTextBlog         = Color(0xFF6A1B9A); // tím đậm
  /// Hex khớp với web --status-good/--status-warning trong globals.css.
  /// KHÔNG dùng [danger] cho adherence thấp — đỏ chỉ dành cho safety card / validation
  /// error (xem CLAUDE.md §11.3.4).
  static const Color success = Color(0xFF1CBA9F); // adherence ≥80%
  static const Color amberWarn = Color(0xFFE0912F); // adherence <80%

  static const Color background = Color(0xFFF7F9FB);
  static const Color border = Color(0xFFDDE5EF);
  static const Color muted = Color(0xFF5B6B85);

  /// Màu nền notification chưa đọc (xanh nhạt)
  static const Color unreadBg = Color(0xFFE3F2FD);
}

class AppTheme {
  const AppTheme._();

  static ThemeData get light {
    const scheme = ColorScheme.light(
      primary: AppColors.navy,
      onPrimary: Colors.white,
      secondary: AppColors.teal,
      onSecondary: Colors.white,
      error: AppColors.danger,
      onError: Colors.white,
      surface: Colors.white,
      onSurface: Color(0xFF222222),
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: AppColors.background,

      appBarTheme: const AppBarTheme(
        backgroundColor: Colors.white,
        foregroundColor: AppColors.navy,
        elevation: 0,
        centerTitle: false,
      ),

      // Ô nhập cao và bo tròn, giống bản web (template dùng height 56px).
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white,
        contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 18),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(28),
          borderSide: const BorderSide(color: AppColors.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(28),
          borderSide: const BorderSide(color: AppColors.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(28),
          borderSide: const BorderSide(color: AppColors.teal, width: 1.6),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(28),
          borderSide: const BorderSide(color: AppColors.danger),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(28),
          borderSide: const BorderSide(color: AppColors.danger, width: 1.6),
        ),
      ),

      // Nút bo tròn hoàn toàn, nền teal, chữ in hoa — đúng .btn-style-one của template.
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColors.teal,
          foregroundColor: Colors.white,
          minimumSize: const Size.fromHeight(56),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(28)),
          textStyle: const TextStyle(
            fontSize: 15,
            fontWeight: FontWeight.w600,
            letterSpacing: 0.8,
          ),
        ),
      ),

      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColors.navy,
          minimumSize: const Size.fromHeight(56),
          side: const BorderSide(color: AppColors.border),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(28)),
        ),
      ),
    );
  }
}
