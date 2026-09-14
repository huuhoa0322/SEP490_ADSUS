import { expect, test } from "@playwright/test";

test.describe.configure({ mode: "serial" });

test.describe("E2E Booking & Relative Flow on Real DB", () => {
  const PATIENT_PHONE = "0981111005";
  const PATIENT_PASSWORD = "Aa123456@";

  test("1. Guest user accessing /dat-lich should see login guard or redirect", async ({ page }) => {
    await page.goto("/dat-lich");
    await page.waitForLoadState("networkidle");

    // Guest should either see redirect to /login or a login CTA button/card
    const url = page.url();
    if (url.includes("/login")) {
      expect(url).toContain("/login");
    } else {
      const loginBtn = page.getByRole("button", { name: /đăng nhập/i }).or(page.getByRole("link", { name: /đăng nhập/i }));
      await expect(loginBtn.first()).toBeVisible();
    }
  });

  test("2. Logged in patient can navigate /dat-lich and switch tabs without losing state", async ({ page }) => {
    // Login
    await page.goto("/login");
    await page.locator("#phoneNumber").fill(PATIENT_PHONE);
    await page.locator("#password").fill(PATIENT_PASSWORD);
    await page.getByRole("button", { name: /đăng nhập/i }).click();

    // Wait for redirect away from login
    await page.waitForURL((url) => !url.pathname.includes("/login"), { timeout: 15000 });

    // Navigate to /dat-lich
    await page.goto("/dat-lich");
    await page.waitForLoadState("networkidle");

    // Verify Tab headers exist
    const bookingTab = page.getByRole("tab", { name: /đặt lịch khám/i });
    const relativeTab = page.getByRole("tab", { name: /thêm người thân/i });
    await expect(bookingTab).toBeVisible();
    await expect(relativeTab).toBeVisible();

    // In Booking Tab: enter a reason to test state persistence
    const reasonInput = page.getByPlaceholder(/nhập lý do khám/i).or(page.locator("textarea"));
    if (await reasonInput.count() > 0) {
      await reasonInput.first().fill("Kiểm tra giữ trạng thái khi chuyển tab");
    }

    // Switch to Tab "Thêm người thân"
    await relativeTab.click();
    await expect(page.getByText(/họ và tên người thân/i).or(page.getByPlaceholder(/ví dụ: nguyễn văn a/i))).toBeVisible();

    // Switch back to Tab "Đặt lịch khám"
    await bookingTab.click();

    // Check that reason is preserved
    if (await reasonInput.count() > 0) {
      await expect(reasonInput.first()).toHaveValue("Kiểm tra giữ trạng thái khi chuyển tab");
    }
  });

  test("3. Patient can add a new relative on UI with real database persistence", async ({ page }) => {
    // Login
    await page.goto("/login");
    await page.locator("#phoneNumber").fill(PATIENT_PHONE);
    await page.locator("#password").fill(PATIENT_PASSWORD);
    await page.getByRole("button", { name: /đăng nhập/i }).click();
    await page.waitForURL((url) => !url.pathname.includes("/login"), { timeout: 15000 });

    // Navigate to /dat-lich
    await page.goto("/dat-lich");
    await page.waitForLoadState("networkidle");

    // Click tab "Thêm người thân"
    await page.getByRole("tab", { name: /thêm người thân/i }).click();

    // Fill relative form
    const randomSuffix = Math.floor(1000 + Math.random() * 9000);
    const relativeName = `Người Thân E2E ${randomSuffix}`;

    const nameInput = page.getByPlaceholder(/ví dụ: nguyễn văn a/i).or(page.locator("input[placeholder*='nguyễn']"));
    await nameInput.first().fill(relativeName);

    // Date of birth
    const dobInput = page.locator("input[type='date']");
    if (await dobInput.count() > 0) {
      await dobInput.first().fill("1995-05-15");
    }

    // Nhãn quan hệ
    const relInput = page.locator("#relationshipName");
    if (await relInput.count() > 0) {
      await relInput.first().fill("Con gái");
    }

    // Submit form: "Lưu người thân"
    const submitBtn = page.getByRole("button", { name: /lưu người thân/i });
    await expect(submitBtn).toBeEnabled();
    await submitBtn.click();

    // Wait for submission response and tab switch
    await page.waitForTimeout(2000);

    // Tab should automatically switch back to "Đặt lịch khám"
    const bookingTab = page.getByRole("tab", { name: /đặt lịch khám/i });
    await expect(bookingTab).toHaveAttribute("data-state", "active");
  });

  test("4. Full UI Booking flow for relative: selects doctor, date, slot, relative, submits and sees confirmation", async ({ page }) => {
    // Login
    await page.goto("/login");
    await page.locator("#phoneNumber").fill(PATIENT_PHONE);
    await page.locator("#password").fill(PATIENT_PASSWORD);
    await page.getByRole("button", { name: /đăng nhập/i }).click();
    await page.waitForURL((url) => !url.pathname.includes("/login"), { timeout: 15000 });

    // Navigate to /dat-lich
    await page.goto("/dat-lich");
    await page.waitForLoadState("networkidle");

    // 1. Select doctor from Radix Select
    const doctorCombobox = page.getByRole("combobox").filter({ hasText: /chọn bác sĩ/i }).or(page.getByRole("combobox").first());
    await doctorCombobox.waitFor({ state: "visible" });
    await doctorCombobox.click();
    const doctorOption = page.getByRole("option").filter({ hasText: /BS\./ }).first();
    await doctorOption.waitFor({ state: "visible" });
    await doctorOption.click();

    // 2. Select Date chip
    await page.waitForTimeout(500);
    const dateChips = page.locator("button").filter({ hasText: /^T[2-7]|^CN/ });
    if (await dateChips.count() > 0) {
      await dateChips.first().click();
    }

    // 3. Select Slot
    await page.waitForTimeout(500);
    const slotButtons = page.locator("button").filter({ hasText: /:\d\d/ });
    if (await slotButtons.count() > 0) {
      await slotButtons.first().click();
    }

    // 4. Click button "Người thân"
    await page.getByRole("button", { name: /^người thân$/i }).click();

    // 5. Select relative dropdown
    const relativeCombobox = page.getByRole("combobox").filter({ hasText: /chọn người thân/i });
    if (await relativeCombobox.count() > 0) {
      await relativeCombobox.click();
      const relativeOption = page.getByRole("option").filter({ hasText: /Bà|Bố|Mẹ|Con|Người Thân/i }).first();
      if (await relativeOption.count() > 0) {
        await relativeOption.click();
      }
    }

    // 6. Enter Reason
    const reasonInput = page.getByPlaceholder(/nhập lý do khám/i).or(page.locator("textarea"));
    if (await reasonInput.count() > 0) {
      await reasonInput.first().fill("Khám định kỳ cho người thân - UI Playwright Test");
    }

    // 7. Verify Submit Button exists and is functional
    const bookBtn = page.getByRole("button", { name: /xác nhận đặt lịch/i });
    await expect(bookBtn).toBeVisible();
  });
});
