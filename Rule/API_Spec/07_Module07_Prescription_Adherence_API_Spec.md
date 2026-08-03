# ADSUS API Specification — Module 07: Prescription & Adherence

| Field | Value |
|---|---|
| Document version | v0.1 (draft) — 2026-07-30 |
| Role | Senior API Architect + Detail Designer |
| Scope | Endpoints #37–#45 of `ADSUS_API_Catalog_v1.1.md` (approved catalog — **no endpoint added, removed, or renamed here**) |
| Sources | `.ai-context/project_context.md` · `.ai-context/api_design_rule/api_design_rules_v0.2.md` · `Reports_md/Report_3.1_UCS_ADSUS.md` (UC-11, UC-17, UC-18) · `Documents/02_Requirements/SQL/ADSUS_Physical_Schema_PostgreSQL_v1.sql` (`prescriptions`, `medicines`, `prescription_items`, `medication_intake_logs`, `patient_reminder_preferences`) |
| Language | English |

> Same ERD substitution note as prior modules: field types below come from `ADSUS_Physical_Schema_PostgreSQL_v1.sql`, not the Logical ERD.

---

## 0. Flags & Open Issues

| # | Issue | Where it shows up | Recommendation |
|---|---|---|---|
| F1 | **Naming collision, not a functional bug — worth flagging so it isn't misread while implementing.** `prescription_items` has an array column literally named `schedule_slots` (the intake time-of-day, e.g. `{MORNING, EVENING}`) — the exact same name as the unrelated `schedule_slots` **table** in Module 08 (a Doctor's clinic appointment slots). | `CreatePrescriptionRequest.items[].intakeSlots` | This spec names the DTO field `intakeSlots` (not `scheduleSlots`) precisely to avoid colliding with Module 08's `ScheduleSlotResponse` naming in generated client code/OpenAPI schemas. Confirm the underlying EF Core property name is disambiguated similarly (e.g. `IntakeSlots`) when implementing. |
| F2 | `#39`/`#41`/`#42` list endpoints are **paginated** (`PagedResult<T>`), unlike the unpaginated judgment calls made for small bounded lists in Modules 04–06. | `#39`, `#41`, `#42` | Not a new judgment call — `api_design_rules_v0.2.md` §7.1 **explicitly names** `medication_intake_logs` (and, by the same per-patient-growth logic, prescriptions) in its "no unbounded `GetAll()`" rule. Pagination here is a direct rule citation, not a design choice. |
| F3 | `#42`'s query parameters (`status`, `date`) are **not** listed in UC-17's own Request Fields table (which only defines `ConfirmIntakeRequest` for the PATCH step) — the "list my due doses" read is implied by SCR-19's existence but never spec'd as a request shape. | `#42 MedicationIntakeLogSearchCriteria` | Self-derived, flagged rather than presented as a UCS-confirmed contract — confirm at FDS. |

---

## 37. `POST /api/v1/prescriptions`

| Field | Value |
|---|---|
| Summary | Prescribe medication for a Confirmed Case; ≥1 item, each with ≥1 intake slot (Morning/Noon/Evening) (UC-18). |
| Auth | **Role-restricted (Doctor)** |
| Type | `[CRUD]` |

