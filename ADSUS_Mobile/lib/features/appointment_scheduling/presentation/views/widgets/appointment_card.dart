import 'package:flutter/material.dart';

import '../../../../../../core/theme/app_theme.dart';
import '../../../domain/entities/appointment.dart';
import 'appointment_detail_sheet.dart';

/// Một thẻ cuộc hẹn trong SCR-22.
///
/// Hiển thị thông tin cơ bản: ngày, giờ, bác sĩ.
/// Tap vào card để xem chi tiết và thực hiện action (đặt lại/hủy).
class AppointmentCard extends StatelessWidget {
  const AppointmentCard({
    super.key,
    required this.appointment,
    required this.busy,
    this.onCancel,
    this.onReschedule,
    this.onSyncCalendar,
    this.syncedToCalendar = false,
  });

  final Appointment appointment;
  final bool busy;
  final VoidCallback? onCancel;
  final VoidCallback? onReschedule;

  /// Callback khi user bấm icon "Thêm vào lịch".
  final VoidCallback? onSyncCalendar;

  /// Local bookkeeping — set khi đã từng mở Calendar dialog.
  final bool syncedToCalendar;

  @override
  Widget build(BuildContext context) {
    final isBooked = appointment.isBooked;
    final isExpired = appointment.isExpired;

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.border),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.05),
            blurRadius: 4,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: () => _showDetail(context),
          borderRadius: BorderRadius.circular(14),
          child: Container(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                // Left accent bar
                Container(
                  width: 4,
                  height: 60,
                  decoration: BoxDecoration(
                    color: isBooked
                        ? (isExpired ? const Color(0xFFFF9800) : AppColors.teal)
                        : AppColors.border,
                    borderRadius: BorderRadius.circular(2),
                  ),
                ),
                const SizedBox(width: 16),

                // Content
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      // Ngày khám
                      Text(
                        'Lịch khám: ${_formatDate(appointment.slotDate)}',
                        style: const TextStyle(
                          fontSize: 13,
                          color: AppColors.muted,
                        ),
                      ),
                      const SizedBox(height: 4),
                      // Giờ khám
                      Text(
                        '${appointment.startTime ?? '—'} - ${appointment.endTime ?? '—'}',
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                          color: AppColors.navy,
                        ),
                      ),
                      const SizedBox(height: 4),
                      // Bác sĩ
                      Text(
                        'BS. ${appointment.doctorName ?? '—'}',
                        style: const TextStyle(
                          fontSize: 13,
                          color: AppColors.muted,
                        ),
                      ),
                    ],
                  ),
                ),

                // Status badge và sync button
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    _Badge(booked: isBooked, expired: isExpired),
                    if (isBooked && !isExpired) ...[
                      const SizedBox(height: 8),
                      _SyncCalendarButton(
                        onPressed: busy ? null : onSyncCalendar,
                        alreadySynced: syncedToCalendar,
                      ),
                    ],
                  ],
                ),

                // Arrow indicator
                const SizedBox(width: 8),
                const Icon(
                  Icons.chevron_right,
                  color: AppColors.muted,
                  size: 24,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _showDetail(BuildContext context) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => AppointmentDetailSheet(
        appointment: appointment,
        onCancel: onCancel,
        onReschedule: onReschedule,
      ),
    );
  }

  String _formatDate(DateTime? date) {
    if (date == null) return '—';
    return '${date.day.toString().padLeft(2, '0')}/'
        '${date.month.toString().padLeft(2, '0')}/'
        '${date.year}';
  }
}

class _Badge extends StatelessWidget {
  const _Badge({required this.booked, required this.expired});
  final bool booked;
  final bool expired;

  @override
  Widget build(BuildContext context) {
    // Xác định badge: ưu tiên cancelled > expired > booked
    String label;
    Color bgColor;
    Color textColor;

    if (!booked) {
      label = 'Đã hủy';
      bgColor = const Color(0xFFEEEEEE);
      textColor = AppColors.muted;
    } else if (expired) {
      label = 'Đã qua';
      bgColor = const Color(0xFFFFF3E0);
      textColor = const Color(0xFFE65100);
    } else {
      label = 'Đã xác nhận';
      bgColor = const Color(0xFFE5F5EE);
      textColor = const Color(0xFF1E9E6B);
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w700,
          color: textColor,
        ),
      ),
    );
  }
}

/// Icon-button "Thêm vào lịch" / "Đã thêm vào lịch" ở góc phải header card.
///
/// Khi đã sync thì icon đổi sang dạng `event_available` với màu teal nhạt và tooltip
/// gợi ý; bấm lại vẫn được (user có thể muốn thêm 1 lần nữa nếu trước đó huỷ trong
/// Calendar app nhưng chưa xoá khỏi app).
class _SyncCalendarButton extends StatelessWidget {
  const _SyncCalendarButton({
    required this.onPressed,
    required this.alreadySynced,
  });

  final VoidCallback? onPressed;
  final bool alreadySynced;

  @override
  Widget build(BuildContext context) {
    final icon = alreadySynced ? Icons.event_available : Icons.event_note;
    final color = alreadySynced ? AppColors.teal : AppColors.navy;
    final tooltip = alreadySynced ? 'Đã thêm vào lịch — bấm để thêm lại' : 'Thêm vào lịch';

    return Tooltip(
      message: tooltip,
      child: IconButton(
        onPressed: onPressed,
        icon: Icon(icon, color: color, size: 22),
        // VisualButton gọn để không phá layout card — padding mặc định của IconButton
        // (44x44) vẫn đủ lớn để bấm dễ theo Material guidelines.
        visualDensity: VisualDensity.compact,
        padding: const EdgeInsets.all(6),
        constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
      ),
    );
  }
}
