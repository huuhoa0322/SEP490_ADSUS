import 'package:flutter/material.dart';

import '../../../../../../core/theme/app_theme.dart';

/// Bottom sheet chọn lý do hủy cuộc hẹn (UC-14 BR-02, AF-02).
///
/// Trả về chuỗi lý do qua Navigator.pop nếu người dùng xác nhận, hoặc null nếu đóng.
/// Lý do BẮT BUỘC — nút xác nhận bị disabled cho tới khi chọn.
class CancelReasonSheet extends StatefulWidget {
  const CancelReasonSheet({super.key});

  static const _presetReasons = [
    'Bận công việc / Không sắp xếp được thời gian',
    'Đã hết triệu chứng',
    'Chuyển sang khám ở cơ sở khác',
    'Lý do khác',
  ];

  @override
  State<CancelReasonSheet> createState() => _CancelReasonSheetState();
}

class _CancelReasonSheetState extends State<CancelReasonSheet> {
  String? _selectedReason;
  final _noteController = TextEditingController();

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  String get _resolvedReason {
    if (_selectedReason == 'Lý do khác') {
      final note = _noteController.text.trim();
      return note.isNotEmpty ? note : 'Lý do khác';
    }
    return _selectedReason ?? '';
  }

  bool get _canConfirm {
    if (_selectedReason == null) return false;
    if (_selectedReason == 'Lý do khác' &&
        _noteController.text.trim().isEmpty) {
      return false;
    }
    return true;
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(
          left: 20,
          right: 20,
          top: 20,
          bottom: 20 + MediaQuery.of(context).viewInsets.bottom,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Hủy lịch khám',
              style: TextStyle(
                fontFamily: 'serif',
                fontSize: 18,
                fontWeight: FontWeight.w700,
                color: AppColors.navy,
              ),
            ),
            const SizedBox(height: 6),
            const Text(
              'Vui lòng cho biết lý do hủy trước khi xác nhận. '
              'Đây là bước bắt buộc.',
              style: TextStyle(fontSize: 13, color: AppColors.muted),
            ),
            const SizedBox(height: 16),
            ...CancelReasonSheet._presetReasons.map(
              (reason) => RadioListTile<String>(
                value: reason,
                groupValue: _selectedReason,
                title: Text(reason, style: const TextStyle(fontSize: 14)),
                contentPadding: EdgeInsets.zero,
                dense: true,
                onChanged: (v) => setState(() => _selectedReason = v),
              ),
            ),
            if (_selectedReason == 'Lý do khác') ...[
              const SizedBox(height: 8),
              TextField(
                controller: _noteController,
                maxLines: 2,
                onChanged: (_) => setState(() {}),
                decoration: const InputDecoration(
                  hintText: 'Nhập lý do cụ thể...',
                ),
              ),
            ],
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: _canConfirm
                  ? () => Navigator.of(context).pop(_resolvedReason)
                  : null,
              style: ElevatedButton.styleFrom(
                backgroundColor:
                    _canConfirm ? AppColors.danger : AppColors.border,
                foregroundColor:
                    _canConfirm ? Colors.white : AppColors.muted,
              ),
              child: const Text('XÁC NHẬN HỦY LỊCH'),
            ),
            const SizedBox(height: 4),
            TextButton(
              onPressed: () => Navigator.of(context).pop(),
              child: const Text('Đóng'),
            ),
          ],
        ),
      ),
    );
  }
}

/// Helper để View gọi cho gọn — không cần biết cấu trúc sheet bên trong.
Future<String?> showCancelReasonSheet(BuildContext context) {
  return showModalBottomSheet<String>(
    context: context,
    isScrollControlled: true,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
    ),
    builder: (_) => const CancelReasonSheet(),
  );
}
