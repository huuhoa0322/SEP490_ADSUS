import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { medicalRecordQueryKeys } from "@/features/medical-record/hooks/query-keys";
import {
  useCreatePatientProfile,
  usePatientProfile,
  useUpdatePatientProfile,
} from "@/features/medical-record/hooks/use-patient-profile";

function makeWrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

const fakeProfile = {
  patientProfileId: "profile-1",
  patientUserId: "user-1",
  fullName: "Nguyễn Thị Hoa",
  phone: "0981111001",
  dateOfBirth: "1992-05-14",
  gender: "FEMALE",
  diseases: [],
  allergies: [],
  createdBy: "nurse-1",
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: "2026-08-01T00:00:00Z",
};

describe("usePatientProfile", () => {
  it("tải hồ sơ nền khi có profileId", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/patient-profiles/profile-1`, () =>
        HttpResponse.json({ code: 200, message: "ok", data: fakeProfile }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => usePatientProfile("profile-1"), {
      wrapper: makeWrapper(client),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.fullName).toBe("Nguyễn Thị Hoa");
  });

  it("không gọi API khi profileId undefined (tài khoản chưa có hồ sơ nền)", async () => {
    let called = false;
    server.use(
      http.get(`${API_BASE_URL}/api/v1/patient-profiles/:profileId`, () => {
        called = true;
        return HttpResponse.json({ code: 200, message: "ok", data: fakeProfile });
      }),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => usePatientProfile(undefined), {
      wrapper: makeWrapper(client),
    });

    expect(result.current.fetchStatus).toBe("idle");
    expect(called).toBe(false);
  });
});

describe("useCreatePatientProfile", () => {
  it("làm mới toàn bộ cache Module 04 sau khi tạo hồ sơ nền", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/patient-profiles`, () =>
        HttpResponse.json({ code: 200, message: "ok", data: fakeProfile }, { status: 201 }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");
    const { result } = renderHook(() => useCreatePatientProfile(), { wrapper: makeWrapper(client) });

    result.current.mutate({ patientUserId: "user-1", gender: "FEMALE", diseases: [], allergies: [] });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidate).toHaveBeenCalledWith({ queryKey: medicalRecordQueryKeys.all });
  });
});

describe("useUpdatePatientProfile", () => {
  it("chỉ làm mới đúng hồ sơ vừa sửa, không invalidate toàn bộ", async () => {
    server.use(
      http.put(`${API_BASE_URL}/api/v1/patient-profiles/profile-1`, () =>
        HttpResponse.json({ code: 200, message: "ok", data: fakeProfile }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");
    const { result } = renderHook(() => useUpdatePatientProfile("profile-1"), {
      wrapper: makeWrapper(client),
    });

    result.current.mutate({ gender: "FEMALE", diseases: [], allergies: [] });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidate).toHaveBeenCalledWith({
      queryKey: medicalRecordQueryKeys.profile("profile-1"),
    });
  });
});
