import 'package:flutter/material.dart';

import '../../../../../../core/theme/app_theme.dart';
import '../../../domain/entities/appointment.dart';

/// Bottom sheet hiển thị chi tiết lịch khám.
///
/// Hiện thông tin đầy đủ và các action: Đặt lại lịch, Hủy lịch.
class AppointmentDetailSheet extends StatelessWidget {
  const AppointmentDetailSheet({
    super.key,
    required this.appointment,
    this.onCancel,
    this.onReschedule,
  });

  final Appointment appointment;
  final VoidCallback? onCancel;
  final VoidCallback? onReschedule;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Handle bar
          Center(
            child: Container(
              width: 40,
              height: 4,
              margin: const EdgeInsets.only(bottom: 20),
              decoration: BoxDecoration(
                color: AppColors.border,
                borderRadius: BorderRadius.circular(2),
              ),
            ),
          ),

          // Header
          Row(
            children: [
              Expanded(
                child: Text(
                  'Chi tiết lịch khám',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: AppColors.navy,
                  ),
                ),
              ),
              _StatusBadge(appointment: appointment),
            ],
          ),
          const SizedBox(height: 24),

          // Thông tin lịch khám
          _InfoRow(
            icon: Icons.calendar_today,
            label: 'Ngày khám',
            value: _formatDate(appointment.slotDate),
          ),
          const SizedBox(height: 12),
          _InfoRow(
            icon: Icons.access_time,
            label: 'Giờ khám',
            value: '${appointment.startTime ?? '—'} - ${appointment.endTime ?? '—'}',
          ),
          const SizedBox(height: 12),
          _InfoRow(
            icon: Icons.person,
            label: 'Bác sĩ',
            value: appointment.doctorName != null
                ? 'BS. ${appointment.doctorName}'
                : '—',
          ),

          // Lý do khám (nếu có)
          if (appointment.reason != null && appointment.reason!.isNotEmpty) ...[
            const SizedBox(height: 12),
            _InfoRow(
              icon: Icons.note,
              label: 'Lý do khám',
              value: appointment.reason!,
            ),
          ],

          // Lý do hủy (nếu đã hủy và chưa hết hạn)
          // Đã qua thì không hiện lý do hủy vì cuộc hẹn đã kết thúc
          if (appointment.isCancelled &&
              !appointment.isExpired &&
              appointment.cancelledReason != null &&
              appointment.cancelledReason!.isNotEmpty) ...[
            const SizedBox(height: 12),
            _InfoRow(
              icon: Icons.cancel,
              label: 'Lý do hủy',
              value: appointment.cancelledReason!,
              valueColor: AppColors.danger,
            ),
          ],

          const SizedBox(height: 24),
          const Divider(),
          const SizedBox(height: 16),

          // Actions - chỉ hiện nếu appointment còn BOOKED và chưa hết hạn
          if (appointment.isBooked && !appointment.isExpired) ...[
            ElevatedButton.icon(
              onPressed: () {
                Navigator.pop(context); // Đóng sheet trước
                onReschedule?.call();
              },
              icon: const Icon(Icons.edit_calendar),
              label: const Text('Đặt lại lịch'),
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.teal,
                foregroundColor: Colors.white,
                padding: const EdgeInsets.symmetric(vertical: 14),
              ),
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: () {
                Navigator.pop(context); // Đóng sheet trước
                onCancel?.call();
              },
              icon: const Icon(Icons.close),
              label: const Text('Hủy lịch'),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColors.danger,
                side: const BorderSide(color: AppColors.danger),
                padding: const EdgeInsets.symmetric(vertical: 14),
              ),
            ),
          ],

          const SizedBox(height: 12),
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Đóng'),
          ),

          // Safe area padding
          SizedBox(height: MediaQuery.of(context).padding.bottom),
        ],
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

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.appointment});

  final Appointment appointment;

  @override
  Widget build(BuildContext context) {
    final isBooked = appointment.isBooked;
    final isExpired = appointment.isExpired;

    String label;
    Color bgColor;
    Color textColor;

    if (!isBooked) {
      label = 'Đã hủy';
      bgColor = const Color(0xFFEEEEEE);
      textColor = AppColors.muted;
    } else if (isExpired) {
      label = 'Đã qua';
      bgColor = const Color(0xFFFFF3E0);
      textColor = const Color(0xFFE65100);
    } else {
      label = 'Đã xác nhận';
      bgColor = const Color(0xFFE5F5EE);
      textColor = const Color(0xFF1E9E6B);
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w700,
          color: textColor,
        ),
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.icon,
    required this.label,
    required this.value,
    this.valueColor,
  });

  final IconData icon;
  final String label;
  final String value;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 20, color: AppColors.muted),
        const SizedBox(width: 12),
        SizedBox(
          width: 90,
          child: Text(
            label,
            style: const TextStyle(
              fontSize: 14,
              color: AppColors.muted,
            ),
          ),
        ),
        Expanded(
          child: Text(
            value,
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w600,
              color: valueColor ?? AppColors.navy,
            ),
          ),
        ),
      ],
    );
  }
}
