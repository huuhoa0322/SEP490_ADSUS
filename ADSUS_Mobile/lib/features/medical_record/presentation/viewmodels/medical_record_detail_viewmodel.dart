import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/medical_record_case.dart';

class MedicalRecordDetailState {
  const MedicalRecordDetailState({
    this.caseDetail,
    this.isLoading = false,
    this.errorMessage,
  });

  final MedicalRecordCase? caseDetail;
  final bool isLoading;
  final String? errorMessage;

  MedicalRecordDetailState copyWith({
    MedicalRecordCase? caseDetail,
    bool? isLoading,
    String? errorMessage,
    bool clearError = false,
  }) {
    return MedicalRecordDetailState(
      caseDetail: caseDetail ?? this.caseDetail,
      isLoading: isLoading ?? this.isLoading,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

/// Không dùng `.family` (giữ nhất quán với pattern Notifier trơn của module này) — vì
/// vậy đây là 1 instance dùng chung cho mọi lượt xem chi tiết. `loadDetail()` PHẢI dựng
/// state mới (bỏ `caseDetail` cũ) ngay khi được gọi, trước khi `await` — nếu không, xem
/// lượt khám A rồi bấm sang lượt khám B sẽ thoáng hiện lại nội dung của A trước khi API
/// trả lời (khoá chặn bởi test thứ 3 ở trên).
class MedicalRecordDetailViewModel extends Notifier<MedicalRecordDetailState> {
  @override
  MedicalRecordDetailState build() => const MedicalRecordDetailState();

  Future<void> loadDetail(String caseId) async {
    // Reset về rỗng NGAY — không giữ caseDetail cũ trong lúc chờ.
    state = const MedicalRecordDetailState(isLoading: true);
    try {
      final detail =
          await ref.read(medicalRecordRepositoryProvider).getRecordDetail(caseId);
      state = state.copyWith(caseDetail: detail, isLoading: false);
    } on ApiException catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.message);
    }
  }
}

final medicalRecordDetailViewModelProvider =
    NotifierProvider<MedicalRecordDetailViewModel, MedicalRecordDetailState>(
  MedicalRecordDetailViewModel.new,
);