**Request Body — `PrescribeMedicationRequest`** (name reused verbatim from UC-18's own Request Fields table)

| Field | Type | Required | Rule |
|---|---|---|---|
| `caseId` | string (UUID) | Yes | Must reference a Case at `status = CONFIRMED` (UC-18 BR-04). |
| `generalNote` | string | No | Maps to the `general_note` column — the Prescription's own header-level note, distinct from each item's `instructions`. |
| `items` | array of object | Yes | At least 1 item. Each: |
| `items[].medicineName` | string | Yes | Looked up (case-insensitive) against the `Medicine` catalog (`uq_medicines_name_lower`); an unmatched name is auto-added as a new `Medicine` record (UC-18 BR-01). |
| `items[].dosage` | string | Yes | e.g. `"500mg"`. Max 100 chars (`VARCHAR(100)`). |
| `items[].intakeSlots` | array of enum `MORNING\|NOON\|EVENING` | Yes | At least 1 (`ck_prescription_items_schedule`) — see F1 for the DTO naming rationale. |
| `items[].durationDays` | integer | Yes | Positive integer (`ck_prescription_items_duration`), e.g. `7`. |
| `items[].instructions` | string | No | e.g. `"Take after meals"`. |

```json
{
  "caseId": "5c6d7e8f-0000-1111-2222-333344445555",
  "generalNote": "Review in 2 weeks if symptoms persist",
  "items": [
    { "medicineName": "Paracetamol", "dosage": "500mg", "intakeSlots": ["MORNING", "EVENING"], "durationDays": 7, "instructions": "Take after meals" }
  ]
}
```

**Success Response — `PrescriptionResponse`** — `201 Created`

```json
{
  "code": 201,
  "message": "Prescription created successfully",
  "data": {
    "prescriptionId": "b2c3d4e5-1111-2222-3333-444455556666",
    "caseId": "5c6d7e8f-0000-1111-2222-333344445555",
    "doctorId": "9a1b2c3d-4e5f-6789-0abc-def123456789",
    "prescribedDate": "2026-07-30",
    "generalNote": "Review in 2 weeks if symptoms persist",
    "status": "ACTIVE",
    "items": [
      {
        "prescriptionItemId": "c3d4e5f6-2222-3333-4444-555566667777",
        "medicineId": "d4e5f6a7-3333-4444-5555-666677778888",
        "medicineName": "Paracetamol",
        "dosage": "500mg",
        "intakeSlots": ["MORNING", "EVENING"],
        "durationDays": 7,
        "startDate": "2026-07-30",
        "instructions": "Take after meals"
      }
    ],
    "createdAt": "2026-07-30T09:30:00Z",
    "updatedAt": "2026-07-30T09:30:00Z"
  }
}
```

**Error Responses**

| Code | Condition |
|---|---|
| 400 | `caseId` missing; an item missing `dosage`/`durationDays`. |
| 401 | Missing/expired access token. |
| 403 | Caller authenticated but not `Doctor`. |
| 404 | `caseId` does not exist. |
| 422 | `caseId`'s Case is not `CONFIRMED` (UC-18 AF-02, BR-04); an item has no `intakeSlots` (UC-18 AF-01, BR-02); `durationDays` ≤ 0. |

**Security:** `bearerAuth: []`. Roles: **Doctor only**.

---

## 38. `GET /api/v1/medicines`

| Field | Value |
|---|---|
| Summary | Search/autocomplete the shared Medicine catalog while prescribing; an unmatched name is auto-added as a new Medicine record (UC-18). |
| Auth | **Role-restricted (Doctor)** |
| Type | `[CRUD]` |

**Query Parameters — `MedicineSearchCriteria`**

| Field | Type | Required | Rule |
|---|---|---|---|
| `search` | string | No | Case-insensitive substring match on `name`. Empty/omitted returns the most-recently-used medicines (self-derived default for a typeahead UX, not a UCS-confirmed behavior). |

**Success Response — `MedicineResponse[]`** — `200 OK` (unpaginated — typeahead result, capped at a reasonable server-side limit, e.g. top 20 matches; not a UCS-specified number).

```json
{ "code": 200, "message": "Medicines retrieved successfully", "data": [ { "medicineId": "d4e5f6a7-3333-4444-5555-666677778888", "name": "Paracetamol" } ] }
```

**Error Responses**

| Code | Condition |
|---|---|
| 401 | Missing/expired access token. |
| 403 | Caller authenticated but not `Doctor`. |

**Security:** `bearerAuth: []`. Roles: **Doctor only**.

---

## 39. `GET /api/v1/patient-profiles/{id}/prescriptions`

| Field | Value |
|---|---|
| Summary | List a patient's prescription history (UC-11). |
| Auth | **Role-restricted (Doctor, Nurse, Patient-own)** |
| Type | `[CRUD]` |

**Path Parameters**

| Name | Type | Required | Rule |
|---|---|---|---|
| `id` | string (UUID) | Yes | `patientProfileId`. For a `Patient` caller, must be their own (UC-11 BR-02). |

**Query Parameters — `PrescriptionSearchCriteria`**

| Field | Type | Required | Rule |
|---|---|---|---|
| `status` | enum `ACTIVE\|COMPLETED` | No | Defaults to all (UC-11 Request Fields). |
| `from` / `to` | string (date) | No | Filters by `prescribed_date` range. |
| `page` / `pageSize` | integer | No | Default `1` / `20`, max `pageSize` `100` (see F2). |

**Success Response — `PagedResult<PrescriptionSummaryResponse>`** — `200 OK`

```json
{
  "code": 200,
  "message": "Prescriptions retrieved successfully",
  "data": {
    "items": [
      { "prescriptionId": "b2c3d4e5-1111-2222-3333-444455556666", "prescribedDate": "2026-07-30", "doctorId": "9a1b2c3d-4e5f-6789-0abc-def123456789", "status": "ACTIVE", "adherenceRatePercent": 80.0 }
    ],
    "page": 1, "pageSize": 20, "totalItems": 1, "totalPages": 1
  }
}
```

`adherenceRatePercent` uses the same self-derived formula as UC-11 BR-01: `(Taken doses / total doses due so far) × 100` — **not an explicit PRD formula**, flagged in UC-11 itself.

**Error Responses**

| Code | Condition |
|---|---|
| 400 | Invalid `status` value; `from > to`; `page`/`pageSize` out of range. |
| 401 | Missing/expired access token. |
| 403 | Caller is `Patient` requesting another patient's `id`. |
| 404 | `id` does not exist. |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse, Patient (own record only)**.

---

## 40. `GET /api/v1/prescriptions/{id}`

| Field | Value |
|---|---|
| Summary | Read one prescription's detail with its items (drug, dosage, intake slots, duration) (UC-11). |
| Auth | **Role-restricted (Doctor, Nurse, Patient-own)** |
| Type | `[CRUD]` |

**Path Parameters**

| Name | Type | Required | Rule |
|---|---|---|---|
| `id` | string (UUID) | Yes | `prescriptionId`. For a `Patient` caller, must belong to their own Case history. |

**Success Response — `PrescriptionResponse`** — `200 OK` (same shape as `#37`'s response, plus `adherenceRatePercent` — see `#39`).

**Error Responses**

| Code | Condition |
|---|---|
| 401 | Missing/expired access token. |
| 403 | Caller is `Patient` requesting a prescription that isn't theirs. |
| 404 | `id` does not exist. |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse, Patient (own record only)**.

---

## 41. `GET /api/v1/prescriptions/{id}/intake-logs`

| Field | Value |
|---|---|
| Summary | Chronological dose-by-dose intake timeline plus the computed Adherence Rate % (UC-11). |
| Auth | **Role-restricted (Doctor, Nurse, Patient-own)** |
| Type | `[CRUD]` |

**Path Parameters**

| Name | Type | Required | Rule |
|---|---|---|---|
| `id` | string (UUID) | Yes | `prescriptionId`. |

**Query Parameters — `IntakeLogSearchCriteria`**

| Field | Type | Required | Rule |
|---|---|---|---|
| `page` / `pageSize` | integer | No | Default `1` / `20`, max `pageSize` `100` — **mandatory here per §7.1's explicit naming of `medication_intake_logs`** (see F2). |

**Success Response — `PagedResult<IntakeLogResponse>`** — `200 OK`, ordered by `scheduled_time ASC`.

```json
{
  "code": 200,
  "message": "Intake logs retrieved successfully",
  "data": {
    "items": [
      { "intakeId": "e5f6a7b8-4444-5555-6666-777788889999", "prescriptionItemId": "c3d4e5f6-2222-3333-4444-555566667777", "medicineName": "Paracetamol", "scheduledTime": "2026-07-30T07:00:00+07:00", "confirmedAt": "2026-07-30T07:05:00+07:00", "status": "TAKEN" }
    ],
    "page": 1, "pageSize": 20, "totalItems": 14, "totalPages": 1
  }
}
```

**Error Responses**

| Code | Condition |
|---|---|
| 400 | `page`/`pageSize` out of range. |
| 401 | Missing/expired access token. |
| 403 | Caller is `Patient` requesting a prescription that isn't theirs. |
| 404 | `id` does not exist. |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse, Patient (own record only)**.

---

## 42. `GET /api/v1/medication-intake-logs`

| Field | Value |
|---|---|
| Summary | List the signed-in Patient's due/pending doses (UC-17). |
| Auth | **Role-restricted (Patient)** |
| Type | `[CRUD]` |

**Query Parameters — `MedicationIntakeLogSearchCriteria`** (self-derived shape — see F3)

| Field | Type | Required | Rule |
|---|---|---|---|
| `status` | enum `PENDING\|TAKEN` | No | Defaults to `PENDING` (the mobile screen's primary use case — "today's due doses"). |
| `page` / `pageSize` | integer | No | Default `1` / `20`, max `pageSize` `100` (§7.1 — see F2). |

**Success Response — `PagedResult<IntakeLogResponse>`** — `200 OK` (same item shape as `#41`, scoped server-side to the caller's own prescriptions via their `patientProfileId`).

**Error Responses**

| Code | Condition |
|---|---|
| 400 | Invalid `status` value; `page`/`pageSize` out of range. |
| 401 | Missing/expired access token. |

**Security:** `bearerAuth: []`. Roles: **Patient only**, own record.

---

## 43. `PATCH /api/v1/medication-intake-logs/{id}`

| Field | Value |
|---|---|
| Summary | Confirm a dose taken via `{"status":"TAKEN"}`; auto-completes the parent Prescription once every scheduled dose is Taken (UC-17). |
| Auth | **Role-restricted (Patient)** |
| Type | `[CRUD]` |

**Path Parameters**

| Name | Type | Required | Rule |
|---|---|---|---|
| `id` | string (UUID) | Yes | `intakeId`. Must belong to the caller's own prescription, and currently be `PENDING` (`ck_medication_intake_logs_taken` — `TAKEN` always carries a `confirmed_at`). |

**Request Body — `ConfirmIntakeRequest`** (name reused verbatim from UC-17's own Request Fields table)

| Field | Type | Required | Rule |
|---|---|---|---|
| `status` | enum `TAKEN` | Yes | Only valid target value — there is no client-settable "Missed" status (UC-17 BR-02, one-way `Pending → Taken`). Expressed as a `status` field per this catalog's `PATCH + status` convention rather than UC-17's looser "Confirmation timestamp"-only framing. |
| `confirmedAt` | string (date-time) | No | Defaults to the server's time when the request is received (UC-17 Request Fields). |

```json
{ "status": "TAKEN" }
```

**Success Response — `IntakeLogResponse`** — `200 OK`. If this was the Prescription's last remaining `PENDING` dose, the parent `Prescription.status` also moves to `COMPLETED` in the same transaction (UC-17 BR-03) — not reflected in this endpoint's own response body (which describes only the `IntakeLog`); check `#40` for the Prescription's updated status.

**Error Responses**

| Code | Condition |
|---|---|
| 400 | `status` missing or not `TAKEN`. |
| 401 | Missing/expired access token. |
| 403 | `id` belongs to another patient's prescription. |
| 404 | `id` does not exist. |
| 422 | The intake log is not `PENDING` (already `TAKEN` — one-way, UC-17 BR-02). |

**Security:** `bearerAuth: []`. Roles: **Patient only**, own record.

---

## 44. `GET /api/v1/reminder-preferences`

| Field | Value |
|---|---|
| Summary | Read the signed-in Patient's custom reminder time for every intake slot (UC-17). |
| Auth | **Role-restricted (Patient)** |
| Type | `[CRUD]` |

**Success Response — `ReminderPreferenceResponse[]`** — `200 OK` (unpaginated — bounded to at most 3 rows, one per slot, per `uq_patient_reminder_preferences_slot`).

```json
{ "code": 200, "message": "Reminder preferences retrieved successfully", "data": [ { "slot": "MORNING", "customTime": "08:00:00" } ] }
```

A slot with **no** custom row is simply absent from this array — the system default (Morning 07:00 / Noon 12:00 / Evening 20:00) is applied at JOB-01 runtime, not materialized as a row here (per `patient_reminder_preferences`'s own table comment).

**Error Responses**

| Code | Condition |
|---|---|
| 401 | Missing/expired access token. |

**Security:** `bearerAuth: []`. Roles: **Patient only**, own record.

---

## 45. `PUT /api/v1/reminder-preferences/{slot}`

| Field | Value |
|---|---|
| Summary | Upsert the custom reminder time for one intake slot, identified in the path (UC-17). |
| Auth | **Role-restricted (Patient)** |
| Type | `[CRUD]` — `[FIXED in v1.1]` (path now carries the resource identifier — see the catalog's v1.1 changelog) |

**Path Parameters**

| Name | Type | Required | Rule |
|---|---|---|---|
| `slot` | enum `morning\|noon\|evening` (lowercase path segment, mapped to `MORNING\|NOON\|EVENING` internally) | Yes | One of the 3 fixed slots. |

**Request Body — `UpsertReminderPreferenceRequest`**

| Field | Type | Required | Rule |
|---|---|---|---|
| `customTime` | string (`HH:mm`, 24h) | Yes | Full replace of this slot's custom time — applies to every future dose in this slot, not tied to one prescription (UC-17 BR-04). |

```json
{ "customTime": "08:30" }
```

**Success Response — `ReminderPreferenceResponse`** — `200 OK`

```json
{ "code": 200, "message": "Reminder preference updated successfully", "data": { "slot": "MORNING", "customTime": "08:30:00" } }
```

**Error Responses**

| Code | Condition |
|---|---|
| 400 | `customTime` missing or not a valid `HH:mm` time. |
| 401 | Missing/expired access token. |

**Security:** `bearerAuth: []`. Roles: **Patient only**, own record.

---

## Module 07 Summary

| # | Method | Endpoint | Auth | Type |
|---|---|---|---|---|
| 37 | POST | `/api/v1/prescriptions` | Role-restricted (Doctor) | CRUD |
| 38 | GET | `/api/v1/medicines` | Role-restricted (Doctor) | CRUD |
| 39 | GET | `/api/v1/patient-profiles/{id}/prescriptions` | Role-restricted (Doctor, Nurse, Patient-own) | CRUD |
| 40 | GET | `/api/v1/prescriptions/{id}` | Role-restricted (Doctor, Nurse, Patient-own) | CRUD |
| 41 | GET | `/api/v1/prescriptions/{id}/intake-logs` | Role-restricted (Doctor, Nurse, Patient-own) | CRUD |
| 42 | GET | `/api/v1/medication-intake-logs` | Role-restricted (Patient) | CRUD |
| 43 | PATCH | `/api/v1/medication-intake-logs/{id}` | Role-restricted (Patient) | CRUD |
| 44 | GET | `/api/v1/reminder-preferences` | Role-restricted (Patient) | CRUD |
| 45 | PUT | `/api/v1/reminder-preferences/{slot}` | Role-restricted (Patient) | CRUD |

No endpoint outside the approved catalog was added.

## Shared DTOs Introduced in Module 07

| DTO | Used by | Notes |
|---|---|---|
| `PrescriptionResponse` | #37, #40; embedded (minimal) in Module 04's `CaseResponse.prescription` | Full shape includes `items: PrescriptionItemResponse[]`. |
| `PrescriptionSummaryResponse` | #39 (inside `PagedResult<T>`) | Adds `adherenceRatePercent`, omits `items`. |
| `IntakeLogResponse` | #41, #42, #43 | |
| `ReminderPreferenceResponse` | #44, #45 | |
| `MedicineResponse` | #38 | |

Waiting on your review before continuing to Module 08 — which follows below in the same batch.
