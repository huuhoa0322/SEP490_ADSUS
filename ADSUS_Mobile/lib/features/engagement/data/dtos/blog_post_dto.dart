/// DTOs cho Module 10 — Blog Sức khỏe (UC-23).
///
/// API endpoints (PATIENT-only, đã đăng nhập):
///   GET /api/v1/blog-posts?page=&pageSize=
///   GET /api/v1/blog-posts/{id}
library;

/// Item trong response GET /api/v1/blog-posts (list).
///
/// Backend DTO: [BlogPostListItemResponse] — chỉ trả về 4 field public (GB-05):
/// id, title, publishedAt, authorName. KHÔNG có category, coverImage, isAiGenerated,
/// viewCount ở schema hiện tại.
class BlogPostListItemDto {
  const BlogPostListItemDto({
    required this.id,
    required this.title,
    required this.publishedAt,
    required this.authorName,
  });

  final String id;
  final String title;
  final DateTime publishedAt;
  final String authorName;

  factory BlogPostListItemDto.fromJson(Map<String, dynamic> json) {
    return BlogPostListItemDto(
      id: json['id'] as String,
      title: json['title'] as String,
      publishedAt: DateTime.parse(json['publishedAt'] as String),
      authorName: json['authorName'] as String? ?? '',
    );
  }
}

/// Detail response GET /api/v1/blog-posts/{id}.
///
/// Backend DTO: [BlogPostDetailResponse] — thêm `content` (Markdown) so với list item.
/// Draft trả 404 theo GB-05 (không 403 để không leak status).
class BlogPostDetailDto {
  const BlogPostDetailDto({
    required this.id,
    required this.title,
    required this.content,
    required this.publishedAt,
    required this.authorName,
  });

  final String id;
  final String title;
  final String content;
  final DateTime publishedAt;
  final String authorName;

  factory BlogPostDetailDto.fromJson(Map<String, dynamic> json) {
    return BlogPostDetailDto(
      id: json['id'] as String,
      title: json['title'] as String,
      content: json['content'] as String,
      publishedAt: DateTime.parse(json['publishedAt'] as String),
      authorName: json['authorName'] as String? ?? '',
    );
  }
}