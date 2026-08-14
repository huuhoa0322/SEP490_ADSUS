import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/medical_record_summary.dart';

class MedicalRecordListState {
  const MedicalRecordListState({
    this.records = const [],
    this.isLoading = false,
    this.errorMessage,
  });

  final List<MedicalRecordSummary> records;
  final bool isLoading;
  final String? errorMessage;

  MedicalRecordListState copyWith({
    List<MedicalRecordSummary>? records,
    bool? isLoading,
    String? errorMessage,
    bool clearError = false,
  }) {
    return MedicalRecordListState(
      records: records ?? this.records,
      isLoading: isLoading ?? this.isLoading,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
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
      final records = await ref.read(medicalRecordRepositoryProvider).getMyRecords();
      state = state.copyWith(records: records, isLoading: false);
    } on ApiException catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.message);
    }
  }
}

final medicalRecordListViewModelProvider =
    NotifierProvider<MedicalRecordListViewModel, MedicalRecordListState>(
  MedicalRecordListViewModel.new,
);
