# Plan: Load Notifications trong vòng 60 ngày với Infinite Scroll

## Mục tiêu
- Hiển thị notifications trong vòng 60 ngày
- Performance: Chỉ load 20 notifications ban đầu
- Infinite scroll: Load thêm 10 notifications mỗi lần khi lướt xuống
- Tiếp tục load cho đến khi hết notifications trong 60 ngày

---

## 1. Backend Changes

### 1.1 Thêm `fromDate` parameter vào API

**File:** `ADSUS_BE/Controllers/NotificationsController.cs`

```csharp
[HttpGet]
public async Task<IActionResult> GetNotifications(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] DateTime? fromDate = null,  // THÊM MỚI
    CancellationToken cancellationToken = default)
{
    var notifications = await _notificationLogRepo.GetByUserIdAsync(
        userId, page, pageSize, false, fromDate, cancellationToken);

    var unreadCount = await _notificationLogRepo.CountUnreadAsync(userId, cancellationToken);

    // THÊM: Trả về hasMore để mobile biết còn data không
    var response = new NotificationListResponse
    {
        Notifications = notifications.Select(n => new NotificationDto { ... }).ToList(),
        UnreadCount = unreadCount,
        HasMore = notifications.Count == pageSize,  // TRUE nếu còn page tiếp theo
    };

    return Ok(ApiResponse<NotificationListResponse>.Ok(response));
}
```

### 1.2 Cập nhật Response DTO

**File:** `ADSUS_BE/Controllers/NotificationsController.cs`

```csharp
public class NotificationListResponse
{
    public List<NotificationDto> Notifications { get; init; } = new();
    public int UnreadCount { get; init; }
    public bool HasMore { get; init; }  // THÊM MỚI
}
```

### 1.3 Cập nhật Interface

**File:** `ADSUS_BE.DAL/Repositories/Interfaces/INotificationLogRepository.cs`

```csharp
Task<IReadOnlyList<NotificationLog>> GetByUserIdAsync(
    Guid userId,
    int page,
    int pageSize,
    bool includeDeleted = false,
    DateTime? fromDate = null,  // THÊM MỚI
    CancellationToken ct = default);
```

### 1.4 Cập nhật Repository Implementation

**File:** `ADSUS_BE.DAL/Repositories/Implementations/NotificationLogRepository.cs`

```csharp
public async Task<IReadOnlyList<NotificationLog>> GetByUserIdAsync(
    Guid userId,
    int page,
    int pageSize,
    bool includeDeleted = false,
    DateTime? fromDate = null,  // THÊM MỚI
    CancellationToken ct = default)
{
    var query = _db.NotificationLogs.AsNoTracking();

    if (!includeDeleted)
    {
        query = query.Where(n => n.IsDeleted != true);
    }

    // THÊM: Filter theo fromDate
    if (fromDate.HasValue)
    {
        query = query.Where(n => n.SentAt >= fromDate.Value);
    }

    return await query
        .Where(n => n.UserId == userId)
        .OrderByDescending(n => n.SentAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);
}
```

---

## 2. Mobile Changes

### 2.1 Cập nhật State

**File:** `ADSUS_Mobile/lib/features/notification/providers/notification_state_provider.dart`

```dart
class NotificationState {
  const NotificationState({
    this.notifications = const [],
    this.unreadCount = 0,
    this.isLoading = false,
    this.hasMore = true,      // THÊM MỚI
    this.currentPage = 1,     // THÊM MỚI
    this.fromDate,            // THÊM MỚI
    this.error,
  });

  final List<NotificationDto> notifications;
  final int unreadCount;
  final bool isLoading;
  final bool hasMore;         // THÊM MỚI
  final int currentPage;       // THÊM MỚI
  final DateTime? fromDate;   // THÊM MỚI
  final String? error;
}
```

### 2.2 Cập nhật `fetchNotifications` với pagination

```dart
Future<void> fetchNotifications({
  int page = 1,
  int pageSize = 20,
  DateTime? fromDate,
  bool isLoadMore = false,  // THÊM MỚI: phân biệt load ban đầu vs load thêm
}) async {
  if (state.isLoading) return;
  if (!isLoadMore && !state.hasMore) return;

  if (_accessToken.isEmpty) {
    state = state.copyWith(isLoading: false, error: 'Not authenticated');
    return;
  }

  // Tính fromDate = 60 ngày trước nếu không truyền
  final effectiveFromDate = fromDate ?? DateTime.now().subtract(const Duration(days: 60));

  state = state.copyWith(isLoading: true, error: null);

  try {
    _dio ??= Dio(BaseOptions(
      baseUrl: ApiConstants.baseUrl,
      connectTimeout: ApiConstants.timeout,
      receiveTimeout: ApiConstants.timeout,
    ));

    final response = await _dio!.get(
      ApiConstants.notifications,
      queryParameters: {
        'page': page,
        'pageSize': pageSize,
        'fromDate': effectiveFromDate.toIso8601String(),
      },
      options: Options(
        headers: {'Authorization': 'Bearer $_accessToken'},
      ),
    );

    if (response.statusCode == 200) {
      final data = response.data['data'];
      final newNotifications = (data['notifications'] as List)
          .map((e) => NotificationDto.fromJson(e))
          .toList();
      final hasMore = data['hasMore'] as bool? ?? false;

      state = state.copyWith(
        // Nếu là loadMore, append vào list hiện tại
        notifications: isLoadMore
            ? [...state.notifications, ...newNotifications]
            : newNotifications,
        unreadCount: data['unreadCount'] as int,
        hasMore: hasMore,
        currentPage: page,
        fromDate: effectiveFromDate,
        isLoading: false,
      );
    }
  } catch (e) {
    state = state.copyWith(isLoading: false, error: e.toString());
  }
}

// THÊM: Hàm load thêm notifications
Future<void> loadMoreNotifications() async {
  if (!state.hasMore || state.isLoading) return;
  await fetchNotifications(
    page: state.currentPage + 1,
    pageSize: 10,  // Load 10 mỗi lần
    fromDate: state.fromDate,
    isLoadMore: true,
  );
}
```

