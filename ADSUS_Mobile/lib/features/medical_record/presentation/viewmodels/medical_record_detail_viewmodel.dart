import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/medical_record_case.dart';
import '../../domain/entities/medical_record_feedback.dart';

class MedicalRecordDetailState {
  const MedicalRecordDetailState({
    this.caseId,
    this.caseDetail,
    this.feedback,
    this.isLoading = false,
    this.errorMessage,
  });

  /// Case mà state này thuộc về — Screen dùng để lọc bỏ state cũ của 1 case khác trước khi
  /// hiển thị bất kỳ nội dung nào (dữ liệu lẫn lỗi), thay vì chỉ suy luận từ `caseDetail == null`.
  final String? caseId;
  final MedicalRecordCase? caseDetail;
  final MedicalRecordFeedback? feedback;
  final bool isLoading;
  final String? errorMessage;

  MedicalRecordDetailState copyWith({
    String? caseId,
    MedicalRecordCase? caseDetail,
    MedicalRecordFeedback? feedback,
    bool? isLoading,
    String? errorMessage,
    bool clearError = false,
  }) {
    return MedicalRecordDetailState(
      caseId: caseId ?? this.caseId,
      caseDetail: caseDetail ?? this.caseDetail,
      feedback: feedback ?? this.feedback,
      isLoading: isLoading ?? this.isLoading,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

/// Không dùng `.family` (giữ nhất quán với pattern Notifier trơn của module này) — vì
/// vậy đây là 1 instance dùng chung cho mọi lượt xem chi tiết. `loadDetail()` PHẢI dựng
/// state mới (bỏ `caseDetail` cũ, gán `caseId` mới) ngay khi được gọi, trước khi `await` —
/// nếu không, xem lượt khám A rồi bấm sang lượt khám B sẽ thoáng hiện lại nội dung (hoặc lỗi)
/// của A trước khi API trả lời (khoá chặn bởi test thứ 3 ở trên).
class MedicalRecordDetailViewModel extends Notifier<MedicalRecordDetailState> {
  @override
  MedicalRecordDetailState build() => const MedicalRecordDetailState();

  Future<void> loadDetail(String caseId) async {
    // Reset về rỗng NGAY, gán caseId mới — không giữ caseDetail/errorMessage cũ trong lúc chờ.
    state = MedicalRecordDetailState(isLoading: true, caseId: caseId);
    try {
      final detail =
          await ref.read(medicalRecordRepositoryProvider).getRecordDetail(caseId);
      if (caseId != state.caseId) return;
      state = state.copyWith(caseDetail: detail, isLoading: false);
    } on ApiException catch (e) {
      if (caseId != state.caseId) return;
      state = state.copyWith(isLoading: false, errorMessage: e.message);
    }
  }

  /// Load feedback cho ca khám hiện tại (FT-37).
  Future<void> loadFeedback(String caseId) async {
    if (caseId != state.caseId) return;
    try {
      final fb =
          await ref.read(medicalRecordRepositoryProvider).getCaseFeedback(caseId);
      if (caseId != state.caseId) return;
      state = state.copyWith(feedback: fb);
    } on ApiException {
      // Feedback optional — không hiển thị lỗi, chỉ không hiện feedback.
    }
  }

  /// Gửi feedback cho ca khám hiện tại (FT-37).
  Future<void> submitFeedback(String caseId, int rating, String? content) async {
    if (caseId != state.caseId) return;
    state = state.copyWith(isLoading: true, clearError: true);
    try {
      await ref.read(medicalRecordRepositoryProvider).submitCaseFeedback(
            caseId,
            rating,
            content,
          );
      // Reload feedback sau khi submit thành công.
      await loadFeedback(caseId);
    } on ApiException catch (e) {
      if (caseId != state.caseId) return;
      state = state.copyWith(
        isLoading: false,
        errorMessage: e.message,
      );
    }
  }
}

final medicalRecordDetailViewModelProvider =
    NotifierProvider<MedicalRecordDetailViewModel, MedicalRecordDetailState>(
  MedicalRecordDetailViewModel.new,
);
