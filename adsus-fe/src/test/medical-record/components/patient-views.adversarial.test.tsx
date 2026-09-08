import { fireEvent, render, screen } from "@testing-library/react";
import { AxiosError, AxiosHeaders, type AxiosResponse } from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthStore } from "@/store/auth-store";
import { PatientListView } from "@/features/medical-record/components/patient-list-view";
import { PatientRecordView } from "@/features/medical-record/components/patient-record-view";
import type { PatientProfile, PatientSummary, CaseSummary, PagedResult } from "@/features/medical-record/types/medical-record.types";

const { listMock, profileMock, caseListMock } = vi.hoisted(() => ({
  listMock: vi.fn(),
  profileMock: vi.fn(),
  caseListMock: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

vi.mock("@/features/medical-record/hooks/use-patients", () => ({
  usePatientList: (args: unknown) => listMock(args),
}));

vi.mock("@/features/medical-record/hooks/use-patient-profile", () => ({
  usePatientProfile: (id: string | undefined) => profileMock(id),
}));

vi.mock("@/features/medical-record/hooks/use-cases", () => ({
  useCaseList: (args: unknown) => caseListMock(args),
}));

function signInAsDoctor() {
  useAuthStore.getState().signIn("access-token", "refresh-token", {
    userId: "doctor-1",
    fullName: "BS. Nguyễn Văn An",
    email: "doctor@adsus.vn",
    role: "DOCTOR",
    mustChangePassword: false,
  });
}

function createAxiosError(status: number, message: string): AxiosError {
  const response: AxiosResponse = {
    data: { code: `ERR_${status}`, message, data: null },
    status,
    statusText: "Error",
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return new AxiosError(
    message,
    `ERR_BAD_RESPONSE`,
    { headers: new AxiosHeaders() },
    {},
    response,
  );
}

describe("Adversarial QA Suite: PatientListView & PatientRecordView", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.getState().signOut();
    signInAsDoctor();
  });

  describe("1. PatientListView Adversarial Stress Tests", () => {
    it("A1. Loading State: renders loading message and hides table, pagination, and empty states", () => {
      listMock.mockReturnValue({
        data: undefined,
        isLoading: true,
        isError: false,
        error: null,
      });

      render(<PatientListView />);

      expect(screen.getByText("Đang tải danh sách...")).toBeInTheDocument();
      expect(screen.queryByRole("table")).not.toBeInTheDocument();
      expect(screen.queryByText(/không tìm thấy bệnh nhân nào/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/tổng:/i)).not.toBeInTheDocument();
    });

    it("A2. Error State: displays fallback error banner when request fails with non-Axios Error", () => {
      listMock.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: true,
        error: new Error("Network connection failure"),
      });

      render(<PatientListView />);

      const alert = screen.getByRole("alert");
      expect(alert).toBeInTheDocument();
      expect(alert).toHaveTextContent("Không tải được danh sách bệnh nhân.");
      expect(screen.queryByRole("table")).not.toBeInTheDocument();
    });

    it("A3. Error State: displays API error message from Axios-like response payload", () => {
      const axiosErr = createAxiosError(400, "Invalid query parameter");
      listMock.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: true,
        error: axiosErr,
      });

      render(<PatientListView />);

      const alert = screen.getByRole("alert");
      expect(alert).toBeInTheDocument();
      expect(alert).toHaveTextContent("Invalid query parameter");
    });

    it("A4. Empty State: renders friendly empty banner and total: 0 when items array is empty", () => {
      listMock.mockReturnValue({
        data: {
          items: [],
          page: 1,
          pageSize: 20,
          totalItems: 0,
          totalPages: 0,
        },
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientListView />);

      expect(screen.getByText("Tổng: 0 bệnh nhân")).toBeInTheDocument();
      expect(screen.getByText("Không tìm thấy bệnh nhân nào")).toBeInTheDocument();
      expect(
        screen.getByText("Thử xoá bớt điều kiện lọc hoặc kiểm tra lại từ khoá tìm kiếm."),
      ).toBeInTheDocument();
      expect(screen.queryByRole("table")).not.toBeInTheDocument();
    });

    it("A5. Null/Undefined row properties: survives null visit date, status, phone and profile", () => {
      const edgePatients: PatientSummary[] = [
        {
          patientUserId: "usr-null-all",
          patientProfileId: null,
          fullName: "Đỗ Gia Hưng",
          phone: "",
          latestVisitDate: null,
          latestVisitStatus: null,
        },
        {
          patientUserId: "usr-end-status",
          patientProfileId: "prof-1",
          fullName: "Lê Văn Tám",
          phone: "0900000001",
          latestVisitDate: "2026-08-15",
          latestVisitStatus: "END",
        },
        {
          patientUserId: "usr-created-status",
          patientProfileId: "prof-2",
          fullName: "Nguyễn Thị Hoa",
          phone: "0900000002",
          latestVisitDate: "2026-08-20",
          latestVisitStatus: "CREATED",
        },
        {
          patientUserId: "usr-unknown-status",
          patientProfileId: "prof-3",
          fullName: "Hoàng Minh",
          phone: "0900000003",
          latestVisitDate: "2026-08-25",
          // @ts-expect-error: simulating unknown status from future backend update
          latestVisitStatus: "UNKNOWN_STATUS",
        },
      ];

      listMock.mockReturnValue({
        data: {
          items: edgePatients,
          page: 1,
          pageSize: 20,
          totalItems: edgePatients.length,
          totalPages: 1,
        },
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientListView />);

      // Null profile shows "Chưa lập hồ sơ nền" and action link
      expect(screen.getByText("Chưa lập hồ sơ nền")).toBeInTheDocument();
      expect(screen.getByRole("link", { name: /tạo hồ sơ nền/i })).toHaveAttribute(
        "href",
        "/patients/new?patientUserId=usr-null-all",
      );

      // Null visit date shows "Chưa có lần khám nào"
      expect(screen.getByText("Chưa có lần khám nào")).toBeInTheDocument();

      // Null visit status renders dash "—"
      expect(screen.getByText("—")).toBeInTheDocument();

      // Status badges
      expect(screen.getByText("Đã kết thúc ca")).toBeInTheDocument();
      expect(screen.getByText("Mới tạo")).toBeInTheDocument();
    });

    it("A6. Initials generator variations: single-name, initials fallback on empty name", () => {
      const nameEdgePatients: PatientSummary[] = [
        {
          patientUserId: "usr-single",
          patientProfileId: "prof-single",
          fullName: "Tuấn",
          phone: "0911111111",
          latestVisitDate: null,
          latestVisitStatus: null,
        },
        {
          patientUserId: "usr-empty",
          patientProfileId: "prof-empty",
          fullName: "",
          phone: "0922222222",
          latestVisitDate: null,
          latestVisitStatus: null,
        },
        {
          patientUserId: "usr-multiname",
          patientProfileId: "prof-multi",
          fullName: "Nguyễn Công Phượng",
          phone: "0933333333",
          latestVisitDate: null,
          latestVisitStatus: null,
        },
      ];

      listMock.mockReturnValue({
        data: {
          items: nameEdgePatients,
          page: 1,
          pageSize: 20,
          totalItems: nameEdgePatients.length,
          totalPages: 1,
        },
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientListView />);

      // "Tuấn" -> 1 word, slice(0, 2) -> "TU"
      expect(screen.getByText("TU")).toBeInTheDocument();
      // "" -> fallback -> "PT"
      expect(screen.getByText("PT")).toBeInTheDocument();
      // "Nguyễn Công Phượng" -> first 'N' + last 'P' -> "NP"
      expect(screen.getByText("NP")).toBeInTheDocument();
    });

    it("A7. Filter & Search triggers page reset to 1", () => {
      listMock.mockReturnValue({
        data: {
          items: [],
          page: 1,
          pageSize: 20,
          totalItems: 0,
          totalPages: 1,
        },
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientListView />);

      const searchInput = screen.getByLabelText("Tìm bệnh nhân");
      fireEvent.change(searchInput, { target: { value: "Trần" } });

      expect(listMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: "Trần", page: 1 }),
      );

      const filterSelect = screen.getByLabelText("Lọc theo trạng thái lần khám");
      fireEvent.change(filterSelect, { target: { value: "Pending" } });

      expect(listMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ visitStatus: "Pending", page: 1 }),
      );
    });

    it("A8. Pagination rendered with buttons when totalPages > 1", () => {
      listMock.mockReturnValue({
        data: {
          items: [
            {
              patientUserId: "usr-1",
              patientProfileId: "p-1",
              fullName: "Bệnh nhân 1",
              phone: "0900000001",
              latestVisitDate: null,
              latestVisitStatus: null,
            },
          ],
          page: 2,
          pageSize: 20,
          totalItems: 50,
          totalPages: 3,
        },
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientListView />);

      expect(screen.getByText(/Trang 2 \/ 3 · 50 bệnh nhân/)).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Trước" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Sau" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "1" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "2" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "3" })).toBeInTheDocument();
    });

    it("A9. Adversarial Bug Discovery: fullName with only whitespace throws TypeError in getInitials", () => {
      listMock.mockReturnValue({
        data: {
          items: [
            {
              patientUserId: "usr-space",
              patientProfileId: null,
              fullName: "   ",
              phone: "0900000000",
              latestVisitDate: null,
              latestVisitStatus: null,
            },
          ],
          page: 1,
          pageSize: 20,
          totalItems: 1,
          totalPages: 1,
        },
        isLoading: false,
        isError: false,
        error: null,
      });

      // EMPIRICAL CHALLENGE: Verify if whitespace-only fullName causes TypeError
      let caughtError: unknown = null;
      try {
        render(<PatientListView />);
      } catch (err) {
        caughtError = err;
      }

      expect(caughtError).toBeInstanceOf(TypeError);
      expect((caughtError as TypeError).message).toMatch(/cannot read properties of undefined/i);
    });

    it("A10. Adversarial Bug Discovery: malformed data payload where items is undefined causes TypeError", () => {
      listMock.mockReturnValue({
        data: {
          page: 1,
          pageSize: 20,
          totalItems: 0,
          totalPages: 0,
        } as unknown as PagedResult<PatientSummary>,
        isLoading: false,
        isError: false,
        error: null,
      });

      let caughtError: unknown = null;
      try {
        render(<PatientListView />);
      } catch (err) {
        caughtError = err;
      }

      expect(caughtError).toBeInstanceOf(TypeError);
      expect((caughtError as TypeError).message).toMatch(/cannot read properties of undefined/i);
    });
  });

  describe("2. PatientRecordView Adversarial Stress Tests", () => {
    const baseProfile: PatientProfile = {
      patientProfileId: "prof-adversarial-1",
      patientUserId: "usr-adversarial-1",
      fullName: "Nguyễn Văn Test",
      phone: "0988776655",
      dateOfBirth: "1995-10-25",
      gender: "MALE",
      diseases: [],
      allergies: [],
      createdBy: "nurse-1",
      createdAt: "2026-06-01T08:00:00Z",
      updatedAt: "2026-06-02T10:00:00Z",
    };

    it("B1. Profile Loading State: shows loading banner, suppresses body", () => {
      profileMock.mockReturnValue({
        data: undefined,
        isLoading: true,
        isError: false,
        error: null,
      });
      caseListMock.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientRecordView profileId="prof-adversarial-1" />);

      expect(screen.getByText("Đang tải hồ sơ bệnh nhân...")).toBeInTheDocument();
      expect(screen.queryByText("Nguyễn Văn Test")).not.toBeInTheDocument();
    });

    it("B2. Profile Error State: renders fallback error alert when profile fails to load", () => {
      profileMock.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: true,
        error: new Error("404 Patient Profile Not Found"),
      });
      caseListMock.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientRecordView profileId="prof-adversarial-1" />);

      const alert = screen.getByRole("alert");
      expect(alert).toBeInTheDocument();
      expect(alert).toHaveTextContent("Không tải được hồ sơ bệnh nhân.");
    });

    it("B3. Cases Loading State: profile header renders intact while Tab 1 shows loading spinner", () => {
      profileMock.mockReturnValue({
        data: baseProfile,
        isLoading: false,
        isError: false,
        error: null,
      });
      caseListMock.mockReturnValue({
        data: undefined,
        isLoading: true,
        isError: false,
        error: null,
      });

      render(<PatientRecordView profileId="prof-adversarial-1" />);

      // Profile header is intact
      expect(screen.getByText("Nguyễn Văn Test")).toBeInTheDocument();
      expect(screen.getByRole("link", { name: /tạo ca khám mới/i })).toBeInTheDocument();

      // Case list loading message in Tab 1
      expect(screen.getByText("Đang tải danh sách lần khám...")).toBeInTheDocument();
    });

    it("B4. Cases Error State: profile header intact while Tab 1 displays case error alert", () => {
      profileMock.mockReturnValue({
        data: baseProfile,
        isLoading: false,
        isError: false,
        error: null,
      });
      caseListMock.mockReturnValue({
        data: undefined,
        isLoading: false,
        isError: true,
        error: new Error("Lỗi kết nối máy chủ khi tải ca khám."),
      });

      render(<PatientRecordView profileId="prof-adversarial-1" />);

      expect(screen.getByText("Nguyễn Văn Test")).toBeInTheDocument();
      const alert = screen.getByRole("alert");
      expect(alert).toHaveTextContent("Không tải được danh sách lần khám.");
    });

    it("B5. Empty Cases State: renders zero-badge and empty state card with CTA button", () => {
      profileMock.mockReturnValue({
        data: baseProfile,
        isLoading: false,
        isError: false,
        error: null,
      });
      caseListMock.mockReturnValue({
        data: {
          items: [],
          page: 1,
          pageSize: 20,
          totalItems: 0,
          totalPages: 0,
        },
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientRecordView profileId="prof-adversarial-1" />);

      // Badge in tab header
      expect(screen.getByText("0")).toBeInTheDocument();

      // Friendly empty state message & CTA
      expect(screen.getByText("Chưa có lần khám nào")).toBeInTheDocument();
      expect(
        screen.getByText(/Bấm “Tạo ca khám mới” để bắt đầu lần khám đầu tiên./),
      ).toBeInTheDocument();
    });

    it("B6. Empty & Null Allergies and Diseases: renders 'Không có' placeholder safely", () => {
      const emptyClinicalProfile: PatientProfile = {
        ...baseProfile,
        allergies: [],
        diseases: [],
      };

      profileMock.mockReturnValue({
        data: emptyClinicalProfile,
        isLoading: false,
        isError: false,
        error: null,
      });
      caseListMock.mockReturnValue({
        data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 },
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientRecordView profileId="prof-adversarial-1" />);

      // Tab 2 has forceMount, so both cards are in DOM
      const emptyTexts = screen.getAllByText("Không có");
      expect(emptyTexts).toHaveLength(2); // One for allergies, one for diseases
    });

    it("B7. Partial and Edge Demographics: missing phone, missing DOB, invalid DOB, OTHER gender", () => {
      const partialProfile: PatientProfile = {
        ...baseProfile,
        phone: "",
        dateOfBirth: null,
        gender: "OTHER",
        createdAt: "invalid-iso",
        updatedAt: "",
      };

      profileMock.mockReturnValue({
        data: partialProfile,
        isLoading: false,
        isError: false,
        error: null,
      });
      caseListMock.mockReturnValue({
        data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 },
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientRecordView profileId="prof-adversarial-1" />);

      // Gender is "Khác"
      expect(screen.getByText("Khác")).toBeInTheDocument();

      // Empty phone and DOB render "—"
      const dashes = screen.getAllByText("—");
      expect(dashes.length).toBeGreaterThanOrEqual(2);
    });

    it("B8. Complex Allergies & Diseases formatting: isOther, custom notes, standard items", () => {
      const complexProfile: PatientProfile = {
        ...baseProfile,
        allergies: [
          { allergyTypeId: "a1", allergyName: "Paracetamol", isOther: false, note: null },
          { allergyTypeId: "a2", allergyName: "Penicillin", isOther: false, note: "Sốc phản vệ độ 2" },
          { allergyTypeId: "a3", allergyName: "Khác", isOther: true, note: "Dị ứng phấn hoa cà phê" },
        ],
        diseases: [
          { diseaseId: "d1", diseaseName: "Tiểu đường tuýp 2", isOther: false, note: null },
          { diseaseId: "d2", diseaseName: "Cao huyết áp", isOther: false, note: "Đang điều trị Amlodipine" },
          { diseaseId: "d3", diseaseName: "Khác", isOther: true, note: "Viêm dạ dày Hp (+)" },
        ],
      };

      profileMock.mockReturnValue({
        data: complexProfile,
        isLoading: false,
        isError: false,
        error: null,
      });
      caseListMock.mockReturnValue({
        data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 },
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientRecordView profileId="prof-adversarial-1" />);

      // Check allergies badges
      expect(screen.getByText("Paracetamol")).toBeInTheDocument();
      expect(screen.getByText("Penicillin: Sốc phản vệ độ 2")).toBeInTheDocument();
      expect(screen.getByText("Dị ứng phấn hoa cà phê")).toBeInTheDocument();

      // Check diseases badges
      expect(screen.getByText("Tiểu đường tuýp 2")).toBeInTheDocument();
      expect(screen.getByText("Cao huyết áp: Đang điều trị Amlodipine")).toBeInTheDocument();
      expect(screen.getByText("Viêm dạ dày Hp (+)")).toBeInTheDocument();
    });

    it("B9. Case list with multiple statuses, missing dates, and pagination", () => {
      const caseItems: CaseSummary[] = [
        {
          caseId: "case-101",
          visitDate: "2026-07-01",
          status: "CONFIRMED",
          doctorId: "doc-1",
          createdAt: "2026-07-01T09:30:00Z",
        },
        {
          caseId: "case-102",
          // @ts-expect-error: simulating null visitDate
          visitDate: null,
          status: "END",
          doctorId: "doc-2",
          createdAt: "2026-07-05T14:15:00Z",
        },
        {
          caseId: "case-103",
          visitDate: "2026-07-10",
          status: "CREATED",
          doctorId: "doc-3",
          createdAt: "2026-07-10T16:00:00Z",
        },
      ];

      profileMock.mockReturnValue({
        data: baseProfile,
        isLoading: false,
        isError: false,
        error: null,
      });
      caseListMock.mockReturnValue({
        data: {
          items: caseItems,
          page: 1,
          pageSize: 20,
          totalItems: 25,
          totalPages: 2,
        },
        isLoading: false,
        isError: false,
        error: null,
      });

      render(<PatientRecordView profileId="prof-adversarial-1" />);

      // Badges
      expect(screen.getByText("Đã kết luận")).toBeInTheDocument();
      expect(screen.getByText("Đã kết thúc ca")).toBeInTheDocument();
      expect(screen.getByText("Mới tạo")).toBeInTheDocument();

      // Links to case detail (3 cases -> 3 links)
      const detailLinks = screen.getAllByRole("link", { name: /xem chi tiết ca/i });
      expect(detailLinks).toHaveLength(3);
      expect(detailLinks[0]).toHaveAttribute("href", "/cases/case-101");
      expect(detailLinks[1]).toHaveAttribute("href", "/cases/case-102");
      expect(detailLinks[2]).toHaveAttribute("href", "/cases/case-103");

      // Pagination
      expect(screen.getByText(/Trang 1 \/ 2 · 25 lần khám/)).toBeInTheDocument();
    });

    it("B10. Adversarial Bug Discovery: profile fullName with only whitespace throws TypeError in getInitials", () => {
      const whitespaceProfile: PatientProfile = {
        ...baseProfile,
        fullName: "   ",
      };

      profileMock.mockReturnValue({
        data: whitespaceProfile,
        isLoading: false,
        isError: false,
        error: null,
      });
      caseListMock.mockReturnValue({
        data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 },
        isLoading: false,
        isError: false,
        error: null,
      });

      // EMPIRICAL CHALLENGE: Verify if whitespace-only fullName in PatientRecordView causes TypeError
      let caughtError: unknown = null;
      try {
        render(<PatientRecordView profileId="prof-adversarial-1" />);
      } catch (err) {
        caughtError = err;
      }

      expect(caughtError).toBeInstanceOf(TypeError);
      expect((caughtError as TypeError).message).toMatch(/cannot read properties of undefined/i);
    });

    it("B11. Adversarial Bug Discovery: cases payload where items is undefined causes TypeError", () => {
      profileMock.mockReturnValue({
        data: baseProfile,
        isLoading: false,
        isError: false,
        error: null,
      });
      caseListMock.mockReturnValue({
        data: {
          page: 1,
          pageSize: 20,
          totalItems: 0,
          totalPages: 0,
        } as unknown as PagedResult<CaseSummary>,
        isLoading: false,
        isError: false,
        error: null,
      });

      let caughtError: unknown = null;
      try {
        render(<PatientRecordView profileId="prof-adversarial-1" />);
      } catch (err) {
        caughtError = err;
      }

      expect(caughtError).toBeInstanceOf(TypeError);
      expect((caughtError as TypeError).message).toMatch(/cannot read properties of undefined/i);
    });
  });
});
