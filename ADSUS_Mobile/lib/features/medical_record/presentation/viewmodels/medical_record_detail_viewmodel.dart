import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/medical_record_case.dart';

class MedicalRecordDetailState {
  const MedicalRecordDetailState({
    this.caseId,
    this.caseDetail,
    this.isLoading = false,
    this.errorMessage,
  });

  /// Case mà state này thuộc về — Screen dùng để lọc bỏ state cũ của 1 case khác trước khi
  /// hiển thị bất kỳ nội dung nào (dữ liệu lẫn lỗi), thay vì chỉ suy luận từ `caseDetail == null`.
  final String? caseId;
  final MedicalRecordCase? caseDetail;
  final bool isLoading;
  final String? errorMessage;

  MedicalRecordDetailState copyWith({
    String? caseId,
    MedicalRecordCase? caseDetail,
    bool? isLoading,
    String? errorMessage,
    bool clearError = false,
  }) {
    return MedicalRecordDetailState(
      caseId: caseId ?? this.caseId,
      caseDetail: caseDetail ?? this.caseDetail,
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
      // Trong lúc chờ, 1 lượt loadDetail(case khác) có thể đã ghi đè caseId — nếu request
      // của LƯỢT NÀY trả lời trễ hơn (out-of-order), không được ghi dữ liệu case cũ đè lên
      // caseId hiện tại. Phát hiện qua whole-branch review 15/08/2026: check state.caseId
      // (từ Screen) đủ chặn thoáng hiện nhầm khi bấm liên tiếp, nhưng KHÔNG chặn được data
      // sai khi 2 request chồng nhau trả lời không đúng thứ tự.
      if (caseId != state.caseId) return;
      state = state.copyWith(caseDetail: detail, isLoading: false);
    } on ApiException catch (e) {
      if (caseId != state.caseId) return;
      state = state.copyWith(isLoading: false, errorMessage: e.message);
    }
  }
}

final medicalRecordDetailViewModelProvider =
    NotifierProvider<MedicalRecordDetailViewModel, MedicalRecordDetailState>(
  MedicalRecordDetailViewModel.new,
);
