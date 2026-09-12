import Link from "next/link";

const POSTS = [
  {
    id: "1",
    title: "5 lý do mẹ bầu nên siêu âm định kỳ",
    excerpt:
      "Siêu âm thai định kỳ giúp theo dõi sự phát triển của thai nhi và phát hiện sớm các vấn đề sức khỏe.",
    date: "15/08/2026",
    tag: "Sản khoa",
  },
  {
    id: "2",
    title: "Siêu âm 4D: Nhìn thấy bé yêu rõ ràng hơn",
    excerpt:
      "Công nghệ 4D cho phép quan sát chuyển động thực của thai nhi trong thời gian thực, giúp phát hiện dị tật chính xác hơn.",
    date: "10/08/2026",
    tag: "Công nghệ",
  },
  {
    id: "3",
    title: "Khi nào cần siêu âm tim thai?",
    excerpt:
      "Siêu âm tim thai được khuyến nghị cho các thai phụ có tiền sử bệnh lý tim mạch hoặc gia đình có người mắc bệnh tim bẩm sinh.",
    date: "05/08/2026",
    tag: "Tim mạch",
  },
];

export function BlogPreviewSection() {
  return (
    <section
      className="px-4 py-16 sm:px-6 lg:px-8"
      style={{ backgroundColor: "var(--lp-surface)" }}
    >
      <div className="mx-auto max-w-6xl">
        {/* Header */}
        <div className="mb-10 flex items-end justify-between gap-4">
          <div>
            <span
              className="mb-2 inline-block text-xs font-semibold uppercase tracking-widest"
              style={{ color: "var(--lp-teal)" }}
            >
              Kiến thức sức khỏe
            </span>
            <h2
              className="text-2xl sm:text-3xl"
              style={{
                fontFamily: "var(--lp-font-serif)",
                fontWeight: 600,
                color: "var(--lp-navy)",
              }}
            >
              Bài viết mới nhất
            </h2>
          </div>
          <Link
            href="/blog"
            className="shrink-0 text-sm font-medium transition-opacity hover:opacity-70"
            style={{ color: "var(--lp-teal)" }}
          >
            Xem tất cả →
          </Link>
        </div>

        {/* Posts grid */}
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {POSTS.map((post) => (
            <Link
              key={post.id}
              href={`/blog/${post.id}`}
              className="group flex flex-col rounded-xl border p-5 transition-shadow hover:shadow-md"
              style={{
                backgroundColor: "var(--lp-surface)",
                borderColor: "var(--lp-border)",
              }}
            >
              {/* Tag */}
              <span
                className="mb-3 inline-block self-start rounded-full px-2.5 py-0.5 text-xs font-semibold"
                style={{
                  backgroundColor: "var(--lp-teal-tint)",
                  color: "var(--lp-teal)",
                }}
              >
                {post.tag}
              </span>

              {/* Title */}
              <h3
                className="mb-2 text-base font-semibold leading-snug transition-colors group-hover:opacity-80"
                style={{ color: "var(--lp-navy)", fontFamily: "var(--lp-font-serif)" }}
              >
                {post.title}
              </h3>

              {/* Excerpt */}
              <p className="mb-4 flex-1 text-sm leading-relaxed" style={{ color: "var(--lp-muted)" }}>
                {post.excerpt}
              </p>

              {/* Date */}
              <time
                className="text-xs"
                style={{ color: "var(--lp-muted)", fontFamily: "var(--lp-font-mono)" }}
              >
                {post.date}
              </time>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}
