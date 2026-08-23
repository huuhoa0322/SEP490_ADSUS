import { NextResponse } from "next/server";

const GITHUB_REPO = "huuhoa0322/SEP490_ADSUS";

// Chạy trên server, KHÔNG phải trình duyệt — đây là lý do route này tồn tại. GITHUB_RELEASE_TOKEN
// là biến môi trường thường (không phải NEXT_PUBLIC_), nên không bao giờ lộ vào bundle JS gửi
// cho client. Frontend chỉ gọi route nội bộ này, không bao giờ gọi thẳng GitHub API — cách đó
// vẫn hoạt động y hệt dù repo Public hay chuyển Private sau này, chỉ cần token còn hiệu lực.
export async function GET() {
  const token = process.env.GITHUB_RELEASE_TOKEN;

  const response = await fetch(
    `https://api.github.com/repos/${GITHUB_REPO}/releases/latest`,
    {
      headers: {
        Accept: "application/vnd.github+json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      // Không cache ở tầng fetch — tần suất gọi lại đã do staleTime của React Query
      // phía client quyết định, không cần thêm 1 tầng cache nữa ở đây.
      cache: "no-store",
    },
  );

  // 404 (chưa có release nào, hoặc token sai quyền/hết hạn với repo Private) coi như
  // "chưa có bản tải" — vẫn trả 200 cho client, để UI chỉ cần kiểm tra downloadUrl có hay
  // không, không cần xử lý riêng từng mã lỗi.
  if (!response.ok) {
    return NextResponse.json({ downloadUrl: null });
  }

  const release = (await response.json()) as {
    assets: Array<{ name: string; browser_download_url: string }>;
  };
  const apkAsset = release.assets.find((asset) => asset.name.endsWith(".apk"));

  return NextResponse.json({ downloadUrl: apkAsset?.browser_download_url ?? null });
}
