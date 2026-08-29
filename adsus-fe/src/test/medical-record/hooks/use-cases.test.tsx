import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";

import { API_BASE_URL } from "@/lib/api-client";
import { server } from "@/test/mocks/server";

import { medicalRecordQueryKeys } from "@/features/medical-record/hooks/query-keys";
import {
  useCaseDetail,
  useConfirmCase,
  useCreateCase,
  useEndCaseWithoutPrescription,
  useSaveCaseConclusion,
} from "@/features/medical-record/hooks/use-cases";

function makeWrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

const fakeCase = {
  caseId: "case-1",
  patientProfileId: "profile-1",
  doctorId: "doctor-1",
  doctorName: "BS. Lê Minh Hoàng",
  visitDate: "2026-08-01",
  clinicalInfo: "Đau tức vú trái",
  status: "CREATED",
  finalDiagnosis: null,
  doctorConclusion: null,
  patientProfile: null,
  ultrasoundImages: [],
  symptoms: [],
  aiResults: [],
  prescription: null,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: "2026-08-01T00:00:00Z",
};

describe("useCaseDetail", () => {
  it("tải chi tiết ca khám khi có caseId", async () => {
    server.use(
      http.get(`${API_BASE_URL}/api/v1/cases/case-1`, () =>
        HttpResponse.json({ code: 200, message: "ok", data: fakeCase }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useCaseDetail("case-1"), { wrapper: makeWrapper(client) });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.doctorName).toBe("BS. Lê Minh Hoàng");
  });

  it("không gọi API khi caseId undefined", async () => {
    let called = false;
    server.use(
      http.get(`${API_BASE_URL}/api/v1/cases/:caseId`, () => {
        called = true;
        return HttpResponse.json({ code: 200, message: "ok", data: fakeCase });
      }),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useCaseDetail(undefined), { wrapper: makeWrapper(client) });

    expect(result.current.fetchStatus).toBe("idle");
    expect(called).toBe(false);
  });
});

describe("useCreateCase", () => {
  it("làm mới toàn bộ cache Module 04 sau khi tạo ca khám", async () => {
    server.use(
      http.post(`${API_BASE_URL}/api/v1/cases`, () =>
        HttpResponse.json({ code: 200, message: "ok", data: fakeCase }, { status: 201 }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");
    const { result } = renderHook(() => useCreateCase(), { wrapper: makeWrapper(client) });

    result.current.mutate({
      patientProfileId: "profile-1",
      responsibleDoctorId: "doctor-1",
      clinicalInfo: "Đau tức vú trái",
      symptoms: [],
      images: [],
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidate).toHaveBeenCalledWith({ queryKey: medicalRecordQueryKeys.all });
  });
});

describe("useSaveCaseConclusion", () => {
  it("chỉ làm mới chi tiết ca này, không đổi danh sách (chưa đổi trạng thái)", async () => {
    server.use(
      http.put(`${API_BASE_URL}/api/v1/cases/case-1/conclusion`, () =>
        HttpResponse.json({ code: 200, message: "ok", data: fakeCase }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");
    const { result } = renderHook(() => useSaveCaseConclusion("case-1"), {
      wrapper: makeWrapper(client),
    });

    result.current.mutate({ finalDiagnosis: "U tuyến xơ vú", doctorConclusion: "Theo dõi" });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidate).toHaveBeenCalledWith({ queryKey: medicalRecordQueryKeys.case("case-1") });
    expect(invalidate).not.toHaveBeenCalledWith({ queryKey: medicalRecordQueryKeys.all });
  });
});

describe("useConfirmCase", () => {
  it("làm mới cả chi tiết ca lẫn toàn bộ danh sách (đổi trạng thái CONFIRMED)", async () => {
    server.use(
      http.put(`${API_BASE_URL}/api/v1/cases/case-1/confirm`, () =>
        HttpResponse.json({ code: 200, message: "ok", data: fakeCase }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");
    const { result } = renderHook(() => useConfirmCase("case-1"), { wrapper: makeWrapper(client) });

    result.current.mutate({ finalDiagnosis: "U tuyến xơ vú", doctorConclusion: "Theo dõi" });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidate).toHaveBeenCalledWith({ queryKey: medicalRecordQueryKeys.case("case-1") });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: medicalRecordQueryKeys.all });
  });
});

describe("useEndCaseWithoutPrescription", () => {
  it("làm mới cả chi tiết ca lẫn toàn bộ danh sách sau khi kết thúc ca", async () => {
    server.use(
      http.put(`${API_BASE_URL}/api/v1/cases/case-1/end`, () =>
        HttpResponse.json({ code: 200, message: "ok", data: fakeCase }),
      ),
    );

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, "invalidateQueries");
    const { result } = renderHook(() => useEndCaseWithoutPrescription("case-1"), {
      wrapper: makeWrapper(client),
    });

    result.current.mutate();

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidate).toHaveBeenCalledWith({ queryKey: medicalRecordQueryKeys.case("case-1") });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: medicalRecordQueryKeys.all });
  });
});
