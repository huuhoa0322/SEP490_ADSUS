import 'package:flutter/material.dart';
import 'package:flutter_markdown/flutter_markdown.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../viewmodels/blog_detail_view_model.dart';

/// Màn hình chi tiết bài viết Blog (SCR-26, UC-23).
///
/// Render `content` (Markdown) bằng `flutter_markdown` — an toàn hơn render HTML thô
/// (GB-03 + medical-safety.md: package này không thực thi script/iframe).
class BlogDetailScreen extends ConsumerStatefulWidget {
  const BlogDetailScreen({super.key, required this.postId});

  final String postId;

  @override
  ConsumerState<BlogDetailScreen> createState() => _BlogDetailScreenState();
}

class _BlogDetailScreenState extends ConsumerState<BlogDetailScreen> {
  @override
  void initState() {
    super.initState();
    Future.microtask(() => ref
        .read(blogDetailViewModelProvider(widget.postId).notifier)
        .load());
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(blogDetailViewModelProvider(widget.postId));
    final title = state.post?.title ?? 'Blog Sức khỏe';

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text(
          title,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
        ),
      ),
      body: RefreshIndicator(
        onRefresh: () => ref
            .read(blogDetailViewModelProvider(widget.postId).notifier)
            .retry(),
        child: _buildBody(state),
      ),
    );
  }

  Widget _buildBody(BlogDetailState state) {
    if (state.isLoading && state.post == null) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.notFound) {
      return ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        children: const [
          SizedBox(height: 120),
          Icon(Icons.article_outlined,
              size: 64, color: AppColors.muted),
          SizedBox(height: 12),
          Center(
            child: Text(
              'Bài viết không tồn tại hoặc chưa được xuất bản.',
              style: TextStyle(fontSize: 14, color: AppColors.muted),
              textAlign: TextAlign.center,
            ),
          ),
        ],
      );
    }

    if (state.errorMessage != null && state.post == null) {
      return ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        children: [
          const SizedBox(height: 120),
          const Icon(Icons.error_outline,
              size: 64, color: AppColors.danger),
          const SizedBox(height: 12),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 32),
            child: Text(
              state.errorMessage!,
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 14, color: AppColors.muted),
            ),
          ),
          const SizedBox(height: 16),
          Center(
            child: ElevatedButton(
              onPressed: () => ref
                  .read(blogDetailViewModelProvider(widget.postId).notifier)
                  .retry(),
              child: const Text('Thử lại'),
            ),
          ),
        ],
      );
    }

    final post = state.post;
    if (post == null) {
      // Trạng thái hiếm — guard an toàn.
      return const SizedBox.shrink();
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(
          post.title,
          style: const TextStyle(
            fontSize: 22,
            fontWeight: FontWeight.bold,
            color: AppColors.navy,
            height: 1.3,
          ),
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            const Icon(Icons.person_outline,
                size: 16, color: AppColors.muted),
            const SizedBox(width: 4),
            Expanded(
              child: Text(
                post.authorName.isEmpty ? 'Ẩn danh' : post.authorName,
                style: const TextStyle(
                    fontSize: 13, color: AppColors.muted),
              ),
            ),
            const SizedBox(width: 8),
            const Icon(Icons.calendar_today_outlined,
                size: 13, color: AppColors.muted),
            const SizedBox(width: 4),
            Text(
              _formatDate(post.publishedAt),
              style: const TextStyle(
                  fontSize: 13, color: AppColors.muted),
            ),
          ],
        ),
        const SizedBox(height: 16),
        const Divider(height: 1, color: AppColors.border),
        const SizedBox(height: 16),
        // Render Markdown — flutter_markdown KHÔNG execute script/iframe (an toàn).
        MarkdownBody(
          data: post.content,
          selectable: true,
          styleSheet: MarkdownStyleSheet(
            p: const TextStyle(
                fontSize: 15, color: AppColors.navy, height: 1.6),
            h1: const TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.bold,
                color: AppColors.navy),
            h2: const TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.bold,
                color: AppColors.navy),
            h3: const TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.bold,
                color: AppColors.navy),
            strong: const TextStyle(
                fontWeight: FontWeight.bold, color: AppColors.navy),
            a: const TextStyle(
                color: AppColors.teal,
                decoration: TextDecoration.underline),
            code: const TextStyle(
                fontFamily: 'monospace',
                fontSize: 14,
                color: AppColors.navy,
                backgroundColor: Color(0xFFF1EDFC)),
            blockquoteDecoration: BoxDecoration(
              border: Border(
                left: BorderSide(color: AppColors.aiViolet, width: 4),
              ),
            ),
            blockquotePadding: const EdgeInsets.only(left: 12),
          ),
        ),
      ],
    );
  }
}

String _formatDate(DateTime utc) {
  final local = utc.toLocal();
  final dd = local.day.toString().padLeft(2, '0');
  final mm = local.month.toString().padLeft(2, '0');
  return '$dd/$mm/${local.year}';
}