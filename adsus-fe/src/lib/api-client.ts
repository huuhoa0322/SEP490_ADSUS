import axios, { AxiosError } from "axios";

import type { ApiResponse } from "@/types/api.types";

import { translateApiMessage } from "./api-messages";

/**
 * Địa chỉ backend. Đọc từ .env.local, nếu không có thì dùng cổng mặc định của
 * profile "http" trong Properties/launchSettings.json.
 *
 * Lưu ý: Next.js chỉ đọc biến môi trường LÚC KHỞI ĐỘNG. Sửa .env.local xong phải
 * tắt npm run dev rồi chạy lại, không thì vẫn dùng giá trị cũ.
 */
// Dùng "||" chứ KHÔNG dùng "??": nếu .env.local ghi NEXT_PUBLIC_API_BASE_URL= (để trống),
// "??" sẽ nhận chuỗi rỗng làm giá trị hợp lệ -> baseURL rỗng -> axios gọi đường dẫn tương
// đối tới chính Next.js (cổng 3000) -> trả 404. "||" coi chuỗi rỗng như chưa đặt và rơi về
// mặc định. Đây đúng là lỗi khiến máy thành viên gọi login ra 404.
export const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5036";

const baseURL = API_BASE_URL;

// KHÔNG đặt "Content-Type": "application/json" mặc định ở đây. axios đã tự set header đó
// cho payload là object thường (defaults/index.js: setContentType nếu chưa có). Nếu đặt cứng
// ở đây, mọi request FormData (#20/#21 — tải ảnh siêu âm) sẽ bị hasJSONContentType=true,
// khiến axios âm thầm JSON.stringify() FormData thay vì giữ nguyên multipart, trong khi
// header vẫn ghi application/json — backend nhận sai định dạng, không phải lỗi chỉ ở test.
export const apiClient = axios.create({
  baseURL,
  timeout: 60_000,
});

/** Storage key for the access token, shared by the store and the interceptor below. */
export const ACCESS_TOKEN_KEY = "adsus.accessToken";

/**
 * Attaches the token to every request. The backend uses JwtBearer, so the header has to be
 * exactly "Authorization: Bearer <token>".
 */
apiClient.interceptors.request.use((config) => {
  // window.localStorage luôn tồn tại trên trình duyệt thật; kiểm tra thêm ở đây chỉ để
  // không crash trong môi trường test (jsdom) khi localStorage chưa sẵn sàng.
  if (typeof window !== "undefined" && window.localStorage) {
    const token = window.localStorage.getItem(ACCESS_TOKEN_KEY);
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
});

/** Khoá zustand dùng để lưu phiên đăng nhập. Phải khớp tên trong auth-store.ts. */
const AUTH_STORE_KEY = "adsus.auth";

/**
 * Phiên chết thì đưa người dùng về màn đăng nhập.
 *
 * VÌ SAO CẦN: backend kiểm trạng thái tài khoản ở MỌI request. Admin khoá một tài khoản
 * (UC-04 FT-08) là token đang dùng chết ngay lập tức. Không có đoạn này thì người bị khoá
 * vẫn ngồi nguyên trong giao diện, bấm gì cũng báo lỗi mà không hiểu vì sao, còn token chết
 * thì nằm lại trong máy.
 *
 * Chỉ xử lý khi request CÓ GẮN token. Đăng nhập sai mật khẩu cũng trả 401 nhưng request đó
 * không kèm token — nếu không phân biệt, nhập sai mật khẩu một lần là trang tự tải lại và
 * người dùng không kịp đọc thông báo lỗi.
 *
 * Dùng window.location thay vì router của Next.js: đây là tệp thường, không phải component,
 * và tải lại cả trang là cách chắc chắn nhất để mọi state trong bộ nhớ bị dọn sạch.
 */
apiClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    const isUnauthorized =
      error instanceof AxiosError && error.response?.status === 401;
    const hadToken = Boolean(
      error instanceof AxiosError && error.config?.headers?.Authorization,
    );

    if (isUnauthorized && hadToken && typeof window !== "undefined" && window.localStorage) {
      window.localStorage.removeItem(ACCESS_TOKEN_KEY);
      window.localStorage.removeItem(AUTH_STORE_KEY);

      // Đang ở trang đăng nhập rồi thì thôi, tránh tải lại vòng quanh.
      if (!window.location.pathname.startsWith("/login")) {
        window.location.href = "/login?expired=1";
      }
    }

    return Promise.reject(error);
  },
);

/**
 * Extracts the error message from a backend response.
 *
 * The backend always returns { code, message, data }, even on failure, so "message" is the
 * text it deliberately chose to expose. For sign-in it returns the same sentence for every
 * possible cause (UCS GB-06), which is exactly why it must be shown as-is — không được nghĩ
 * ra lý do cụ thể hơn ở phía giao diện.
 *
 * Câu tiếng Anh đó được dịch sang tiếng Việt trước khi hiển thị: người dùng hệ thống này là
 * nhân viên phòng khám, còn API thì giữ một thứ tiếng vì còn phục vụ ứng dụng di động và đi
 * vào log. Xem api-messages.ts.
 */
export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof AxiosError) {
    const body = error.response?.data as ApiResponse<unknown> | undefined;
    if (body?.message) return translateApiMessage(body.message);

    // Không có response nghĩa là không chạm được tới backend. Nêu rõ địa chỉ đang gọi,
    // vì nguyên nhân hầu hết là backend chưa bật hoặc đang chạy ở cổng khác.
    if (!error.response) {
      return `Không kết nối được tới backend tại ${API_BASE_URL}. Kiểm tra: backend đã chạy chưa, và có đúng cổng đó không?`;
    }
  }
  return fallback;
}
