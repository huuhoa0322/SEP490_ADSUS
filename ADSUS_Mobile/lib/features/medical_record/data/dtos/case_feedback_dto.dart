/// DTO khớp 1:1 `CaseFeedbackResponse` (FT-37).
class CaseFeedbackDto {
  const CaseFeedbackDto({
    required this.id,
    required this.rating,
    this.content,
    required this.submittedAt,
  });

  final String id;
  final int rating;
  final String? content;
  final String submittedAt;

  factory CaseFeedbackDto.fromJson(Map<String, dynamic> json) => CaseFeedbackDto(
        id: json['id'] as String,
        rating: json['rating'] as int,
        content: json['content'] as String?,
        submittedAt: json['submittedAt'] as String,
      );
}