### 2.3 Cập nhật UI Screen với Infinite Scroll

**File:** `ADSUS_Mobile/lib/features/notification/screens/notification_history_screen.dart`

```dart
// Thay ListView thường bằng NotificationScrollView (custom widget)
class NotificationHistoryScreen extends ConsumerStatefulWidget {
  @override
  ConsumerState<NotificationHistoryScreen> createState() =>
      _NotificationHistoryScreenState();
}

class _NotificationHistoryScreenState
    extends ConsumerState<NotificationHistoryScreen> {
  final ScrollController _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    // Load ban đầu với 20 notifications trong 60 ngày
    final fromDate = DateTime.now().subtract(const Duration(days: 60));
    ref.read(notificationsProvider.notifier).fetchNotifications(
          fromDate: fromDate,
          pageSize: 20,
        );

    // Lắng nghe scroll để load thêm
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController.removeListener(_onScroll);
    _scrollController.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.position.pixels >=
        _scrollController.position.maxScrollExtent - 200) {
      // Khi còn cách bottom 200px, load thêm
      ref.read(notificationsProvider.notifier).loadMoreNotifications();
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(notificationsProvider);

    return Scaffold(
      appBar: AppBar(title: Text('Thông báo')),
      body: RefreshIndicator(
        onRefresh: () async {
          await ref.read(notificationsProvider.notifier).fetchNotifications(
                fromDate: DateTime.now().subtract(const Duration(days: 60)),
                pageSize: 20,
              );
        },
        child: ListView.builder(
          controller: _scrollController,
          itemCount: state.notifications.length + (state.hasMore ? 1 : 0),
          itemBuilder: (context, index) {
            // Nếu là item cuối và còn hasMore, hiện loading indicator
            if (index == state.notifications.length && state.hasMore) {
              return const Center(
                child: Padding(
                  padding: EdgeInsets.all(16),
                  child: CircularProgressIndicator(),
                ),
              );
            }
            return NotificationTile(notification: state.notifications[index]);
          },
        ),
      ),
    );
  }
}
```

### 2.4 Cập nhật CopyWith Extension

```dart
extension NotificationStateCopyWith on NotificationState {
  NotificationState copyWith({
    List<NotificationDto>? notifications,
    int? unreadCount,
    bool? isLoading,
    bool? hasMore,
    int? currentPage,
    DateTime? fromDate,
    String? error,
  }) {
    return NotificationState(
      notifications: notifications ?? this.notifications,
      unreadCount: unreadCount ?? this.unreadCount,
      isLoading: isLoading ?? this.isLoading,
      hasMore: hasMore ?? this.hasMore,
      currentPage: currentPage ?? this.currentPage,
      fromDate: fromDate ?? this.fromDate,
      error: error,
    );
  }
}
```

---

## 3. Files cần thay đổi

| File | Action |
|------|--------|
| `ADSUS_BE/Controllers/NotificationsController.cs` | Thêm `fromDate` param + `HasMore` |
| `ADSUS_BE.DAL/Repositories/Interfaces/INotificationLogRepository.cs` | Thêm `fromDate` param |
| `ADSUS_BE.DAL/Repositories/Implementations/NotificationLogRepository.cs` | Thêm filter |
| `ADSUS_Mobile/lib/features/notification/providers/notification_state_provider.dart` | Thêm pagination + `loadMoreNotifications()` |
| `ADSUS_Mobile/lib/features/notification/screens/notification_history_screen.dart` | Thêm Infinite Scroll |

---

## 4. Performance Strategy

```
┌─────────────────────────────────────────────────────────────┐
│ Initial Load (60 ngày gần nhất)                            │
│ - Page 1, PageSize = 20                                    │
│ - Load ~0.5s, hiển thị ngay                               │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│ User Scrolls Down (còn cách bottom 200px)                  │
│ - Page 2, PageSize = 10                                    │
│ - Append 10 notifications                                   │
│ - Check HasMore từ backend                                  │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼ (lặp lại cho đến khi HasMore = false)
┌─────────────────────────────────────────────────────────────┐
│ End of List                                                │
│ - HasMore = false                                          │
│ - Không gọi API nữa                                        │
└─────────────────────────────────────────────────────────────┘
```

**Lợi ích:**
- **Initial load nhanh:** Chỉ 20 items đầu tiên
- **Perceived performance:** Hiển thị ngay, không blocking UI
- **Memory efficient:** Load từ từ, không load hết 1 lần
- **Bandwidth saving:** Không download notifications cũ hơn 60 ngày

---

## 5. Lưu ý

- **`pageSize` khác nhau:** Initial load = 20, Load more = 10
- **Backend `HasMore`:** Dựa vào `notifications.Count == pageSize`
- **60 ngày:** Tính từ `DateTime.now()` trừ đi 60 ngày
- **Pull to Refresh:** Reset về page 1, load lại 20 items đầu
- **Error Handling:** Nếu load thất bại, vẫn giữ list cũ + hiện error
