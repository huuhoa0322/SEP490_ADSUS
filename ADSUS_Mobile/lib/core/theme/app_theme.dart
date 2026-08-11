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

  /// Màu trạng thái tuân thủ (Module 7 AdherencePill).
  /// Hex khớp với web --status-good/--status-warning trong globals.css.
  /// KHÔNG dùng [danger] cho adherence thấp — đỏ chỉ dành cho safety card / validation
  /// error (xem CLAUDE.md §11.3.4).
  static const Color success = Color(0xFF1CBA9F); // adherence ≥80%
  static const Color amberWarn = Color(0xFFE0912F); // adherence <80%

  static const Color background = Color(0xFFF7F9FB);
  static const Color border = Color(0xFFDDE5EF);
  static const Color muted = Color(0xFF5B6B85);
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
