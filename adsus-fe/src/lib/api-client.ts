import axios, { AxiosError } from "axios";

import type { ApiResponse } from "@/types/api.types";

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

export const apiClient = axios.create({
  baseURL,
  headers: { "Content-Type": "application/json" },
  timeout: 15_000,
});

/** Storage key for the access token, shared by the store and the interceptor below. */
export const ACCESS_TOKEN_KEY = "adsus.accessToken";

/**
 * Attaches the token to every request. The backend uses JwtBearer, so the header has to be
 * exactly "Authorization: Bearer <token>".
 */
apiClient.interceptors.request.use((config) => {
  if (typeof window !== "undefined") {
    const token = window.localStorage.getItem(ACCESS_TOKEN_KEY);
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
});

/**
 * Extracts the error message from a backend response.
 *
 * The backend always returns { code, message, data }, even on failure, so "message" is the
 * text it deliberately chose to expose. For sign-in it returns the same sentence for every
 * possible cause (UCS GB-06), which is exactly why it should be displayed verbatim.
 */
export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof AxiosError) {
    const body = error.response?.data as ApiResponse<unknown> | undefined;
    if (body?.message) return body.message;

    // Không có response nghĩa là không chạm được tới backend. Nêu rõ địa chỉ đang gọi,
    // vì nguyên nhân hầu hết là backend chưa bật hoặc đang chạy ở cổng khác.
    if (!error.response) {
      return `Không kết nối được tới backend tại ${API_BASE_URL}. Kiểm tra: backend đã chạy chưa, và có đúng cổng đó không?`;
    }
  }
  return fallback;
}
