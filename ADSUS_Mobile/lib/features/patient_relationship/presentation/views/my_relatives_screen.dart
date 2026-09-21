import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../viewmodels/my_relatives_view_model.dart';
import 'add_relative_screen.dart';
import 'widgets/relative_card.dart';

/// Màn hình Danh sách người thân (My Relatives).
///
/// Chức năng:
///   - Xem danh sách người thân đã lưu
///   - Thêm người thân mới
///   - Xóa người thân
class MyRelativesScreen extends ConsumerWidget {
  const MyRelativesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(myRelativesViewModelProvider);

    // Listen for state changes
    ref.listen<MyRelativesState>(myRelativesViewModelProvider, (prev, next) {
      // Show error snackbar
      if (prev?.errorMessage == null && next.errorMessage != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(next.errorMessage!),
            backgroundColor: AppColors.danger,
          ),
        );
        ref.read(myRelativesViewModelProvider.notifier).clearError();
      }
    });

    return Scaffold(
      appBar: AppBar(
        title: const Text('Danh bạ người thân'),
        actions: [
          IconButton(
            icon: const Icon(Icons.add),
            tooltip: 'Thêm người thân',
            onPressed: () => _navigateToAddRelative(context, ref),
          ),
        ],
      ),
      body: SafeArea(
        child: _buildBody(context, ref, state),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _navigateToAddRelative(context, ref),
        icon: const Icon(Icons.person_add),
        label: const Text('Thêm người thân'),
        backgroundColor: AppColors.teal,
        foregroundColor: Colors.white,
      ),
    );
  }

  Widget _buildBody(BuildContext context, WidgetRef ref, MyRelativesState state) {
    if (state.isLoading && state.relatives.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.errorMessage != null && state.relatives.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline, size: 64, color: AppColors.danger),
              const SizedBox(height: 16),
              Text(
                state.errorMessage!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: AppColors.danger),
              ),
              const SizedBox(height: 16),
              ElevatedButton(
                onPressed: () => ref
                    .read(myRelativesViewModelProvider.notifier)
                    .loadRelatives(),
                child: const Text('THỬ LẠI'),
              ),
            ],
          ),
        ),
      );
    }

    if (state.relatives.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.people_outline, size: 64, color: AppColors.muted),
              const SizedBox(height: 16),
              const Text(
                'Chưa có người thân nào trong danh bạ.',
                textAlign: TextAlign.center,
                style: TextStyle(color: AppColors.muted, fontSize: 15),
              ),
              const SizedBox(height: 8),
              const Text(
                'Nhấn nút bên dưới để thêm.',
                textAlign: TextAlign.center,
                style: TextStyle(color: AppColors.muted, fontSize: 14),
              ),
            ],
          ),
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: () => ref
          .read(myRelativesViewModelProvider.notifier)
          .loadRelatives(),
      child: ListView.builder(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 80), // Extra bottom padding for FAB
        itemCount: state.relatives.length,
        itemBuilder: (context, index) {
          final relative = state.relatives[index];
          return RelativeCard(
            relative: relative,
          );
        },
      ),
    );
  }

  Future<void> _navigateToAddRelative(BuildContext context, WidgetRef ref) async {
    final result = await Navigator.of(context).push<bool>(
      MaterialPageRoute<bool>(
        builder: (_) => const AddRelativeScreen(),
      ),
    );
    if (result == true) {
      ref.read(myRelativesViewModelProvider.notifier).loadRelatives();
    }
  }
}
