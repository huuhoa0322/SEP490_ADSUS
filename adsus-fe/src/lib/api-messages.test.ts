import { describe, expect, it } from "vitest";

import { translateApiMessage } from "./api-messages";

describe("translateApiMessage", () => {
  it("dịch câu khớp nguyên văn", () => {
    expect(translateApiMessage("Account not found.")).toBe("Không tìm thấy tài khoản.");
  });

  it("bỏ qua khoảng trắng thừa hai đầu", () => {
    expect(translateApiMessage("  Account updated.  ")).toBe("Đã lưu thay đổi.");
  });

  it("dịch từng câu khi backend nối nhiều lỗi kiểm tra dữ liệu", () => {
    // Backend nối bằng string.Join(" ", ...) nên chuỗi về là vài câu dính liền. Dịch cả cụm
    // thì chỉ cần lệch một câu là hỏng toàn bộ.
    const result = translateApiMessage("Full name is required. Role is required.");

    expect(result).toBe("Vui lòng nhập họ và tên. Vui lòng chọn vai trò.");
  });

  it("giữ nguyên phần chưa có trong bảng, vẫn dịch phần còn lại", () => {
    const result = translateApiMessage("Full name is required. Something brand new.");

    expect(result).toBe("Vui lòng nhập họ và tên. Something brand new.");
  });

  it("thiếu bản dịch thì trả nguyên văn, KHÔNG trả chuỗi rỗng", () => {
    // Chuỗi rỗng là ô báo lỗi hiện ra mà không có chữ nào — người dùng chỉ thấy một khung
    // đỏ trống, không biết phải sửa gì.
    expect(translateApiMessage("A message nobody has translated yet")).toBe(
      "A message nobody has translated yet",
    );
  });

  it("giữ nguyên câu GB-06 đã được dịch, không suy diễn thêm lý do", () => {
    // UC-01 GB-06: sai số điện thoại, sai mật khẩu, tài khoản bị khoá hay bị vô hiệu hoá
    // đều phải hiện đúng một câu. Bản dịch cũng phải mơ hồ y như bản gốc.
    expect(translateApiMessage("Invalid phone number or password.")).toBe(
      "Số điện thoại hoặc mật khẩu không đúng.",
    );
  });

  it("dịch lỗi chưa đủ tuổi từ validator quản lý tài khoản", () => {
    expect(translateApiMessage("Account holder must be at least 18 years old.")).toBe(
      "Người dùng phải đủ 18 tuổi.",
    );
  });
});
