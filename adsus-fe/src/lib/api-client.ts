import axios, { AxiosError } from "axios";

import type { ApiResponse } from "@/types/api.types";

const baseURL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5036";

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

    // No response at all means the backend could not be reached.
    if (!error.response) {
      return "Không kết nối được tới máy chủ. Kiểm tra xem backend đã chạy chưa.";
    }
  }
  return fallback;
}
