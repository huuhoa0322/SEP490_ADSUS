import "@testing-library/jest-dom/vitest";
import { afterAll, afterEach, beforeAll, beforeEach } from "vitest";
import { server } from "./mocks/server";

/**
 * Node 22+ định nghĩa sẵn `globalThis.localStorage` (thử nghiệm, cần cờ `--localstorage-file`
 * mới hoạt động thật). Trên Node 26 dùng ở đây, thuộc tính đó che mất `window.localStorage`
 * mà jsdom lẽ ra tự cấp — window.localStorage trở thành `undefined` trong MỌI test, dù code
 * nguồn (vd. use-sign-in.ts) gọi thẳng `window.localStorage.setItem` không qua guard, đúng như
 * mọi trình duyệt thật đều có sẵn localStorage. Polyfill một bản in-memory ở đây để môi trường
 * test khớp với môi trường thật, thay vì bắt từng nơi trong source phải tự phòng thủ.
 */
class MemoryStorage implements Storage {
  private store = new Map<string, string>();

  get length(): number {
    return this.store.size;
  }

  clear(): void {
    this.store.clear();
  }

  getItem(key: string): string | null {
    return this.store.has(key) ? this.store.get(key)! : null;
  }

  key(index: number): string | null {
    return Array.from(this.store.keys())[index] ?? null;
  }

  removeItem(key: string): void {
    this.store.delete(key);
  }

  setItem(key: string, value: string): void {
    this.store.set(key, String(value));
  }
}

// Some test files opt into the "node" environment (// @vitest-environment node) where
// `window` doesn't exist at all — this setup file still runs for them, so guard it.
beforeAll(() => {
  if (typeof window !== "undefined" && !window.localStorage) {
    Object.defineProperty(window, "localStorage", {
      value: new MemoryStorage(),
      configurable: true,
    });
  }
});

beforeEach(() => {
  if (typeof window !== "undefined") window.localStorage.clear();
});

beforeAll(() => server.listen({ onUnhandledRequest: "warn" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
