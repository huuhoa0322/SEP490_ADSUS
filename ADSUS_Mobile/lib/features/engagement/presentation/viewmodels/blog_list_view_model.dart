import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/blog_post.dart';
import '../../domain/repositories/blog_repository.dart';

/// Trạng thái màn hình Blog List (UC-23).
class BlogListState {
  const BlogListState({
    this.posts = const [],
    this.isLoading = false,
    this.errorMessage,
  });

  final List<BlogPost> posts;

  /// true khi đang fetch lần đầu.
  final bool isLoading;
  final String? errorMessage;

  BlogListState copyWith({
    List<BlogPost>? posts,
    bool? isLoading,
    String? errorMessage,
    bool clearError = false,
  }) =>
      BlogListState(
        posts: posts ?? this.posts,
        isLoading: isLoading ?? this.isLoading,
        errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      );
}

/// ViewModel cho màn Blog List (SCR-26).
class BlogListViewModel extends StateNotifier<BlogListState> {
  BlogListViewModel(this._ref) : super(const BlogListState());

  final Ref _ref;

  BlogRepository get _repo => _ref.read(blogRepositoryProvider);

  /// Tải trang đầu. Gọi trong initState của screen.
  Future<void> load() async {
    state = state.copyWith(isLoading: true, clearError: true);
    try {
      final posts = await _repo.listPublished(page: 1, pageSize: 20);
      state = state.copyWith(posts: posts, isLoading: false);
    } on ApiException catch (e) {
      state = state.copyWith(isLoading: false, errorMessage: e.message);
    }
  }

  /// Retry — clear lỗi rồi load lại.
  Future<void> retry() => load();

  void clearError() => state = state.copyWith(clearError: true);
}

final blogListViewModelProvider =
    StateNotifierProvider<BlogListViewModel, BlogListState>((ref) {
  return BlogListViewModel(ref);
});