import 'package:flutter/material.dart';

import '../../../../core/theme/app_theme.dart';

/// Badge hiển thị tỉ lệ tuân thủ thuốc của bệnh nhân.
///
/// Quy tắc nghiệp vụ (CLAUDE.md §11.3.4):
///   - adherence ≥ 80%  → variant "good"   (AppColors.success = teal)
///   - adherence < 80%  → variant "warn"   (AppColors.amberWarn = amber)
///   - KHÔNG dùng màu đỏ / destructive cho adherence thấp — đỏ chỉ dành cho
///     safety card, validation error, "không được".
///
/// Giá trị không xác định (null/NaN) → variant "unknown" (AppColors.muted).
///
/// [percent] Giá trị 0..100. Null/undefined/NaN → variant "unknown".
/// [label]   Label tuỳ biến, vd: "tuần này", "tháng này".
class AdherencePillBadge extends StatelessWidget {
  const AdherencePillBadge({
    super.key,
    required this.percent,
    this.label,
  });

  final num? percent;
  final String? label;

  @override
  Widget build(BuildContext context) {
    final variant = _deriveVariant(percent);
    final displayText = percent == null || percent!.isNaN ? '—' : '${percent!.round()}%';

    final (bgColor, borderColor, textColor) = _variantStyles(variant);

    return Container(
      key: const Key('adherence-pill'),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: BorderRadius.circular(999),
        border: Border(top: BorderSide(color: borderColor, width: 2)),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            displayText,
            style: TextStyle(
              color: textColor,
              fontSize: 13,
              fontWeight: FontWeight.w700,
              fontFamily: 'JetBrains Mono',
            ),
          ),
          if (label != null) ...[
            const SizedBox(width: 4),
            Text(
              label!,
              style: TextStyle(
                color: textColor.withValues(alpha: 0.8),
                fontSize: 11,
                fontWeight: FontWeight.w400,
              ),
            ),
          ],
        ],
      ),
    );
  }

  /// Derive variant từ percent value.
  _AdherenceVariant _deriveVariant(num? percent) {
    if (percent == null || percent.isNaN) return _AdherenceVariant.unknown;
    return percent >= 80 ? _AdherenceVariant.good : _AdherenceVariant.warn;
  }

  /// Style tuple (bgColor, borderColor, textColor) theo variant.
  (Color, Color, Color) _variantStyles(_AdherenceVariant variant) {
    switch (variant) {
      case _AdherenceVariant.good:
        return (
          AppColors.teal.withValues(alpha: 0.1),
          AppColors.success.withValues(alpha: 0.3),
          AppColors.success,
        );
      case _AdherenceVariant.warn:
        return (
          AppColors.amberWarn.withValues(alpha: 0.1),
          AppColors.amberWarn.withValues(alpha: 0.3),
          AppColors.amberWarn,
        );
      case _AdherenceVariant.unknown:
        return (
          AppColors.muted.withValues(alpha: 0.1),
          AppColors.muted.withValues(alpha: 0.3),
          AppColors.muted,
        );
    }
  }
}

enum _AdherenceVariant { good, warn, unknown }
