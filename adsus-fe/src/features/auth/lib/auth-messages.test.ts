import { AxiosError, AxiosHeaders } from "axios";
import { describe, expect, it } from "vitest";

import { WebNotAvailableForRoleError } from "../types/auth.types";

import { getSignInErrorMessage } from "./auth-messages";

/** Dựng một lỗi axios đúng mã HTTP cần thử. */
function loiHttp(status: number): AxiosError {
  const error = new AxiosError("Request failed");
  error.response = {
    status,
    statusText: "",
    data: {},
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

  it("bệnh nhân đăng nhập trên Web được chỉ sang ứng dụng di động", () => {
    // UC-01: SCR-01 (Web) dành cho Admin/Doctor/Nurse, bệnh nhân dùng SCR-02 trên mobile.
    // Mật khẩu đúng nên KHÔNG dùng câu chung của GB-06.
    const cau = getSignInErrorMessage(new WebNotAvailableForRoleError());

    expect(cau).toContain("điện thoại");
    expect(cau).not.toBe("Số điện thoại hoặc mật khẩu không đúng.");
  });
});
