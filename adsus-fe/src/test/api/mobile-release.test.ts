// @vitest-environment node
import { http, HttpResponse } from "msw";
import { afterEach, describe, expect, it, vi } from "vitest";

import { server } from "@/test/mocks/server";

import { GET } from "@/app/api/mobile-release/route";

const GITHUB_API =
  "https://api.github.com/repos/huuhoa0322/SEP490_ADSUS/releases/latest";

afterEach(() => {
  vi.unstubAllEnvs();
});

describe("GET /api/mobile-release", () => {
  it("release có file .apk trong assets — trả đúng URL file đó", async () => {
    server.use(
      http.get(GITHUB_API, () =>
        HttpResponse.json({
          assets: [
            { name: "source.zip", browser_download_url: "https://x/source.zip" },
            {
              name: "adsus-mobile-1.0.0.apk",
              browser_download_url: "https://x/adsus-mobile-1.0.0.apk",
            },
          ],
        }),
      ),
    );

    const body = (await (await GET()).json()) as { downloadUrl: string | null };

    expect(body.downloadUrl).toBe("https://x/adsus-mobile-1.0.0.apk");
  });

  it("GitHub trả 404 (chưa có release, hoặc token sai/hết hạn) — downloadUrl null, không throw", async () => {
    server.use(http.get(GITHUB_API, () => new HttpResponse(null, { status: 404 })));

    const body = (await (await GET()).json()) as { downloadUrl: string | null };

    expect(body.downloadUrl).toBeNull();
  });

  it("release có assets nhưng không file nào .apk — downloadUrl null", async () => {
    server.use(
      http.get(GITHUB_API, () =>
        HttpResponse.json({
          assets: [{ name: "source.zip", browser_download_url: "https://x/source.zip" }],
        }),
      ),
    );

    const body = (await (await GET()).json()) as { downloadUrl: string | null };

    expect(body.downloadUrl).toBeNull();
  });

  it("có GITHUB_RELEASE_TOKEN — gửi kèm header Authorization: Bearer <token>", async () => {
    vi.stubEnv("GITHUB_RELEASE_TOKEN", "fake-token-abc");

    let authHeader: string | null = null;
    server.use(
      http.get(GITHUB_API, ({ request }) => {
        authHeader = request.headers.get("authorization");
        return HttpResponse.json({ assets: [] });
      }),
    );

    await GET();

    expect(authHeader).toBe("Bearer fake-token-abc");
  });

  it("không có GITHUB_RELEASE_TOKEN — vẫn gọi được, không gửi header Authorization", async () => {
    vi.stubEnv("GITHUB_RELEASE_TOKEN", "");

    let authHeader: string | null = "chưa-gọi";
    server.use(
      http.get(GITHUB_API, ({ request }) => {
        authHeader = request.headers.get("authorization");
        return HttpResponse.json({ assets: [] });
      }),
    );

    await GET();

    expect(authHeader).toBeNull();
  });
});
