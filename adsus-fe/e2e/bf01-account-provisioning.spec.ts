import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

/**
 * `/api/v1/auth/login` giới hạn 10 request/phút MỖI IP (`Program.cs`, RateLimitPolicies.Auth
 * — cố ý chặn dò mật khẩu). Mọi trình duyệt Playwright chạy trên cùng máy = cùng 1 IP, nên
 * chạy song song nhiều test (mỗi test tự login ít nhất 1 lần) rất dễ vượt giới hạn và bị 429.
 * Ép toàn bộ file này chạy TUẦN TỰ, và chỉ đăng nhập Admin ĐÚNG 1 LẦN dùng chung
 * (adminTokenPromise bên dưới) để giảm tối đa số lần gọi `/auth/login`.
 */
test.describe.configure({ mode: "serial" });

/**
 * Report 5.3 — BF-01 Account Provisioning & Lifecycle, Scenario C + E (E2E Web).
 *
 * Chạy qua browser thật, KHÔNG mock gì (đúng tinh thần System Test) — cần backend local
 * (`https://localhost:7092`, trỏ vào DB test qua user-secrets) và frontend local
 * (`npm run dev`, tự khởi động qua `webServer` trong playwright.config.ts) đang chạy.
 *
 * Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (API không cho tạo Admin). Đổi 2 hằng
 * số ADMIN_PHONE/ADMIN_PASSWORD bên dưới nếu bạn seed khác giá trị mặc định.
 *
 * Chỉ STC010 tạo tài khoản QUA GIAO DIỆN thật (đó chính là điều cần kiểm). Các TC còn lại
 * (STC011/014/015) chỉ cần MỘT tài khoản Doctor có sẵn để đăng nhập — tạo thẳng qua API
 * (`createDoctorAccountViaApi`, tin cậy hơn nhiều so với đọc mật khẩu tạm từ DOM) rồi mới
 * dùng browser để kiểm đúng hành vi cần test (đăng nhập, RBAC, hết hạn session).
 *
 * Backend chạy HTTPS với chứng chỉ dev tự ký — `ignoreHTTPSErrors` bật ở API context để
 * Playwright không chặn vì lỗi TLS không tin cậy trên localhost.
 *
 * Số điện thoại tài khoản Doctor tạo mới trong mỗi test được sinh NGẪU NHIÊN để chạy lại
 * nhiều lần không bị lỗi trùng — cùng quyết định đã áp dụng cho bộ test HTTP Flow.
 */

const ADMIN_PHONE = "0900000001";
const ADMIN_PASSWORD = "Test123456@";
const API_BASE_URL = "https://localhost:7092";

function uniquePhone(): string {
  return "09" + Math.floor(10_000_000 + Math.random() * 89_999_999);
}

async function signIn(page: Page, phone: string, password: string) {
  await page.goto("/login");
  await page.locator("#phoneNumber").fill(phone);
  await page.locator("#password").fill(password);
  await page.getByRole("button", { name: "Đăng nhập" }).click();
}

// Đăng nhập Admin qua API ĐÚNG 1 LẦN cho cả file, tái dùng token cho mọi test — tránh cộng
// dồn số lần gọi `/auth/login` (giới hạn 10/phút/IP, xem ghi chú đầu file).
let cachedAdminToken: string | null = null;

async function getAdminToken(request: APIRequestContext): Promise<string> {
  if (cachedAdminToken) return cachedAdminToken;

  const loginResponse = await request.post(`${API_BASE_URL}/api/v1/auth/login`, {
    data: { phoneNumber: ADMIN_PHONE, password: ADMIN_PASSWORD },
  });
  expect(loginResponse.ok(), await loginResponse.text()).toBeTruthy();
  const loginBody = await loginResponse.json();
  cachedAdminToken = loginBody.data.accessToken as string;
  return cachedAdminToken;
}

/** Tạo 1 tài khoản Doctor thẳng qua API (chuẩn bị dữ liệu, không phải điều đang test). */
async function createDoctorAccountViaApi(
  request: APIRequestContext,
): Promise<{ phone: string; fullName: string; tempPassword: string }> {
  const accessToken = await getAdminToken(request);

  const phone = uniquePhone();
  const fullName = `E2E Doctor ${phone.slice(-4)}`;

  const createResponse = await request.post(`${API_BASE_URL}/api/v1/admin/users`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: { phoneNumber: phone, fullName, role: "DOCTOR" },
  });
  expect(createResponse.ok(), await createResponse.text()).toBeTruthy();
  const createBody = await createResponse.json();

  return {
    phone,
    fullName,
    tempPassword: createBody.data.temporaryPassword as string,
  };
}

const READY_ACCOUNT_PASSWORD = "E2eReady123!";

