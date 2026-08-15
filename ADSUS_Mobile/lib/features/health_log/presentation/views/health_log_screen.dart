import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../viewmodels/health_log_view_model.dart';
import '../widgets/health_log_calendar.dart';
import '../widgets/health_log_list.dart';
import 'add_health_log_screen.dart';

/// Màn hình chính hiển thị nhật ký sức khỏe (Module 9 - FT-35).
///
/// Giao diện:
///   - AppBar teal với tiêu đề "Nhật ký sức khỏe"
///   - Body: Column chứa HealthLogCalendar + HealthLogList
///   - FAB để navigate đến AddHealthLogScreen
class HealthLogScreen extends ConsumerWidget {
  const HealthLogScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Nhật ký sức khỏe'),
        backgroundColor: AppColors.teal,
        foregroundColor: Colors.white,
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: const [
              HealthLogCalendar(),
              SizedBox(height: 20),
              HealthLogList(),
            ],
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () {
          final selectedDate = ref.read(selectedDateProvider);
          _navigateToAdd(context, selectedDate);
        },
        backgroundColor: AppColors.teal,
        child: const Icon(Icons.add, color: Colors.white),
      ),
    );
  }

  void _navigateToAdd(BuildContext context, DateTime selectedDate) {
    Navigator.push(
      context,
      MaterialPageRoute<void>(
        builder: (_) => AddHealthLogScreen(selectedDate: selectedDate),
      ),
    );
  }
}
