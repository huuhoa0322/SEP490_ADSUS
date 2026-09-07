// generateBurntImage KHÔNG có test ở đây (cố ý): cần Canvas 2D thật (getContext, toBlob) mà
// jsdom không hỗ trợ sẵn — cùng loại phức tạp với diagnostic-canvas.tsx, đã gác lại theo quyết
// định phạm vi của đợt P_FE8 này.
import { describe, expect, it } from "vitest";

import { checkIntersection } from "@/features/medical-record/utils/canvas-utils";
import type { Point } from "@/features/medical-record/stores/use-diagnostic-store";

const pt = (x: number, y: number): Point => ({ x, y });

describe("checkIntersection", () => {
  it("2 đoạn thẳng cắt nhau ở giữa → true", () => {
    const pairA = [pt(0, 0), pt(10, 10)];
    const pairB = [pt(0, 10), pt(10, 0)];

    expect(checkIntersection(pairA, pairB)).toBe(true);
  });

  it("2 đoạn thẳng song song không cắt nhau → false", () => {
    const pairA = [pt(0, 0), pt(10, 0)];
    const pairB = [pt(0, 5), pt(10, 5)];

    expect(checkIntersection(pairA, pairB)).toBe(false);
  });

  it("2 đoạn thẳng cùng đường nhưng không giao (nằm ngoài đoạn) → false", () => {
    const pairA = [pt(0, 0), pt(5, 5)];
    const pairB = [pt(20, 0), pt(25, 5)];

    expect(checkIntersection(pairA, pairB)).toBe(false);
  });

  it("giao đúng tại điểm đầu mút (t=0 hoặc s=0) vẫn tính là cắt nhau", () => {
    const pairA = [pt(0, 0), pt(10, 10)];
    const pairB = [pt(0, 0), pt(0, 10)];

    expect(checkIntersection(pairA, pairB)).toBe(true);
  });
});
