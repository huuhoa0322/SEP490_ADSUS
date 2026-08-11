import 'package:flutter/material.dart';

import '../../../../../../core/theme/app_theme.dart';

/// Ô chọn giờ khám trong grid SCR-21.
///
/// Hai trạng thái: thường (viền xám) và đã chọn (nền teal, chữ trắng). Không có trạng
/// thái disabled vì ViewModel chỉ truyền vào các slot Open — không bao giờ hiển thị slot
/// Closed/Full.
class SlotPill extends StatelessWidget {
  const SlotPill({
    super.key,
    required this.label,
    required this.subLabel,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final String? subLabel;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(10),
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 6),
        decoration: BoxDecoration(
          color: selected ? AppColors.teal : Colors.white,
          border: Border.all(
            color: selected ? AppColors.teal : AppColors.border,
          ),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Flexible(
              child: Text(
                label,
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: selected ? Colors.white : AppColors.navy,
                ),
                overflow: TextOverflow.ellipsis,
                maxLines: 1,
              ),
            ),
            if (subLabel != null) ...[
              const SizedBox(height: 2),
              Flexible(
                child: Text(
                  subLabel!,
                  style: TextStyle(
                    fontSize: 10,
                    color: selected ? Colors.white70 : AppColors.muted,
                  ),
                  overflow: TextOverflow.ellipsis,
                  maxLines: 1,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
