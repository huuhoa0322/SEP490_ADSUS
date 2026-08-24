import '../entities/blog_post.dart';

/// Repository cho Blog Sức khỏe (UC-23).
///
/// Tách interface khỏi implementation để:
/// - ViewModel chỉ depend vào interface (dễ test, đổi mock).
/// - Khớp quy ước 03_mobile.md §9: "ViewModel consume repository qua DI".
abstract class BlogRepository {
  /// GET /api/v1/blog-posts?page=&pageSize=
  ///
  /// Trả về list [BlogPost] (chỉ status Published ở backend).
  /// Throw [ApiException] nếu lỗi mạng / server.
  Future<List<BlogPost>> listPublished({int page = 1, int pageSize = 20});

  /// GET /api/v1/blog-posts/{id}
  ///
  /// Trả về [BlogPostDetail]. Backend trả 404 nếu id không tồn tại HOẶC là Draft
  /// (GB-05) — repository phân biệt 404 với lỗi khác để ViewModel hiển thị đúng.
  Future<BlogPostDetail> getById(String id);
}