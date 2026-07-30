import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
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
              Container(
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  color: Colors.white,
                  border: Border.all(color: AppColors.border),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: const Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(Icons.info_outline, color: AppColors.teal),
                    SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        'Các chức năng đặt lịch, đơn thuốc và theo dõi sức khoẻ '
                        'sẽ được bổ sung ở những module tiếp theo.',
                        style: TextStyle(fontSize: 14, height: 1.5),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
