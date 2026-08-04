# ADSUS API Specification — Module 08: Appointment Scheduling

| Field | Value |
|---|---|
| Document version | v0.1 (draft) — 2026-07-30 |
| Role | Senior API Architect + Detail Designer |
| Scope | Endpoints #46–#54 of `ADSUS_API_Catalog_v1.1.md` (approved catalog — **no endpoint added, removed, or renamed here**) |
| Sources | `.ai-context/project_context.md` · `.ai-context/api_design_rule/api_design_rules_v0.2.md` · `Reports_md/Report_3.1_UCS_ADSUS.md` (UC-13, UC-14, UC-15, UC-16) · `Documents/02_Requirements/SQL/ADSUS_Physical_Schema_PostgreSQL_v1.sql` (`schedule_slots`, `appointments`) |
| Language | English |

> Same ERD substitution note as prior modules: field types below come from `ADSUS_Physical_Schema_PostgreSQL_v1.sql`, not the Logical ERD.

---

## 0. Flags & Open Issues

| # | Issue | Where it shows up | Recommendation |
|---|---|---|---|
| **F1** | **Unresolved business-rule conflict between the physical schema and the UCS decision the approved catalog was built on — the most significant flag in this document.** The `slot_status` enum has 3 values (`OPEN, FULL, CLOSED`), and the `schedule_slots` table's own `COMMENT` explicitly describes a "v2 simplification": *"mỗi khung giờ mặc định 1 bệnh nhân (không có capacity) — status chuyển FULL ngay khi có 1 booking BOOKED"* (each slot defaults to 1 patient — no capacity concept — status moves to `FULL` as soon as it receives 1 `BOOKED` appointment). This is the **opposite** of the UCS's own later, dated decision (`Report_3.1_UCS_ADSUS.md`, resolved 2026-07-23): *"no Capacity attribute exists… the number of Appointments per slot is unlimited… status is only Open → Closed — there is no 'Full' value… cancelling an appointment never needs to change the slot's status."* The API Catalog (and every endpoint below) was built on the **UCS's** decision, since that is what the approved catalog encodes. | `#47` status filter, `#48` status transitions, `#50` booking side-effects | **Not resolved here — flagged for whoever owns the schema/UCS to reconcile.** Note also that the DB's own *hard* constraints do not force the single-booking-per-slot behavior either: `uq_appointments_active_booking` is scoped to `(slot_id, patient_profile_id)`, which only blocks the *same* patient from double-booking the *same* slot — it does **not** prevent multiple *different* patients from booking the same slot. So the "1 patient per slot" behavior exists only as a comment describing intended application logic, not as an enforced constraint — meaning implementing the UCS's "unlimited bookings, ignore `FULL`" decision (as this spec does) does not require a schema migration, only that the application layer never writes `FULL`. Implementing the comment's original intent instead would require additional application logic beyond what any current constraint provides. |
| F2 | `#47`/`#49` list endpoints are paginated (`PagedResult<T>`) for consistency with `schedule_slots`/`appointments` being tables that can grow over many doctors/days/patients — not explicitly named in §7.1's list, but the same per-growth reasoning applies (`idx_schedule_slots_open`, `idx_appointments_patient` exist specifically to support paginated, indexed queries). | `#47`, `#49`, `#53` | Judgment call, flagged for consistency — not a literal rule citation like Module 07's F2. |

---

## 46. `POST /api/v1/schedule-slots`

| Field | Value |
|---|---|
| Summary | Publish a new `OPEN` schedule slot (date, time range, doctor); rejects an overlapping time range for the same doctor (UC-15). |
| Auth | **Role-restricted (Doctor, Nurse)** |
| Type | `[CRUD]` |

