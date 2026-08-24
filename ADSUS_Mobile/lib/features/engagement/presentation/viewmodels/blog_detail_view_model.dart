import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/blog_post.dart';
import '../../domain/repositories/blog_repository.dart';

/// Trạng thái màn hình Blog Detail.
class BlogDetailState {
  const BlogDetailState({
    this.post,
    this.isLoading = false,
    this.notFound = false,
    this.errorMessage,
  });

  /// null khi đang loading hoặc lỗi. Detail thật khi fetch thành công.
  final BlogPostDetail? post;
  final bool isLoading;

  /// Backend trả 404 nếu post là Draft hoặc không tồn tại (GB-05).
  /// true → render "Không tìm thấy bài viết" thay vì lỗi chung.
  final bool notFound;
  final String? errorMessage;

  BlogDetailState copyWith({
    BlogPostDetail? post,
    bool? isLoading,
    bool? notFound,
    String? errorMessage,
    bool clearError = false,
  }) =>
      BlogDetailState(
        post: post ?? this.post,
        isLoading: isLoading ?? this.isLoading,
        notFound: notFound ?? this.notFound,
        errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      );
}

/// ViewModel cho màn Blog Detail (SCR-26).
class BlogDetailViewModel extends StateNotifier<BlogDetailState> {
  BlogDetailViewModel(this._ref, this._postId) : super(const BlogDetailState());

  final Ref _ref;
  final String _postId;

  BlogRepository get _repo => _ref.read(blogRepositoryProvider);

  Future<void> load() async {
    state = state.copyWith(
      isLoading: true,
      notFound: false,
      clearError: true,
    );
    try {
      final post = await _repo.getById(_postId);
      state = state.copyWith(post: post, isLoading: false);
    } on ApiException catch (e) {
      if (e.statusCode == 404) {
        state = state.copyWith(isLoading: false, notFound: true);
      } else {
        state = state.copyWith(isLoading: false, errorMessage: e.message);
      }
    }
  }

  Future<void> retry() => load();
}

/// Family provider — mỗi postId có 1 instance riêng (cache theo id).
final blogDetailViewModelProvider = StateNotifierProvider.family<
    BlogDetailViewModel, BlogDetailState, String>((ref, postId) {
  return BlogDetailViewModel(ref, postId);
});