/**
 * Tạo 1 tài khoản Doctor VÀ hoàn tất luôn bước đổi mật khẩu bắt buộc qua API — dùng cho các
 * TC (STC014/STC015) cần 1 Doctor ở trạng thái "sẵn sàng dùng" (vào thẳng /patients), không
 * phải test lại chính luồng đổi mật khẩu lần đầu (đó là việc của STC011).
 */
async function createReadyDoctorAccountViaApi(
  request: APIRequestContext,
): Promise<{ phone: string; password: string }> {
  const { phone, tempPassword } = await createDoctorAccountViaApi(request);

  const loginResponse = await request.post(`${API_BASE_URL}/api/v1/auth/login`, {
    data: { phoneNumber: phone, password: tempPassword },
  });
  expect(loginResponse.ok(), await loginResponse.text()).toBeTruthy();
  const loginBody = await loginResponse.json();
  const accessToken: string = loginBody.data.accessToken;

  const changeResponse = await request.post(`${API_BASE_URL}/api/v1/auth/change-password`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: {
      currentPassword: tempPassword,
      newPassword: READY_ACCOUNT_PASSWORD,
      confirmNewPassword: READY_ACCOUNT_PASSWORD,
    },
  });
  expect(changeResponse.ok(), await changeResponse.text()).toBeTruthy();

  return { phone, password: READY_ACCOUNT_PASSWORD };
}

test.describe("BF-01 Scenario C — Admin provisions account, user signs in", () => {
  test("STC010: Admin creates a new Doctor account through the Web UI", async ({ page }) => {
    await signIn(page, ADMIN_PHONE, ADMIN_PASSWORD);
    await page.waitForURL("/dashboard");

    await page.goto("/admin/users/new");

    const phone = uniquePhone();
    const fullName = `E2E Doctor ${phone.slice(-4)}`;

    await page.getByLabel("Số điện thoại").fill(phone);
    await page.getByLabel("Họ và tên").fill(fullName);
    await page.locator("select").selectOption("DOCTOR");
    await page.getByRole("button", { name: "Tạo tài khoản" }).click();

    // Điều đang test: tạo thành công qua UI. Không cần đọc mật khẩu tạm ở đây.
    await expect(
      page.getByRole("heading", { name: new RegExp(`Đã tạo tài khoản cho ${fullName}`) }),
    ).toBeVisible();

    await page.getByRole("button", { name: /Đã đọc cho họ.*Xong/ }).click();
    await expect(page).toHaveURL("/admin/users");
    await expect(page.getByText(fullName)).toBeVisible();
  });

  test("STC011: New Doctor signs in on the Web for the first time", async ({ page, request }) => {
    const { phone, tempPassword } = await createDoctorAccountViaApi(request);

    await signIn(page, phone, tempPassword);

    // Đang dùng mật khẩu tạm (must_change_password = true) — AuthGuard bắt buộc đổi mật
    // khẩu (UC-25) TRƯỚC KHI vào bất kỳ đâu khác, kể cả khu vực riêng /patients của Doctor.
    await expect(page).toHaveURL("/change-password");
    await expect(page.getByRole("heading", { name: "Đổi mật khẩu" })).toBeVisible();
  });
});

test.describe("BF-01 Scenario E — RBAC-on-UI spot-check & session/token expiry", () => {
  // 1 tài khoản Doctor "sẵn sàng dùng" DÙNG CHUNG cho cả STC014 và STC015 — setup 1 lần
  // (login + đổi mật khẩu) thay vì 2 lần riêng, giảm bớt số request tới `/auth/login`.
  let readyAccount: { phone: string; password: string };

  test.beforeAll(async ({ request }) => {
    readyAccount = await createReadyDoctorAccountViaApi(request);
  });

  test("STC014: A non-Admin (Doctor) cannot access an Admin-only Web page", async ({ page }) => {
    await signIn(page, readyAccount.phone, readyAccount.password);
    await page.waitForURL("/patients");

    await page.goto("/admin/users");

    // Không có trang 403 — AuthGuard âm thầm đưa Doctor về đúng khu vực của họ.
    await expect(page).toHaveURL("/patients");
  });

  test("STC015: An expired/invalid session forces re-authentication", async ({ page }) => {
    await signIn(page, readyAccount.phone, readyAccount.password);
    await page.waitForURL("/patients");

    // Ép MỌI request API tiếp theo trả 401 — mô phỏng token hết hạn/bị thu hồi ở phía
    // server. Đặt route intercept SAU khi đã đăng nhập xong, để không chặn nhầm chính lời
    // gọi đăng nhập ở trên.
    await page.route("**/api/v1/**", (route) =>
      route.fulfill({
        status: 401,
        contentType: "application/json",
        body: JSON.stringify({ code: 401, message: "Invalid or expired token." }),
      }),
    );

    await page.reload();

    await expect(page).toHaveURL(/\/login\?expired=1/);
    await expect(page.getByText(/Phiên đăng nhập đã kết thúc/)).toBeVisible();
  });
});
