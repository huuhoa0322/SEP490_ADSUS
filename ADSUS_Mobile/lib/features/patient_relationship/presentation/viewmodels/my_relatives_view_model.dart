import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/patient_relationship.dart';

/// State cho màn hình Danh sách người thân.
class MyRelativesState {
  const MyRelativesState({
    this.relatives = const [],
    this.isLoading = false,
    this.isDeleting = false,
    this.errorMessage,
    this.deletedId,
  });

  final List<PatientRelationship> relatives;
  final bool isLoading;
  final bool isDeleting;
  final String? errorMessage;

  /// ID của relationship vừa xóa thành công (để hiện snackbar).
  final String? deletedId;

  MyRelativesState copyWith({
    List<PatientRelationship>? relatives,
    bool? isLoading,
    bool? isDeleting,
    String? errorMessage,
    String? deletedId,
    bool clearError = false,
    bool clearDeleted = false,
  }) {
    return MyRelativesState(
      relatives: relatives ?? this.relatives,
      isLoading: isLoading ?? this.isLoading,
      isDeleting: isDeleting ?? this.isDeleting,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      deletedId: clearDeleted ? null : (deletedId ?? this.deletedId),
    );
  }
}

/// ViewModel cho màn hình MyRelativesScreen.
class MyRelativesViewModel extends Notifier<MyRelativesState> {
  @override
  MyRelativesState build() {
    // Tự nạp danh sách khi mở màn hình
    Future.microtask(() => loadRelatives());
    return const MyRelativesState(isLoading: true);
  }

  /// Tải danh sách người thân từ API.
  Future<void> loadRelatives() async {
    state = state.copyWith(isLoading: true, clearError: true);
    try {
      final relatives = await ref
          .read(patientRelationshipRepositoryProvider)
          .getRelatives();
      state = state.copyWith(
        relatives: relatives,
        isLoading: false,
      );
    } on ApiException catch (e) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: e.message,
      );
    } catch (e, st) {
      debugPrint('[DEBUG] MyRelativesViewModel.loadRelatives error: $e\n$st');
      state = state.copyWith(
        isLoading: false,
        errorMessage: 'Không tải được danh sách người thân.',
      );
    }
  }

  /// Xóa một người thân.
  Future<bool> deleteRelative(String relationshipId) async {
    state = state.copyWith(isDeleting: true, clearError: true);
    try {
      await ref
          .read(patientRelationshipRepositoryProvider)
          .deleteRelative(relationshipId);

      // Xóa khỏi danh sách local
      final updatedList =
          state.relatives.where((r) => r.relationshipId != relationshipId).toList();

      state = state.copyWith(
        relatives: updatedList,
        isDeleting: false,
        deletedId: relationshipId,
      );
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(
        isDeleting: false,
        errorMessage: e.message,
      );
      return false;
    } catch (e, st) {
      debugPrint('[DEBUG] MyRelativesViewModel.deleteRelative error: $e\n$st');
      state = state.copyWith(
        isDeleting: false,
        errorMessage: 'Xóa người thân thất bại.',
      );
      return false;
    }
  }

  /// Xóa flag deletedId sau khi snackbar đã hiển thị.
  void clearDeletedFlag() {
    state = state.copyWith(clearDeleted: true);
  }

  /// Xóa lỗi.
  void clearError() {
    state = state.copyWith(clearError: true);
  }
}

final myRelativesViewModelProvider =
    NotifierProvider<MyRelativesViewModel, MyRelativesState>(
  MyRelativesViewModel.new,
);
