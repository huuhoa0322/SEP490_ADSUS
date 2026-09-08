import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { useDiagnosticStore } from "@/features/medical-record/stores/use-diagnostic-store";

function fakeImage(name: string): File {
  return new File([new Uint8Array([1, 2, 3])], name, { type: "image/jpeg" });
}

describe("useDiagnosticStore — Adversarial & Stress Testing", () => {
  beforeEach(() => {
    useDiagnosticStore.getState().clearSession();
  });

  afterEach(() => {
    useDiagnosticStore.getState().clearSession();
  });

  describe("Task 1.1: setCurrentIndex alias existence & contract", () => {
    it("setCurrentIndex function exists and is exposed on useDiagnosticStore", () => {
      const state = useDiagnosticStore.getState();
      expect(state.setCurrentIndex).toBeDefined();
      expect(typeof state.setCurrentIndex).toBe("function");
    });

    it("setCurrentIndex correctly sets currentIndex when inside valid range", () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-adv", [
        fakeImage("1.jpg"),
        fakeImage("2.jpg"),
        fakeImage("3.jpg"),
        fakeImage("4.jpg"),
      ]);

      useDiagnosticStore.getState().setCurrentIndex(2);
      expect(useDiagnosticStore.getState().currentIndex).toBe(2);

      useDiagnosticStore.getState().setCurrentIndex(0);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().setCurrentIndex(3);
      expect(useDiagnosticStore.getState().currentIndex).toBe(3);
    });
  });

  describe("Task 1.2: Out-of-bounds input stress testing for setCurrentIndex", () => {
    it("clamps negative indices (-1, -100, -999999, -Infinity) to 0", () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-adv", [
        fakeImage("1.jpg"),
        fakeImage("2.jpg"),
        fakeImage("3.jpg"),
      ]);

      useDiagnosticStore.getState().setCurrentIndex(-1);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().setCurrentIndex(-100);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().setCurrentIndex(-999999);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().setCurrentIndex(-Infinity);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);
    });

    it("clamps overly large indices (999, 10000, 1000000, Infinity) to images.length - 1", () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-adv", [
        fakeImage("1.jpg"),
        fakeImage("2.jpg"),
        fakeImage("3.jpg"),
      ]);

      useDiagnosticStore.getState().setCurrentIndex(3);
      expect(useDiagnosticStore.getState().currentIndex).toBe(2);

      useDiagnosticStore.getState().setCurrentIndex(999);
      expect(useDiagnosticStore.getState().currentIndex).toBe(2);

      useDiagnosticStore.getState().setCurrentIndex(10000);
      expect(useDiagnosticStore.getState().currentIndex).toBe(2);

      useDiagnosticStore.getState().setCurrentIndex(1000000);
      expect(useDiagnosticStore.getState().currentIndex).toBe(2);

      useDiagnosticStore.getState().setCurrentIndex(Infinity);
      expect(useDiagnosticStore.getState().currentIndex).toBe(2);
    });

    it("handles boundary indices 0 and images.length - 1 exactly", () => {
      const files = [fakeImage("1.jpg"), fakeImage("2.jpg"), fakeImage("3.jpg"), fakeImage("4.jpg"), fakeImage("5.jpg")];
      useDiagnosticStore.getState().setDiagnosticSession("case-adv", files);

      useDiagnosticStore.getState().setCurrentIndex(0);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().setCurrentIndex(files.length - 1);
      expect(useDiagnosticStore.getState().currentIndex).toBe(files.length - 1);
    });

    it("evaluates behavior on non-integer inputs (floating points)", () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-adv", [
        fakeImage("1.jpg"),
        fakeImage("2.jpg"),
        fakeImage("3.jpg"),
      ]);

      // Note: Math.min/Math.max preserves floats if passed
      useDiagnosticStore.getState().setCurrentIndex(1.5);
      expect(useDiagnosticStore.getState().currentIndex).toBe(1.5);
    });

    it("evaluates behavior on NaN input", () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-adv", [
        fakeImage("1.jpg"),
        fakeImage("2.jpg"),
      ]);

      // In JavaScript Math.min/Math.max with NaN yields NaN
      useDiagnosticStore.getState().setCurrentIndex(NaN);
      expect(Number.isNaN(useDiagnosticStore.getState().currentIndex)).toBe(true);
    });
  });

  describe("Task 1.3: nextImage() and prevImage() over-cycling under extreme conditions", () => {
    it("does not exceed images.length - 1 when nextImage is called repeatedly on last image", () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-adv", [
        fakeImage("1.jpg"),
        fakeImage("2.jpg"),
        fakeImage("3.jpg"),
      ]);

      // Move to last image
      useDiagnosticStore.getState().setCurrentIndex(2);
      expect(useDiagnosticStore.getState().currentIndex).toBe(2);

      // Over-cycle 500 times
      for (let i = 0; i < 500; i++) {
        useDiagnosticStore.getState().nextImage();
      }

      expect(useDiagnosticStore.getState().currentIndex).toBe(2);
    });

    it("does not drop below 0 when prevImage is called repeatedly on first image", () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-adv", [
        fakeImage("1.jpg"),
        fakeImage("2.jpg"),
        fakeImage("3.jpg"),
      ]);

      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      // Over-cycle 500 times
      for (let i = 0; i < 500; i++) {
        useDiagnosticStore.getState().prevImage();
      }

      expect(useDiagnosticStore.getState().currentIndex).toBe(0);
    });

    it("survives rapid ping-pong oscillation between nextImage and prevImage", () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-adv", [
        fakeImage("1.jpg"),
        fakeImage("2.jpg"),
      ]);

      for (let i = 0; i < 1000; i++) {
        useDiagnosticStore.getState().nextImage();
        useDiagnosticStore.getState().prevImage();
      }

      expect(useDiagnosticStore.getState().currentIndex).toBe(0);
    });
  });

  describe("Task 1.4: Empty image session (images = []) and single image session (images = [file])", () => {
    it("empty session: setCurrentIndex with any value remains clamped to 0 without crashing", () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-empty", []);

      expect(useDiagnosticStore.getState().images).toHaveLength(0);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().setCurrentIndex(10);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().setCurrentIndex(-5);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().nextImage();
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().prevImage();
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);
    });

    it("single image session: currentIndex is always locked to 0 regardless of next, prev, or setCurrentIndex", () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-single", [fakeImage("only.jpg")]);

      expect(useDiagnosticStore.getState().images).toHaveLength(1);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().nextImage();
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().prevImage();
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().setCurrentIndex(1);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().setCurrentIndex(999);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      useDiagnosticStore.getState().setCurrentIndex(-10);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);
    });
  });

  describe("Task 1.5: Rapid successive calls & stress loop", () => {
    it("executes 10,000 randomized store navigations without any runtime error and maintains invariant bounds", () => {
      const files = Array.from({ length: 10 }, (_, i) => fakeImage(`img_${i}.jpg`));
      useDiagnosticStore.getState().setDiagnosticSession("case-stress", files);

      for (let i = 0; i < 10000; i++) {
        const op = i % 4;
        if (op === 0) {
          useDiagnosticStore.getState().nextImage();
        } else if (op === 1) {
          useDiagnosticStore.getState().prevImage();
        } else if (op === 2) {
          // Random index including negative and out-of-bound numbers
          const target = Math.floor(Math.random() * 50) - 20;
          useDiagnosticStore.getState().setCurrentIndex(target);
        } else {
          useDiagnosticStore.getState().setCurrentIndex(i % files.length);
        }

        const idx = useDiagnosticStore.getState().currentIndex;
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(idx).toBeLessThanOrEqual(files.length - 1);
      }
    });

    it("handles rapid removeImage calls down to 0 images while maintaining valid currentIndex", () => {
      const files = [fakeImage("a.jpg"), fakeImage("b.jpg"), fakeImage("c.jpg"), fakeImage("d.jpg")];
      useDiagnosticStore.getState().setDiagnosticSession("case-stress", files);

      // Move to index 3
      useDiagnosticStore.getState().setCurrentIndex(3);
      expect(useDiagnosticStore.getState().currentIndex).toBe(3);

      // Remove last image
      useDiagnosticStore.getState().removeImage(3);
      expect(useDiagnosticStore.getState().currentIndex).toBe(2);
      expect(useDiagnosticStore.getState().images).toHaveLength(3);

      // Remove middle image
      useDiagnosticStore.getState().removeImage(1);
      expect(useDiagnosticStore.getState().currentIndex).toBe(1);
      expect(useDiagnosticStore.getState().images).toHaveLength(2);

      // Remove first image
      useDiagnosticStore.getState().removeImage(0);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);
      expect(useDiagnosticStore.getState().images).toHaveLength(1);

      // Remove remaining image
      useDiagnosticStore.getState().removeImage(0);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);
      expect(useDiagnosticStore.getState().images).toHaveLength(0);

      // Remove from empty store should not crash
      expect(() => useDiagnosticStore.getState().removeImage(0)).not.toThrow();
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);
    });
  });

  describe("Comparative Analysis: setCurrentIndex vs goToImage", () => {
    it("documents that setCurrentIndex clamps to bounds whereas goToImage does not clamp", () => {
      useDiagnosticStore.getState().setDiagnosticSession("case-compare", [
        fakeImage("1.jpg"),
        fakeImage("2.jpg"),
      ]);

      // setCurrentIndex clamps
      useDiagnosticStore.getState().setCurrentIndex(999);
      expect(useDiagnosticStore.getState().currentIndex).toBe(1);

      useDiagnosticStore.getState().setCurrentIndex(-10);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);

      // goToImage does NOT clamp (historical unclamped method)
      useDiagnosticStore.getState().goToImage(999);
      expect(useDiagnosticStore.getState().currentIndex).toBe(999);

      useDiagnosticStore.getState().goToImage(-10);
      expect(useDiagnosticStore.getState().currentIndex).toBe(-10);

      // Resetting with setCurrentIndex immediately restores valid state
      useDiagnosticStore.getState().setCurrentIndex(0);
      expect(useDiagnosticStore.getState().currentIndex).toBe(0);
    });
  });
});
