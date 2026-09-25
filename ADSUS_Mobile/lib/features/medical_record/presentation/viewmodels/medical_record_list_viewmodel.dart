import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/medical_record_summary.dart';

/// State phân tách hồ sơ bản thân / người thân — giống MyAppointmentsState.
class MedicalRecordListState {
  const MedicalRecordListState({
    this.selfRecords = const [],
    this.relativeRecords = const [],
    this.isLoading = false,
    this.errorMessage,
    this.filterScope = 'SELF',
  });

  final List<MedicalRecordSummary> selfRecords;
  final List<MedicalRecordSummary> relativeRecords;
  final bool isLoading;
  final String? errorMessage;

  /// 'SELF' hoặc 'RELATIVE'
  final String filterScope;

  List<MedicalRecordSummary> get filteredRecords {
    switch (filterScope) {
      case 'RELATIVE':
        return relativeRecords;
      case 'SELF':
      default:
        return selfRecords;
    }
  }

  int get selfCount => selfRecords.length;
  int get relativeCount => relativeRecords.length;

  /// Alias tương thích ngược cho code cũ truy cập state.records
  List<MedicalRecordSummary> get records => filteredRecords;

  MedicalRecordListState copyWith({
    List<MedicalRecordSummary>? selfRecords,
    List<MedicalRecordSummary>? relativeRecords,
    bool? isLoading,
    String? errorMessage,
    String? filterScope,
    bool clearError = false,
  }) {
    return MedicalRecordListState(
      selfRecords: selfRecords ?? this.selfRecords,
      relativeRecords: relativeRecords ?? this.relativeRecords,
      isLoading: isLoading ?? this.isLoading,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      filterScope: filterScope ?? this.filterScope,
    );
  }
}

class MedicalRecordListViewModel extends Notifier<MedicalRecordListState> {
  @override
  MedicalRecordListState build() {
    Future.microtask(load);
    return const MedicalRecordListState(isLoading: true);
  }

  Future<void> load() async {
    state = state.copyWith(isLoading: true, clearError: true);
    try {
      final repo = ref.read(medicalRecordRepositoryProvider);

      // Gọi cả 2 API song song (self + relative)
      final results = await Future.wait([
        repo.getMyRecords(),
        repo.getRelativeCases(),
      ]);

      state = state.copyWith(
        selfRecords: results[0],
        relativeRecords: results[1],
        isLoading: false,
      );
    } on ApiException catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.message);
    }
  }

  void setFilterScope(String scope) {
    state = state.copyWith(filterScope: scope);
  }
}

final medicalRecordListViewModelProvider =
    NotifierProvider<MedicalRecordListViewModel, MedicalRecordListState>(
  MedicalRecordListViewModel.new,
);
