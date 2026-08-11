import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../../../appointment_scheduling/presentation/views/book_appointment_screen.dart';
import '../../../appointment_scheduling/presentation/views/my_appointments_screen.dart';
import '../../../medication_reminder/presentation/views/medication_reminder_screen.dart';
import '../viewmodels/auth_view_model.dart';
import 'profile_screen.dart';

/// Màn hình chính của bệnh nhân sau khi đăng nhập.
///
/// Đây là trang tạm để luồng đăng nhập có đích đến. Nội dung thật (lịch hẹn, đơn thuốc,
/// nhật ký sức khoẻ...) thuộc về các module khác.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(authViewModelProvider).session;

    return Scaffold(
      appBar: AppBar(
        title: const Text('ADSUS'),
        actions: [
          IconButton(
            tooltip: 'Hồ sơ cá nhân',
            icon: const Icon(Icons.person_outline),
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute<void>(builder: (_) => const ProfileScreen()),
            ),
          ),
          // UC-01 bước 4: đăng xuất phải với tới được ngay từ màn chính, không bắt người
          // dùng đi vòng qua màn hồ sơ mới tìm thấy.
          IconButton(
            tooltip: 'Đăng xuất',
            icon: const Icon(Icons.logout),
            onPressed: () => ref.read(authViewModelProvider.notifier).signOut(),
          ),
        ],
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Xin chào,',
                style: TextStyle(fontSize: 15, color: AppColors.muted),
              ),
              const SizedBox(height: 4),
              Text(
                session?.fullName ?? '',
                style: const TextStyle(
                  fontSize: 26,
                  fontWeight: FontWeight.bold,
                  color: AppColors.navy,
                ),
              ),
              const SizedBox(height: 28),
              // Module 7 SCR-19 — Nhắc uống thuốc.
              _QuickActionCard(
                icon: Icons.medication,
                title: 'Nhắc uống thuốc',
                subtitle: 'Xem lịch uống & ghi nhận đã uống',
                color: AppColors.teal,
                onTap: () => Navigator.of(context).push(
                  MaterialPageRoute<void>(
                    builder: (_) => const MedicationReminderScreen(),
                  ),
                ),
              ),
              const SizedBox(height: 16),

              // UC-13 — Đặt lịch hẹn.
              _QuickActionCard(
                icon: Icons.calendar_today,
                title: 'Đặt lịch hẹn',
                subtitle: 'Chọn bác sĩ và khung giờ khám',
                color: AppColors.primary,
                onTap: () => Navigator.of(context).push(
                  MaterialPageRoute<void>(
                    builder: (_) => const BookAppointmentScreen(),
                  ),
                ),
              ),
              const SizedBox(height: 16),

              // UC-14 — Lịch hẹn của tôi.
              _QuickActionCard(
                icon: Icons.event_note,
                title: 'Lịch hẹn của tôi',
                subtitle: 'Xem và huỷ lịch hẹn đã đặt',
                color: AppColors.accent,
                onTap: () => Navigator.of(context).push(
                  MaterialPageRoute<void>(
                    builder: (_) => const MyAppointmentsScreen(),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Card hành động nhanh cho HomeScreen.
class _QuickActionCard extends StatelessWidget {
  const _QuickActionCard({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.color,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(20),
      child: Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: Colors.white,
          border: Border.all(color: AppColors.border),
          borderRadius: BorderRadius.circular(20),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, color: color, size: 28),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                      color: AppColors.navy,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    style: const TextStyle(
                      fontSize: 13,
                      color: AppColors.muted,
                    ),
                  ),
                ],
              ),
            ),
            Icon(Icons.chevron_right, color: color.withValues(alpha: 0.5)),
          ],
        ),
      ),
    );
  }
}
