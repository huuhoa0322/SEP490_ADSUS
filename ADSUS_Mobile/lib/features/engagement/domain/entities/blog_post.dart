import '../../data/dtos/blog_post_dto.dart';

/// Một bài viết trong danh sách Blog Sức khỏe (UC-23, GB-05).
///
/// Backend chỉ trả về bài đã Published cho Patient, nên ở tầng entity không cần lưu status.
class BlogPost {
  const BlogPost({
    required this.id,
    required this.title,
    required this.publishedAt,
    required this.authorName,
  });

  final String id;
  final String title;
  final DateTime publishedAt;
  final String authorName;
}

extension BlogPostListItemDtoX on BlogPostListItemDto {
  BlogPost toEntity() => BlogPost(
        id: id,
        title: title,
        publishedAt: publishedAt,
        authorName: authorName,
      );
}

/// Chi tiết bài viết — kèm `content` Markdown để render ở detail screen.
class BlogPostDetail {
  const BlogPostDetail({
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
}

extension BlogPostDetailDtoX on BlogPostDetailDto {
  BlogPostDetail toEntity() => BlogPostDetail(
        id: id,
        title: title,
        content: content,
        publishedAt: publishedAt,
        authorName: authorName,
      );
}