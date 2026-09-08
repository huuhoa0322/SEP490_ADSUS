# ADSUS Frontend — Phase 7 Improvement Spec (REVISED)

## Context

ADSUS is a Vietnamese medical clinic management application (SEP-490 project).
Frontend: Next.js 15 (App Router), TypeScript, shadcn/ui + Tailwind, TanStack Query.
Backend: ASP.NET Core (C#), REST API at `/api/v1/`.

**Phase 7 scope (revised):** Improve the existing Nurse invoice flow (Module 9) and add new features on top of it. **Module 7 prescription flow stays as-is** — Doctor keeps redirecting to `/cases/{caseId}` after creating prescription (the auto-generated invoice is handled separately by Nurse in `/invoices`).

> **Important corrections from previous spec**:
> 1. Routes `/invoices` and `/invoices/[id]` **already exist** (created in earlier work) and use the existing `InvoiceListView` and `InvoiceDetailView` components — they already work (verified by user with screenshots).
> 2. The "Kết thúc ca bệnh" Dialog in `case-detail-view.tsx` is **kept** per user decision — it's for cases without prescription, distinct from the prescription flow.

---

## 1. Goals

| # | Goal | Module |
|---|------|--------|
| G1 | Migrate `InvoiceListView` & `InvoiceDetailView` from `useState + useEffect` to TanStack Query hooks (consistent with `patient-list-view.tsx` pattern) | Module 9 |
| G2 | Add **status filter** to invoice list (PENDING / PAID / CANCELLED / ALL) | Module 9 |
| G3 | Add **payment method badges** (CASH / BANK_TRANSFER) to invoice list column | Module 9 |
| G4 | Surface **cancel reason** & **paid-at time** in invoice list row (currently only visible in detail) | Module 9 |
| G5 | Add empty-state CTA on invoice list ("Chưa có hóa đơn nào — chờ ca khám có kê đơn") | Module 9 |
| G6 | Fix existing `eslint-disable react-hooks/set-state-in-effect` in both invoice components — replace with TanStack Query so the disable comment is no longer needed | Module 9 |
| G7 | Add missing test: `invoice-list-view.test.tsx` (detail already has one) | Module 9 |
| G8 | Add **link to invoice list from case detail** (Nurse only) when the case has an invoice — see Module 7 enhancement | Module 7 + 9 |
| G9 | Show **case ID** in invoice list + detail for traceability | Module 9 |

> **Out of scope** (deliberately excluded):
> - Modifying `NewPrescriptionPage` redirect (stays at `/cases/{caseId}`)
> - Removing "Kết thúc ca bệnh" Dialog (kept per user)
> - New routes (all needed routes exist)

---

## 2. Module 9 — Invoice Flow Improvements

### 2.1 Convert to TanStack Query

Create hooks file: `src/features/prescription-adherence/hooks/use-invoices.ts`

Exports:
```ts
useInvoicesList(filter: InvoiceFilter) → { data, isLoading, isError, error }
useInvoiceDetail(invoiceId: string)   → { data, isLoading, isError, error, refetch }
usePayInvoice()                       → mutation
useCancelInvoice()                    → mutation
```

Pattern: match `src/features/medical-record/hooks/use-patients.ts` style.

### 2.2 Invoice List Component (`InvoiceListView`)

**File:** `src/features/prescription-adherence/components/invoice-list-view.tsx`

Changes:
- Replace `useState` + `useEffect` + `useCallback` with `useInvoicesList(filter)` hook.
- Add **status filter dropdown** next to search (All / Chờ thanh toán / Đã thanh toán / Đã hủy).
- Add **payment method badge** column when status === PAID.
- Add **cancel reason** tooltip/popover for CANCELLED rows.
- Show **case ID** as monospace text (consistent with patient code styling).
- Empty state: include helper text + refresh button.

State management:
```ts
const [search, setSearch]   = useState("");
const [status, setStatus]   = useState<InvoiceStatus | "ALL">("ALL");
const [page, setPage]       = useState(1);
const filter = { search, status, page, pageSize: 10 };
const { data, isLoading, isError, error } = useInvoicesList(filter);
```

### 2.3 Invoice Detail Component (`InvoiceDetailView`)

**File:** `src/features/prescription-adherence/components/invoice-detail-view.tsx`

Changes:
- Replace `useState + useEffect` with `useInvoiceDetail(id)` + `usePayInvoice()` + `useCancelInvoice()` mutations.
- Mutation `onSuccess` triggers `refetch()` from the detail hook (instead of manual `fetchDetail()` call).
- Remove `eslint-disable react-hooks/set-state-in-effect` comments (no longer needed).
- Replace `(error as any).response?.data?.message` with `getApiErrorMessage(error, "...")` (existing helper, used elsewhere — see `case-detail-view.tsx`).
- Display **case ID** as a clickable link → `/cases/{caseId}` (only for Nurse role — check auth store).

### 2.4 Tests

**File:** `src/test/prescription-adherence/components/invoice-list-view.test.tsx` (NEW)

Test cases:
1. Renders table with invoices on success.
2. Shows loading state while fetching.
3. Shows empty state when zero results.
4. Search input + button triggers refetch with new filter.
5. Status filter changes the query.
6. Pagination changes page.

Use the same `vi.mock` pattern from `invoice-detail-view.test.tsx`.

---

## 3. Module 7 + Module 9 Bridge

### 3.1 Surface invoice from case detail (Nurse)

**File:** `src/features/medical-record/components/case-detail-view.tsx`

Add a new section (visible to NURSE role, regardless of case status — END or CONFIRMED):
- If case has invoice(s) → show summary card with: invoice ID, status badge, total, button "Chi tiết hóa đơn" → `/invoices/{id}`.
- Source: `GET /api/v1/cases/{caseId}/invoices` — need to add this to `invoiceService.ts` (returns `InvoiceResponse[]`).

> Note: Backend `GET /api/v1/cases/{caseId}/invoices` endpoint existence needs verification during implementation. If absent, fall back to passing `caseId` to invoice list with a pre-filter (currently `getInvoices` only supports `search` + `status` — backend may need a `caseId` filter param).

### 3.2 New service method

**File:** `src/api/invoiceService.ts`

Add:
```ts
getCaseInvoices: async (caseId: string): Promise<InvoiceResponse[]> => {
  const response = await api.get<ApiResponse<InvoiceResponse[]>>(
    `/api/v1/cases/${caseId}/invoices`
  );
  return (response.data.data ?? []) as InvoiceResponse[];
},
```

### 3.3 New hook

**File:** `src/features/prescription-adherence/hooks/use-invoices.ts` (same file as §2.1)

Add:
```ts
useCaseInvoices(caseId: string | undefined) → { data, isLoading }
```
- `enabled: !!caseId`
- `staleTime: 30s`

---

## 4. UI / UX

### 4.1 Design tokens

| Element | Token | Hex |
|---|---|---|
| PENDING badge | `--amber-warn` | `#E8963C` |
| PAID badge | `--success` | `#1E9E6B` |
| CANCELLED badge | `--danger` | `#D8453B` |
| Bank transfer pill | `--teal-tint` bg + `--ink-navy` text | `#E4F5F3` + `#1F2A44` |
| Cash pill | `--teal-primary` bg + white text | `#128C82` |

Use existing Badge variants where possible; add custom class only if needed.

### 4.2 Accessibility

- Status filter: `<select>` with `aria-label="Lọc theo trạng thái"`.
- Search input: `aria-label="Tìm kiếm hóa đơn"`.
- Cancel button in detail view: stays enabled even when status !== PENDING (per existing logic — allows refund of PAID invoice too).
- Loading state: visible spinner with `role="status"` + `aria-live="polite"`.
- Empty state: `role="status"`.

### 4.3 Responsive

- Invoice list table: collapse "Mã Hóa Đơn" + "Ngày Tạo" to icon-only on `<sm`.
- Detail view QR card: stack under main column on `<md`.

---

## 5. File Inventory

| Action | File | Notes | Status |
|--------|------|-------|--------|
| MODIFY | `src/api/invoiceService.ts` | Add `getCaseInvoices` | pending |
| CREATE | `src/features/prescription-adherence/hooks/use-invoices.ts` | New TanStack Query hooks | **DONE** |
| MODIFY | `src/features/prescription-adherence/components/invoice-list-view.tsx` | Rewrite with hooks + filters + better empty state | **DONE** |
| MODIFY | `src/features/prescription-adherence/components/invoice-detail-view.tsx` | Rewrite with hooks + fix error helper + case link | pending |
| MODIFY | `src/features/medical-record/components/case-detail-view.tsx` | Add invoice summary card for Nurse | pending |
| CREATE | `src/test/prescription-adherence/components/invoice-list-view.test.tsx` | New test (detail test already exists) | pending |
| UPDATE | `src/test/prescription-adherence/components/invoice-detail-view.test.tsx` | Update mocks for `useInvoiceDetail` etc. | pending |

---

## 6. Acceptance Criteria

### Module 9 — Invoice List
- [x] List uses TanStack Query (`useInvoicesList`), not raw `useState`/`useEffect`.
- [x] No `eslint-disable react-hooks/set-state-in-effect` in invoice components.
- [x] Status filter dropdown works (4 options: ALL / PENDING / PAID / CANCELLED).
- [x] Search + button triggers refetch; pressing Enter also triggers.
- [x] Payment method badge appears for PAID invoices (CASH / BANK_TRANSFER).
- [ ] Case ID column visible; clickable (opens `/cases/{id}` in new tab on `Cmd+Click`). — ** deferred to §2.3 (needs Case ID clickable link in detail first)**
- [x] Empty state has helpful copy + retry button.
- [x] Loading state uses accessible spinner.
- [x] Responsive: less critical columns hidden on mobile.

### Module 9 — Invoice Detail
- [ ] Detail uses TanStack Query (`useInvoiceDetail`) + mutation hooks.
- [ ] Mutations trigger `refetch()` after success (no manual fetch call).
- [ ] Errors use `getApiErrorMessage` helper.
- [ ] Case ID link to `/cases/{caseId}` works for Nurse.
- [ ] Cancel + Pay flows still work end-to-end (toast on success, error message on failure).

### Module 7 + Module 9 Bridge
- [ ] Nurse viewing case detail sees invoice summary card (if case has invoice).
- [ ] Card shows invoice status + total + "Chi tiết hóa đơn" link.
- [ ] Card hides when case has no invoice.
- [ ] Doctor sees NO invoice section (only Nurse).

### Tests
- [ ] `invoice-list-view.test.tsx` covers loading / empty / search / status filter / pagination.
- [ ] `invoice-detail-view.test.tsx` still passes after refactor (mocks updated).
- [ ] All tests pass: `npm run test` (or `vitest`).

---

## 7. Open Questions for Implementation

These will be verified during implementation, not blocking:

1. **Backend endpoint `GET /api/v1/cases/{caseId}/invoices`** — does it exist? If not, alternative: filter invoice list via `search=` with `caseId:` prefix, OR ask backend to add endpoint.
2. **Existing test file mocks `invoiceService` directly** — after refactor to hooks, mocks need to switch to mock the hook (or keep mocking service — both work, but consistent with project pattern wins).
3. **`react-hot-toast` vs project toast helper** — current invoice components use `react-hot-toast` directly. Project pattern (`patient-list-view.tsx`) also uses it. Keep as-is.