**Request Body — `CreateScheduleSlotRequest`** (name reused verbatim from UC-15's own Request Fields table)

| Field | Type | Required | Rule |
|---|---|---|---|
| `doctorId` | string (UUID) | Yes | Must reference an account with `role = DOCTOR`. |
| `slotDate` | string (date) | Yes | Must not be in the past (UC-15 BR-01). |
| `startTime` | string (`HH:mm`) | Yes | |
| `endTime` | string (`HH:mm`) | Yes | Must be `> startTime`, and the gap must exceed 15 minutes (UC-15 BR-01, `ck_schedule_slots_time_order`). Must not overlap any existing slot for the same `doctorId`/`slotDate` (`ex_schedule_slots_no_overlap` — enforced at the DB layer via a `GiST EXCLUDE` constraint, a hard guarantee, not just an application check). |

```json
{ "doctorId": "9a1b2c3d-4e5f-6789-0abc-def123456789", "slotDate": "2026-08-01", "startTime": "09:00", "endTime": "10:00" }
```

**Success Response — `ScheduleSlotResponse`** — `201 Created`

```json
{
  "code": 201,
  "message": "Schedule slot created successfully",
  "data": {
    "slotId": "f6a7b8c9-5555-6666-7777-88889999aaaa",
    "doctorId": "9a1b2c3d-4e5f-6789-0abc-def123456789",
    "slotDate": "2026-08-01",
    "startTime": "09:00:00",
    "endTime": "10:00:00",
    "status": "OPEN",
    "createdAt": "2026-07-30T09:40:00Z",
    "updatedAt": "2026-07-30T09:40:00Z"
  }
}
```

**Error Responses**

| Code | Condition |
|---|---|
| 400 | `doctorId`/`slotDate`/`startTime`/`endTime` missing/malformed. |
| 401 | Missing/expired access token. |
| 403 | Caller authenticated but not `Doctor`/`Nurse`. |
| 404 | `doctorId` does not exist. |
| 409 | The exact `(doctorId, slotDate, startTime)` combination already exists (`uq_schedule_slots_start`). |
| 422 | `slotDate` is in the past; `endTime ≤ startTime` or the gap is ≤ 15 minutes; the time range overlaps an existing slot for the same doctor (`ex_schedule_slots_no_overlap`). |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse**.

---

## 47. `GET /api/v1/schedule-slots`

| Field | Value |
|---|---|
| Summary | List/filter schedule slots — used both by Doctor/Nurse managing the calendar and by Patient browsing available slots to book (UC-13, UC-15). |
| Auth | **Role-restricted (Doctor, Nurse, Patient)** |
| Type | `[CRUD]` |

**Query Parameters — `ScheduleSlotSearchCriteria`**

| Field | Type | Required | Rule |
|---|---|---|---|
| `doctorId` | string (UUID) | No | Filters to one doctor's slots. |
| `slotDate` | string (date) | No | Filters to one day. |
| `status` | enum `OPEN\|CLOSED` | No | **`FULL` is intentionally not an accepted value here — see F1.** A Patient browsing to book should in practice always filter `status=OPEN`, though the parameter is optional at the contract level. |
| `page` / `pageSize` | integer | No | Default `1` / `20`, max `pageSize` `100` (see F2). |

**Success Response — `PagedResult<ScheduleSlotResponse>`** — `200 OK` (same item shape as `#46`'s response).

**Error Responses**

| Code | Condition |
|---|---|
| 400 | Invalid `status` value; `page`/`pageSize` out of range. |
| 401 | Missing/expired access token. |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse, Patient** (any authenticated role — no data is patient-specific at this level; a Patient sees the same slot list a Doctor would).

---

## 48. `PATCH /api/v1/schedule-slots/{id}`

| Field | Value |
|---|---|
| Summary | Close a slot via `{"status":"CLOSED"}` — terminal, no reopen; slot status is never affected by booking count (no Capacity concept) (UC-15). |
| Auth | **Role-restricted (Doctor, Nurse)** |
| Type | `[CRUD]` |

**Path Parameters**

| Name | Type | Required | Rule |
|---|---|---|---|
| `id` | string (UUID) | Yes | `slotId`. Must exist and currently be `OPEN`. |

**Request Body — `UpdateScheduleSlotStatusRequest`**

| Field | Type | Required | Rule |
|---|---|---|---|
| `status` | enum `CLOSED` | Yes | Only valid target value at this endpoint — `Closed` is terminal (UC-15 BR-02); there is no client-settable transition back to `OPEN`, and (per F1) this spec never writes `FULL`. |

```json
{ "status": "CLOSED" }
```

**Success Response — `ScheduleSlotResponse`** — `200 OK` (same shape as `#46`'s response, `status: "CLOSED"`).

**Error Responses**

| Code | Condition |
|---|---|
| 400 | `status` missing or not `CLOSED`. |
| 401 | Missing/expired access token. |
| 403 | Caller authenticated but not `Doctor`/`Nurse`. |
| 404 | `id` does not exist. |
| 422 | The slot is already `CLOSED` (terminal, no re-transition). |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse**.

---

## 49. `GET /api/v1/schedule-slots/{id}/appointments`

| Field | Value |
|---|---|
| Summary | List the bookings on one slot, so Doctor/Nurse can see affected patients before closing it (UC-15 AF-02). |
| Auth | **Role-restricted (Doctor, Nurse)** |
| Type | `[CRUD]` |

**Path Parameters**

| Name | Type | Required | Rule |
|---|---|---|---|
| `id` | string (UUID) | Yes | `slotId`. Must exist. |

**Query Parameters**

| Field | Type | Required | Rule |
|---|---|---|---|
| `page` / `pageSize` | integer | No | Default `1` / `20`, max `pageSize` `100` (see F2 — in practice small per slot, but the DB does not cap it, see F1). |

**Success Response — `PagedResult<AppointmentSummaryResponse>`** — `200 OK`

```json
{
  "code": 200,
  "message": "Appointments retrieved successfully",
  "data": {
    "items": [
      { "appointmentId": "a7b8c9d0-6666-7777-8888-9999aaaabbbb", "patientProfileId": "a1b2c3d4-1111-2222-3333-444455556666", "status": "BOOKED", "reason": "Follow-up" }
    ],
    "page": 1, "pageSize": 20, "totalItems": 1, "totalPages": 1
  }
}
```

**Error Responses**

| Code | Condition |
|---|---|
| 400 | `page`/`pageSize` out of range. |
| 401 | Missing/expired access token. |
| 403 | Caller authenticated but not `Doctor`/`Nurse`. |
| 404 | `id` does not exist. |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse**.

---

## 50. `POST /api/v1/appointments`

| Field | Value |
|---|---|
| Summary | Book an `OPEN` slot; rejects if the Patient already holds another `BOOKED` appointment in the same window (BR — one active booking per patient per slot) (UC-13). |
| Auth | **Role-restricted (Patient)** |
| Type | `[CRUD]` |

**Request Body — `BookAppointmentRequest`** (name reused verbatim from UC-13's own Request Fields table)

| Field | Type | Required | Rule |
|---|---|---|---|
| `scheduleSlotId` | string (UUID) | Yes | Must reference a slot currently at `status = OPEN`. |
| `reason` | string | No | Visit reason/note. |

```json
{ "scheduleSlotId": "f6a7b8c9-5555-6666-7777-88889999aaaa", "reason": "Follow-up ultrasound" }
```

**Success Response — `AppointmentResponse`** — `201 Created`

```json
{
  "code": 201,
  "message": "Appointment booked successfully",
  "data": {
    "appointmentId": "a7b8c9d0-6666-7777-8888-9999aaaabbbb",
    "slotId": "f6a7b8c9-5555-6666-7777-88889999aaaa",
    "patientProfileId": "a1b2c3d4-1111-2222-3333-444455556666",
    "reason": "Follow-up ultrasound",
    "status": "BOOKED",
    "cancelledReason": null,
    "calendarSyncedAt": null,
    "createdAt": "2026-07-30T09:45:00Z",
    "updatedAt": "2026-07-30T09:45:00Z"
  }
}
```

Per UC-13/BR-03 and F1: the Schedule Slot's own `status` is **never** changed as a side effect of this call — it stays `OPEN` (this spec follows the UCS decision, not the DB comment's `FULL` simplification).

**Error Responses**

| Code | Condition |
|---|---|
| 400 | `scheduleSlotId` missing/malformed. |
| 401 | Missing/expired access token. |
| 404 | `scheduleSlotId` does not exist. |
| 409 | The caller already holds a `BOOKED` appointment on this exact slot (`uq_appointments_active_booking` — a unique-constraint conflict, UC-13 AF-01). |
| 422 | The slot's `status` is not `OPEN` (e.g. `CLOSED`). |

**Security:** `bearerAuth: []`. Roles: **Patient only**.

---

## 51. `POST /api/v1/appointments/{id}/cancel`

| Field | Value |
|---|---|
| Summary | Cancel a Booked appointment; requires a mandatory `cancellationReason` field — kept as an action endpoint since a bare status flip cannot express the required-reason gate (UC-14). |
| Auth | **Role-restricted (Patient)** |
| Type | `[ACTION]` |

**Path Parameters**

| Name | Type | Required | Rule |
|---|---|---|---|
| `id` | string (UUID) | Yes | `appointmentId`. Must belong to the caller and currently be `BOOKED`. |

**Request Body — `CancelAppointmentRequest`** (name reused verbatim from UC-14's own Request Fields table)

| Field | Type | Required | Rule |
|---|---|---|---|
| `cancellationReason` | string | Yes | Mandatory — `ck_appointments_cancel_reason` requires it to be non-null exactly when `status = CANCELLED`, and null otherwise (UC-14 BR-02). |

```json
{ "cancellationReason": "Something urgent came up" }
```

**Success Response — `AppointmentResponse`** — `200 OK` (same shape as `#50`'s response, `status: "CANCELLED"`, `cancelledReason` populated). The Schedule Slot's `status` is unchanged (UC-14 BR-03; see F1).

**Error Responses**

| Code | Condition |
|---|---|
| 400 | `cancellationReason` missing/empty (UC-14 AF-02). |
| 401 | Missing/expired access token. |
| 403 | `id` belongs to another patient. |
| 404 | `id` does not exist. |
| 422 | The appointment is not `BOOKED` (already `CANCELLED` — one-way, GB-01). |

**Security:** `bearerAuth: []`. Roles: **Patient only**, own record. **Reschedule (UC-14 AF-01) has no separate endpoint** — the client performs this call followed by a fresh `#50`, per BR-04 ("cancel old + book new, both kept").

---

## 52. `GET /api/v1/appointments/{id}`

| Field | Value |
|---|---|
| Summary | Read one of the signed-in Patient's own appointments (UC-14). |
| Auth | **Role-restricted (Patient)** |
| Type | `[CRUD]` |

**Path Parameters**

| Name | Type | Required | Rule |
|---|---|---|---|
| `id` | string (UUID) | Yes | `appointmentId`. Must belong to the caller. |

**Success Response — `AppointmentResponse`** — `200 OK` (same shape as `#50`'s response).

**Error Responses**

| Code | Condition |
|---|---|
| 401 | Missing/expired access token. |
| 403 | `id` belongs to another patient. |
| 404 | `id` does not exist. |

**Security:** `bearerAuth: []`. Roles: **Patient only**, own record.

---

## 53. `GET /api/v1/appointments`

| Field | Value |
|---|---|
| Summary | List the signed-in Patient's own appointment history (Booked/Cancelled) (UC-13, UC-14). |
| Auth | **Role-restricted (Patient)** |
| Type | `[CRUD]` |

**Query Parameters — `AppointmentSearchCriteria`**

| Field | Type | Required | Rule |
|---|---|---|---|
| `status` | enum `BOOKED\|CANCELLED` | No | Defaults to all. |
| `page` / `pageSize` | integer | No | Default `1` / `20`, max `pageSize` `100` (see F2; also `idx_appointments_patient` exists specifically to support this query). |

**Success Response — `PagedResult<AppointmentSummaryResponse>`** — `200 OK` (same item shape as `#49`'s response, scoped to the caller's own `patientProfileId`).

**Error Responses**

| Code | Condition |
|---|---|
| 400 | Invalid `status` value; `page`/`pageSize` out of range. |
| 401 | Missing/expired access token. |

**Security:** `bearerAuth: []`. Roles: **Patient only**, own record.

---

## 54. N/A — Device-Calendar Sync (UC-16)

| Field | Value |
|---|---|
| Summary | Device-calendar sync is one-way, no read-back: the Mobile App calls the OS Calendar API (API-02) directly. ADSUS_BE exposes **no endpoint** for this integration. |
| Auth | N/A |
| Type | `[N/A]` |

Restated from the API Catalog for completeness — **no schema to design.** `appointments.calendar_synced_at` exists purely as a local bookkeeping timestamp the Mobile App may report back for its own use (not currently exposed through any of `#50`–`#53`'s request/response shapes above, since no UCS Request Field names it as client-writable). If the Mobile App ever needs to persist "I successfully synced this event," that would be a new, currently unapproved endpoint — not invented here.

---

## Module 08 Summary

| # | Method | Endpoint | Auth | Type |
|---|---|---|---|---|
| 46 | POST | `/api/v1/schedule-slots` | Role-restricted (Doctor, Nurse) | CRUD |
| 47 | GET | `/api/v1/schedule-slots` | Role-restricted (Doctor, Nurse, Patient) | CRUD |
| 48 | PATCH | `/api/v1/schedule-slots/{id}` | Role-restricted (Doctor, Nurse) | CRUD |
| 49 | GET | `/api/v1/schedule-slots/{id}/appointments` | Role-restricted (Doctor, Nurse) | CRUD |
| 50 | POST | `/api/v1/appointments` | Role-restricted (Patient) | CRUD |
| 51 | POST | `/api/v1/appointments/{id}/cancel` | Role-restricted (Patient) | ACTION |
| 52 | GET | `/api/v1/appointments/{id}` | Role-restricted (Patient) | CRUD |
| 53 | GET | `/api/v1/appointments` | Role-restricted (Patient) | CRUD |
| 54 | — | N/A (client-device-only) | N/A | N/A |

No endpoint outside the approved catalog was added.

## Shared DTOs Introduced in Module 08

| DTO | Used by | Notes |
|---|---|---|
| `ScheduleSlotResponse` | #46, #47, #48 | `status` enum restricted to `OPEN\|CLOSED` at the API contract layer — see F1. |
| `AppointmentResponse` | #50, #51, #52 | |
| `AppointmentSummaryResponse` | #49, #53 (inside `PagedResult<T>`) | |

**Please resolve F1 before this module is implemented** — it is a real conflict between two authoritative-looking sources (physical schema comment vs. dated UCS decision), not a stylistic nit.

Waiting on your review before continuing to Module 09 (Health Monitoring) and Module 10 (Engagement).
