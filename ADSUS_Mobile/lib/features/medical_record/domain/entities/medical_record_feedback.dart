/// Entity feedback ca khám (FT-37).
class MedicalRecordFeedback {
  const MedicalRecordFeedback({
    required this.id,
    required this.rating,
    this.content,
    required this.submittedAt,
  });

  final String id;
  final int rating;
  final String? content;
  final DateTime submittedAt;
}
