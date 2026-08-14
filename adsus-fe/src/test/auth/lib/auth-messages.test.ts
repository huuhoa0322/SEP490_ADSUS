import { AxiosError, AxiosHeaders } from "axios";
import { describe, expect, it } from "vitest";

import { WebNotAvailableForRoleError } from "@/features/auth/types/auth.types";

import {
  getChangePasswordErrorMessage,
  getSignInErrorMessage,
} from "@/features/auth/lib/auth-messages";

/** Dựng một lỗi axios đúng mã HTTP cần thử. */
function loiHttp(status: number, message?: string): AxiosError {
  const error = new AxiosError("Request failed");
  error.response = {
    status,
    statusText: "",
    data: message ? { code: status, message, data: null } : {},
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return error;
}

describe("getSignInErrorMessage — GB-06", () => {
  it("sai số điện thoại, sai mật khẩu, bị khoá, bị vô hiệu hoá: CÙNG MỘT CÂU", () => {
    // Backend trả 401 cho cả bốn trường hợp và không kèm mã phân biệt, nên phía client
    // cũng chỉ có đúng một câu. Test này là chốt chặn: ai đó thêm nhánh "tài khoản đã bị
    // khoá" cho thân thiện hơn là lộ ngay thông tin tài khoản nào có thật.
    const cau = getSignInErrorMessage(loiHttp(401));

    expect(cau).toBe("Số điện thoại hoặc mật khẩu không đúng.");
    expect(cau).not.toMatch(/khoá|vô hiệu|không tồn tại/i);
  });

  it("404 KHÔNG bị gộp vào 'sai mật khẩu'", () => {
    // 404 nghĩa là request đi lạc (sai NEXT_PUBLIC_API_BASE_URL), không phải sai mật khẩu.
    // Gộp vào là cả nhóm ngồi mò lại mật khẩu trong khi lỗi nằm ở file .env.local.
    const cau = getSignInErrorMessage(loiHttp(404));

    expect(cau).toContain("NEXT_PUBLIC_API_BASE_URL");
  });

  it("429 báo chờ thử lại, không bị gộp vào 'sai mật khẩu'", () => {
    const cau = getSignInErrorMessage(
      loiHttp(429, "Too many requests. Please wait before trying again."),
    );

    expect(cau).toBe(
      "Bạn đã gửi quá nhiều yêu cầu. Vui lòng chờ một lúc rồi thử lại.",
    );
    expect(cau).not.toContain("mật khẩu không đúng");
  });

  it("bệnh nhân đăng nhập trên Web được chỉ sang ứng dụng di động", () => {
    // UC-01: SCR-01 (Web) dành cho Admin/Doctor/Nurse, bệnh nhân dùng SCR-02 trên mobile.
    // Mật khẩu đúng nên KHÔNG dùng câu chung của GB-06.
    const cau = getSignInErrorMessage(new WebNotAvailableForRoleError());

    expect(cau).toContain("điện thoại");
    expect(cau).not.toBe("Số điện thoại hoặc mật khẩu không đúng.");
  });
});

describe("getChangePasswordErrorMessage — UC-25", () => {
  it("dịch đúng câu lỗi mật khẩu hiện tại sai", () => {
    const cau = getChangePasswordErrorMessage(loiHttp(400, "Current password is incorrect."));

    expect(cau).toBe("Mật khẩu hiện tại không đúng.");
  });

  it("gộp 2 lỗi validate mới bị backend nối bằng dấu cách vẫn dịch trọn từng câu", () => {
    // Backend join bằng " " (string.Join), nên phải khớp từng câu con thay vì so nguyên chuỗi.
    //
    // LƯU Ý HÀNH VI THẬT (14/08/2026): getChangePasswordErrorMessage gọi getApiErrorMessage
    // trước, và hàm đó đã tự dịch qua translateApiMessage() (api-messages.ts) trước khi
    // CHANGE_PASSWORD_ERRORS cục bộ của file này có cơ hội so khớp. Một khi đã dịch xong thì
    // chuỗi không còn tiếng Anh nữa nên CHANGE_PASSWORD_ERRORS không bao giờ khớp được cho 2
    // câu này — kết quả cuối cùng dùng ĐÚNG wording của api-messages.ts ("một chữ hoa"/"một
    // chữ số"), KHÔNG PHẢI wording "1 chữ hoa"/"1 chữ số" mà CHANGE_PASSWORD_ERRORS định nói.
    // Xác nhận đây là hành vi hiện tại đã biết (không sửa source), quyết định 14/08/2026.
    const cau = getChangePasswordErrorMessage(
      loiHttp(
        400,
        "New password must contain at least one uppercase letter. New password must contain at least one digit.",
      ),
    );

    expect(cau).toBe(
      "Mật khẩu mới phải có ít nhất một chữ hoa. Mật khẩu mới phải có ít nhất một chữ số.",
    );
  });

  it("'Invalid access token.' — CHANGE_PASSWORD_ERRORS cục bộ bị api-messages.ts nói thay", () => {
    // Cùng lý do ở test trên: wording thật là của api-messages.ts ("không hợp lệ"), không
    // phải của CHANGE_PASSWORD_ERRORS ("đã hết hạn") dù 2 câu đọc đều hợp lý — test này chốt
    // lại hành vi thật để ai đó lỡ tưởng CHANGE_PASSWORD_ERRORS đang có tác dụng thì bị báo.
    const cau = getChangePasswordErrorMessage(loiHttp(400, "Invalid access token."));

    expect(cau).toBe("Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.");
  });

  it("câu lỗi lạ, chưa có trong bảng dịch — hiện nguyên văn thay vì chuỗi rỗng", () => {
    const cau = getChangePasswordErrorMessage(loiHttp(400, "Some new backend message."));

    expect(cau).toBe("Some new backend message.");
  });

  it("không có response (mất kết nối) — rơi về câu fallback mặc định", () => {
    const error = new AxiosError("Network Error");

    const cau = getChangePasswordErrorMessage(error);

    expect(cau).toContain("Không kết nối được tới backend");
  });
});
