# ADSUS API Specification — Module 04: Medical Record

| Field            | Value                                                                                                                                                                                                                                                                                             |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Document version | v0.1 (draft) — 2026-07-30                                                                                                                                                                                                                                                                         |
| Role             | Senior API Architect + Detail Designer                                                                                                                                                                                                                                                            |
| Scope            | Endpoints #17–#27 of `ADSUS_API_Catalog_v1.1.md` (approved catalog — **no endpoint added, removed, or renamed here**)                                                                                                                                                                             |
| Sources          | `.ai-context/project_context.md` · `.ai-context/api_design_rule/api_design_rules_v0.2.md` · `Reports_md/Report_3.1_UCS_ADSUS.md` (UC-06, UC-07, UC-08, UC-09, UC-12) · `Documents/02_Requirements/SQL/ADSUS_Physical_Schema_PostgreSQL_v1.sql` (`patient_profiles`, `cases`, `ultrasound_images`) |
| Language         | English                                                                                                                                                                                                                                                                                           |

> Same ERD substitution note as prior modules: field types below come from `ADSUS_Physical_Schema_PostgreSQL_v1.sql`, not the Logical ERD (no column-level detail there by design).

---

## 0. Flags & Open Issues

| #   | Issue                                                                                                                                                                                                                                                                                                 | Where it shows up                                    | Recommendation                                                                                                                                                                                                                                                                        |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| F1  | The physical `cases` table has **two** text columns that look like the same concept — `final_diagnosis` (has a `COMMENT` explaining it as "the doctor's final diagnostic conclusion after reviewing the AI result") and `doctor_conclusion` (no `COMMENT`, purpose undocumented at the schema layer). | `#23 CaseResponse`, `#27` PDF content                | Not resolved here. This spec exposes a single `conclusion` field in the DTO and defers to whichever physical column is authoritative — **confirm with whoever owns the physical schema before implementing**, rather than guessing which of the two columns is canonical.             |
| F2  | `patient_profiles.created_by` has a `CHECK`-equivalent comment requiring the creator to have `role = DOCTOR`, but UC-06's own Allowed Roles say **both Doctor and Nurse** may create/update a Patient Profile.                                                                                        | `#17 CreatePatientProfileRequest`, `createdBy` field | Flagging the contradiction, not silently picking a side — if a Nurse is allowed to create a profile (per UC-06), the DB comment's Doctor-only assumption needs to be revisited (e.g. relax the comment, or record a separate "acting Nurse" attribution field) before implementation. |
| F3  | UC-09's own Request Fields table lists a "Patient code" search field, but neither `users` nor `patient_profiles` has a code/MRN-style column in the physical schema.                                                                                                                                  | `#26 PatientSearchCriteria.code`                     | Inherited gap from the UCS, not introduced here (same pattern as the `category` gap already flagged on `blog-posts` in the API Catalog). Kept in the query schema as optional and effectively a no-op today — raise back to the PRD/UCS owner.                                        |
| F4  | `#27 GET /cases/{id}/report` returns a binary PDF file, which cannot be wrapped in the `{code, message, data}` JSON envelope mandated for every other endpoint (`api_design_rules_v0.2.md` §4).                                                                                                       | `#27`                                                | Documented as a deliberate, narrow exception: success responses are raw `application/pdf` bytes; only the error path (400/403/404/422) still uses the standard JSON envelope, since no file exists yet to stream at that point.                                                       |
| F5  | `ultrasound_images.file_ref` is a raw storage path (`VARCHAR(500)`). Per the PRD's Supabase Storage decision (private buckets, signed URL access — see `Report_4.0_TDS_ADSUS.md` §1), the API almost certainly must never return `file_ref` verbatim.                                                 | `UltrasoundImageResponse.imageUrl`                   | This spec exposes a signed `imageUrl` instead of `fileRef` — a reasonable inference from the known storage decision, **not an explicit UCS field**, flagged so it isn't mistaken for a confirmed contract.                                                                            |

---

## 17. `POST /api/v1/patient-profiles`

| Field   | Value                                                                                                                                |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| Summary | Create a patient's baseline medical profile (gender, medical history, allergies), linked 1–1 to an existing Patient account (UC-06). |
| Auth    | **Role-restricted (Doctor, Nurse)**                                                                                                  |
| Type    | `[CRUD]`                                                                                                                             |

**Request Body — `CreatePatientProfileRequest`**

> Named per this task's `Create{Entity}Request` convention — UC-06 itself names a single merged DTO (`SavePatientProfileRequest`) for both create and update; this spec splits it to match the catalog's separate POST/PUT endpoints.

| Field            | Type                       | Required | Rule                                                                                                                                                                            |
| ---------------- | -------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `patientUserId`  | string (UUID)              | Yes      | Must reference an existing account with `role = PATIENT` (UC-06 BR-01).                                                                                                         |
| `gender`         | enum `FEMALE\|MALE\|OTHER` | No       | Defaults to `FEMALE` at the DB layer (`gender_type` default) — **this default is a schema artifact, not a UCS business rule; the client should always send an explicit value.** |
| `medicalHistory` | string                     | No       | Free text.                                                                                                                                                                      |
| `allergies`      | string                     | No       | Free text.                                                                                                                                                                      |

```json
{
    "patientUserId": "3f2a1c90-6b1e-4b2a-9c3d-8f1e2a7b0d11",
    "gender": "FEMALE",
    "medicalHistory": "Previously had a benign tumor",
    "allergies": "Penicillin"
}
```

`createdBy` is **not** a request field — it is set server-side from the caller's JWT identity (see F2 for the Doctor/Nurse attribution gap).

**Success Response — `PatientProfileResponse`** — `201 Created`

```json
{
    "code": 201,
    "message": "Patient profile created successfully",
    "data": {
        "patientProfileId": "a1b2c3d4-1111-2222-3333-444455556666",
        "patientUserId": "3f2a1c90-6b1e-4b2a-9c3d-8f1e2a7b0d11",
        "fullName": "Nguyen Van A",
        "phone": "0901234567",
        "dateOfBirth": "1990-05-12",
        "gender": "FEMALE",
        "medicalHistory": "Previously had a benign tumor",
        "allergies": "Penicillin",
        "createdBy": "9a1b2c3d-4e5f-6789-0abc-def123456789",
        "createdAt": "2026-07-30T09:00:00Z",
        "updatedAt": "2026-07-30T09:00:00Z"
    }
}
```

`fullName`, `phone`, `dateOfBirth` are read-only fields pulled from the linked `users` row (UC-06 step 2 — "identifying info… from the User entity, read-only").

**Error Responses**

| Code | Condition                                                                                                             |
| ---- | --------------------------------------------------------------------------------------------------------------------- |
| 400  | `patientUserId` missing/malformed; invalid `gender` enum value.                                                       |
| 401  | Missing/expired access token.                                                                                         |
| 403  | Caller authenticated but not `Doctor`/`Nurse`.                                                                        |
| 404  | `patientUserId` does not match any account.                                                                           |
| 409  | The target account already has a Patient Profile (`uq_patient_profiles_user` — 1–1 relationship, UC-06's data model). |
| 422  | `patientUserId` matches an account whose `role` ≠ `PATIENT` (UC-06 BR-01).                                            |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse**.

---

## 18. `PUT /api/v1/patient-profiles/{id}`

| Field   | Value                                        |
| ------- | -------------------------------------------- |
| Summary | Replace/update the baseline profile (UC-06). |
| Auth    | **Role-restricted (Doctor, Nurse)**          |
| Type    | `[CRUD]`                                     |

**Path Parameters**

| Name | Type          | Required | Rule                            |
| ---- | ------------- | -------- | ------------------------------- |
| `id` | string (UUID) | Yes      | `patientProfileId`. Must exist. |

**Request Body — `UpdatePatientProfileRequest`**

| Field            | Type                       | Required | Rule                                                |
| ---------------- | -------------------------- | -------- | --------------------------------------------------- |
| `gender`         | enum `FEMALE\|MALE\|OTHER` | Yes      | Full replace — send the current value if unchanged. |
| `medicalHistory` | string                     | No       |                                                     |
| `allergies`      | string                     | No       |                                                     |

`patientUserId` cannot be changed via this endpoint — the 1–1 link is fixed at creation.

**Success Response — `PatientProfileResponse`** — `200 OK` (same shape as `#17`, reflecting the update).

**Error Responses**

| Code | Condition                                      |
| ---- | ---------------------------------------------- |
| 400  | Invalid `gender` enum value.                   |
| 401  | Missing/expired access token.                  |
| 403  | Caller authenticated but not `Doctor`/`Nurse`. |
| 404  | `id` does not match any Patient Profile.       |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse**.

---

## 19. `GET /api/v1/patient-profiles/{id}`

| Field   | Value                                        |
| ------- | -------------------------------------------- |
| Summary | Read one patient's baseline profile (UC-06). |
| Auth    | **Role-restricted (Doctor, Nurse)**          |
| Type    | `[CRUD]`                                     |

**Path Parameters**

| Name | Type          | Required | Rule                            |
| ---- | ------------- | -------- | ------------------------------- |
| `id` | string (UUID) | Yes      | `patientProfileId`. Must exist. |

**Success Response — `PatientProfileResponse`** — `200 OK` (same shape as `#17`).

**Error Responses**

| Code | Condition                                      |
| ---- | ---------------------------------------------- |
| 401  | Missing/expired access token.                  |
| 403  | Caller authenticated but not `Doctor`/`Nurse`. |
| 404  | `id` does not match any Patient Profile.       |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse**.

---

## 20. `POST /api/v1/cases`

| Field        | Value                                                                                                                                                                                            |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Summary      | Create a new visit (Case) with clinical symptoms and ≥1 ultrasound image (JPEG/PNG, ≤20MB each) in one multipart request; attributes the Case to exactly one responsible Doctor (GB-04) (UC-07). |
| Auth         | **Role-restricted (Doctor, Nurse)**                                                                                                                                                              |
| Type         | `[CRUD]`                                                                                                                                                                                         |
| Content-Type | `multipart/form-data` (file upload — the only multipart endpoint besides `#21`)                                                                                                                  |

**Request Body — `CreateCaseRequest`** (name reused verbatim from UC-07's own Request Fields table)

| Field                 | Type          | Required | Rule                                                                                                                                                            |
| --------------------- | ------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `patientProfileId`    | string (UUID) | Yes      | Must reference an existing Patient Profile.                                                                                                                     |
| `responsibleDoctorId` | string (UUID) | Yes      | Must reference an account with `role = DOCTOR` (GB-04). Required even when the caller is the Doctor themselves — the field is never inferred, per UC-07 step 5. |
| `images`              | array of file | Yes      | At least 1 file. Each: JPEG or PNG (verified by content, not filename), ≤ 20 MB (PRD §6.1).                                                                     |
| `clinicalInfo`        | string        | No       | Visit symptoms (lump location, pain, discharge, etc.) — free text (UC-07 step 3).                                                                               |

**Success Response — `CaseResponse`** — `201 Created` (see `#23` for the full `CaseResponse` shape — a newly created Case has `status = CREATED`, no `aiResults`, no `prescription`).

```json
{
    "code": 201,
    "message": "Case created successfully",
    "data": {
        "caseId": "5c6d7e8f-0000-1111-2222-333344445555",
        "patientProfileId": "a1b2c3d4-1111-2222-3333-444455556666",
        "doctorId": "9a1b2c3d-4e5f-6789-0abc-def123456789",
        "visitDate": "2026-07-30",
        "clinicalInfo": "Left breast pain",
        "status": "CREATED",
        "conclusion": null,
        "ultrasoundImages": [
            {
                "imageId": "6d7e8f90-1111-2222-3333-444455556666",
                "imageUrl": "https://…/signed-url",
                "uploadedAt": "2026-07-30T09:05:00Z",
                "note": null
            }
        ],
        "aiResults": [],
        "prescription": null,
        "createdAt": "2026-07-30T09:05:00Z",
        "updatedAt": "2026-07-30T09:05:00Z"
    }
}
```

**Error Responses**

| Code | Condition                                                                                                                                                                     |
| ---- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 400  | `patientProfileId`/`responsibleDoctorId` missing/malformed.                                                                                                                   |
| 401  | Missing/expired access token.                                                                                                                                                 |
| 403  | Caller authenticated but not `Doctor`/`Nurse`.                                                                                                                                |
| 404  | `patientProfileId` or `responsibleDoctorId` does not exist.                                                                                                                   |
| 422  | No image uploaded (UC-07 AF-02, BR-02); an image fails format/size validation (UC-07 AF-01, BR-01); `responsibleDoctorId` matches an account whose `role` ≠ `DOCTOR` (GB-04). |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse**.

---

## 21. `POST /api/v1/cases/{caseId}/ultrasound-images`

| Field        | Value                                                                                 |
| ------------ | ------------------------------------------------------------------------------------- |
| Summary      | Attach additional ultrasound image(s) to an existing, not-yet-Confirmed Case (UC-07). |
| Auth         | **Role-restricted (Doctor, Nurse)**                                                   |
| Type         | `[CRUD]`                                                                              |
| Content-Type | `multipart/form-data`                                                                 |

**Path Parameters**

| Name     | Type          | Required | Rule                                                                                         |
| -------- | ------------- | -------- | -------------------------------------------------------------------------------------------- |
| `caseId` | string (UUID) | Yes      | Must exist and not be `CONFIRMED` (GB-01 — a finalized Case does not reopen for more input). |

**Request Body — `AddUltrasoundImagesRequest`**

| Field    | Type          | Required | Rule                                                                                                                                                                         |
| -------- | ------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `images` | array of file | Yes      | At least 1 file. Same format/size rule as `#20` (JPEG/PNG, ≤20MB).                                                                                                           |
| `note`   | string        | No       | Applied to every image in this batch — the UCS does not describe a per-image note field on this incremental-add flow; kept as a single batch-level note as a simplification. |

**Success Response — `UltrasoundImageResponse[]`** — `201 Created`

```json
{
    "code": 201,
    "message": "Ultrasound image(s) uploaded successfully",
    "data": [
        {
            "imageId": "7e8f9012-2222-3333-4444-555566667777",
            "caseId": "5c6d7e8f-0000-1111-2222-333344445555",
            "imageUrl": "https://…/signed-url-2",
            "uploadedAt": "2026-07-30T09:10:00Z",
            "note": null
        }
    ]
}
```

**Error Responses**

| Code | Condition                                                                              |
| ---- | -------------------------------------------------------------------------------------- |
| 400  | No file attached.                                                                      |
| 401  | Missing/expired access token.                                                          |
| 403  | Caller authenticated but not `Doctor`/`Nurse`.                                         |
| 404  | `caseId` does not exist.                                                               |
| 422  | An image fails format/size validation; `caseId`'s Case is already `CONFIRMED` (GB-01). |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse**.

---

## 22. `GET /api/v1/cases/{caseId}/ultrasound-images`

| Field   | Value                                                             |
| ------- | ----------------------------------------------------------------- |
| Summary | List the raw ultrasound images uploaded to a Case (UC-07, UC-08). |
| Auth    | **Role-restricted (Doctor, Nurse)**                               |
| Type    | `[CRUD]`                                                          |

**Path Parameters**

| Name     | Type          | Required | Rule        |
| -------- | ------------- | -------- | ----------- |
| `caseId` | string (UUID) | Yes      | Must exist. |

**Success Response — `UltrasoundImageResponse[]`** — `200 OK` (same item shape as `#21`'s response; not paginated — per UC-07's own boundary note, there is no stated limit on images per Case, but in practice this is a small, bounded list per visit, not a growth-unbounded collection requiring `PagedResult<T>`).

| Field        | Type               | Notes                                                                     |
| ------------ | ------------------ | ------------------------------------------------------------------------- |
| `imageId`    | string (UUID)      |                                                                           |
| `caseId`     | string (UUID)      |                                                                           |
| `imageUrl`   | string (URL)       | Signed URL, time-limited — see F5. Never the raw `file_ref` storage path. |
| `uploadedAt` | string (date-time) |                                                                           |
| `note`       | string \| null     |                                                                           |

**Error Responses**

| Code | Condition                                      |
| ---- | ---------------------------------------------- |
| 401  | Missing/expired access token.                  |
| 403  | Caller authenticated but not `Doctor`/`Nurse`. |
| 404  | `caseId` does not exist.                       |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse**. Never exposed to `Patient` directly — Patient views summarized content only via `#23`'s Patient-facing view, which omits raw images entirely (GB-05).

---

## 23. `GET /api/v1/cases/{id}`

| Field   | Value                                                                                                                                                                                                           |
| ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Full record for Doctor/Nurse (raw images, AI Findings, all statuses, progression); the same path returns only the doctor-confirmed conclusion + prescription when called by the owning Patient (GB-05) (UC-08). |
| Auth    | **Role-restricted (Doctor, Nurse, Patient-own)**                                                                                                                                                                |
| Type    | `[CRUD]`                                                                                                                                                                                                        |

**Path Parameters**

| Name | Type          | Required | Rule                                                                                                                                                                                         |
| ---- | ------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `id` | string (UUID) | Yes      | `caseId`. Must exist. For a `Patient` caller, must belong to that Patient's own Patient Profile, and the Case's `status` must be `CONFIRMED` — otherwise treated as not found (UC-08 AF-01). |

**Success Response — `CaseResponse`** — `200 OK`. **Field visibility differs by caller role**, all under one DTO shape (fields simply absent/`null` when not visible to the caller, per `sensitive_data_rules` — a field the caller isn't entitled to is never declared, not just filtered):

| Field                     | Type                                                                         | Visible to Doctor/Nurse | Visible to Patient (own, Confirmed only)                                                                                                                       |
| ------------------------- | ---------------------------------------------------------------------------- | ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `caseId`                  | string (UUID)                                                                | ✅                      | ✅                                                                                                                                                             |
| `patientProfileId`        | string (UUID)                                                                | ✅                      | ❌ (redundant — Patient already knows it's their own)                                                                                                          |
| `doctorId`                | string (UUID)                                                                | ✅                      | ✅ (to show "Dr. X" on the record)                                                                                                                             |
| `visitDate`               | string (date)                                                                | ✅                      | ✅                                                                                                                                                             |
| `clinicalInfo`            | string \| null                                                               | ✅                      | ❌ (internal working notes, not part of the confirmed conclusion)                                                                                              |
| `status`                  | enum `CREATED\|ANALYZED\|CONFIRMED`                                          | ✅                      | ✅ (always `CONFIRMED` for this caller)                                                                                                                        |
| `conclusion`              | string \| null                                                               | ✅                      | ✅ — see F1 for which physical column this maps to                                                                                                             |
| `patientProfile`          | `PatientProfileResponse` \| null                                             | ✅                      | ❌                                                                                                                                                             |
| `ultrasoundImages`        | `UltrasoundImageResponse[]`                                                  | ✅                      | ❌ (GB-05 — raw images never shown to Patient)                                                                                                                 |
| `aiResults`               | array (minimal `AiResultSummary`: `aiResultId`, `status`, `confidenceScore`) | ✅                      | ❌ (GB-05 — Patient never sees raw AI output, only the human `conclusion` text). **Finalized in the Module 05 spec — shown here only as the embedding shape.** |
| `prescription`            | object \| null (minimal `PrescriptionSummary`: `prescriptionId`, `status`)   | ✅                      | ✅ (Patient may see their own prescription exists — full detail via Module 07's own endpoints)                                                                 |
| `createdAt` / `updatedAt` | string (date-time)                                                           | ✅                      | ❌                                                                                                                                                             |

**Error Responses**

| Code | Condition                                                                                                                                                                                                                                                     |
| ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 401  | Missing/expired access token.                                                                                                                                                                                                                                 |
| 403  | Caller is `Doctor`/`Nurse` but the Case is outside their permitted scope (if any such restriction is later added — not specified by the current UCS, kept here only as the standard slot for it).                                                             |
| 404  | `id` does not exist; or caller is `Patient` and either the Case does not belong to them, or its `status` is not `CONFIRMED` (UC-08 AF-01 — treated as not found, never a distinct "forbidden" signal, to avoid leaking that a not-yet-Confirmed Case exists). |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse, Patient (own record, Confirmed only)**.

---

## 24. `GET /api/v1/cases?patientProfileId={id}`

| Field   | Value                                                                                                                                                |
| ------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | List a given patient's visits for Doctor/Nurse (Web SCR-12); `patientProfileId` is required — Patient no longer calls this path (see `#25`) (UC-08). |
| Auth    | **Role-restricted (Doctor, Nurse)**                                                                                                                  |
| Type    | `[CRUD]`                                                                                                                                             |

**Query Parameters — `CaseSearchCriteria`**

| Field               | Type                                | Required | Rule                                               |
| ------------------- | ----------------------------------- | -------- | -------------------------------------------------- |
| `patientProfileId`  | string (UUID)                       | Yes      | Must reference an existing Patient Profile.        |
| `status`            | enum `CREATED\|ANALYZED\|CONFIRMED` | No       | Filters by status.                                 |
| `sortBy`            | string                              | No       | `visitDate` (default) — per §7.3 of the API rules. |
| `sortOrder`         | `asc\|desc`                         | No       | Default `desc` (most recent visit first).          |
| `page` / `pageSize` | integer                             | No       | Default `1` / `20`, max `pageSize` `100`.          |

**Success Response — `PagedResult<CaseSummaryResponse>`** — `200 OK`

```json
{
    "code": 200,
    "message": "Cases retrieved successfully",
    "data": {
        "items": [
            {
                "caseId": "5c6d7e8f-0000-1111-2222-333344445555",
                "visitDate": "2026-07-30",
                "status": "CREATED",
                "doctorId": "9a1b2c3d-4e5f-6789-0abc-def123456789"
            }
        ],
        "page": 1,
        "pageSize": 20,
        "totalItems": 1,
        "totalPages": 1
    }
}
```

`CaseSummaryResponse` intentionally excludes `clinicalInfo`, `ultrasoundImages`, `aiResults`, `prescription` — those belong to the single-item `#23` detail view, not the list.

**Error Responses**

| Code | Condition                                                                                                          |
| ---- | ------------------------------------------------------------------------------------------------------------------ |
| 400  | `patientProfileId` missing/malformed; invalid `status`/`sortBy`/`sortOrder` value; `page`/`pageSize` out of range. |
| 401  | Missing/expired access token.                                                                                      |
| 403  | Caller authenticated but not `Doctor`/`Nurse`.                                                                     |
| 404  | `patientProfileId` does not exist.                                                                                 |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse**.

---

## 25. `GET /api/v1/cases/me`

| Field   | Value                                                                                           |
| ------- | ----------------------------------------------------------------------------------------------- |
| Summary | List the signed-in Patient's own visits, auto-scoped to Confirmed-only (Mobile SCR-13) (UC-08). |
| Auth    | **Role-restricted (Patient)**                                                                   |
| Type    | `[CRUD]` — `[NEW in v1.1]`                                                                      |

**Query Parameters — `CaseSearchCriteria` (self-scoped subset)**

| Field               | Type    | Required | Rule                                      |
| ------------------- | ------- | -------- | ----------------------------------------- |
| `page` / `pageSize` | integer | No       | Default `1` / `20`, max `pageSize` `100`. |

No `patientProfileId`/`status` params — the Patient Profile is resolved from the caller's own JWT identity, and `status` is always forced to `CONFIRMED` server-side (never client-controlled, per GB-05).

**Success Response — `PagedResult<CaseSummaryResponse>`** — `200 OK` (same item shape as `#24`, always `status: "CONFIRMED"`).

**Error Responses**

| Code | Condition                                                                                          |
| ---- | -------------------------------------------------------------------------------------------------- |
| 400  | `page`/`pageSize` out of range.                                                                    |
| 401  | Missing/expired access token.                                                                      |
| 404  | Caller has no Patient Profile yet (edge case — an account provisioned but never seen by a Doctor). |

**Security:** `bearerAuth: []`. Roles: **Patient only**, own record.

---

## 26. `GET /api/v1/patients`

| Field   | Value                                                                                                                                                                |
| ------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Search/filter the clinical patient list by name, phone, or code, with a visit-status filter — distinct DTO from Admin's `/users` (no account/status fields) (UC-09). |
| Auth    | **Role-restricted (Doctor, Nurse)**                                                                                                                                  |
| Type    | `[CRUD]`                                                                                                                                                             |

**Query Parameters — `PatientSearchCriteria`**

| Field               | Type                           | Required | Rule                                                                                                                                                            |
| ------------------- | ------------------------------ | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `search`            | string                         | No       | Case-insensitive substring match on `fullName` or `phone` (UC-09 BR-01).                                                                                        |
| `code`              | string                         | No       | **Not implementable against the current schema — see F3.** Accepted but currently a no-op.                                                                      |
| `visitStatus`       | enum `All\|Pending\|Confirmed` | No       | Defaults to `All` (UC-09 Request Fields). "Pending"/"Confirmed" refer to the patient's most recent Case status, collapsing `CREATED`/`ANALYZED` into "Pending". |
| `page` / `pageSize` | integer                        | No       | Default `1` / `20`, max `pageSize` `100`.                                                                                                                       |

**Success Response — `PagedResult<PatientSummaryResponse>`** — `200 OK`

```json
{
    "code": 200,
    "message": "Patients retrieved successfully",
    "data": {
        "items": [
            {
                "patientProfileId": "a1b2c3d4-1111-2222-3333-444455556666",
                "patientUserId": "3f2a1c90-6b1e-4b2a-9c3d-8f1e2a7b0d11",
                "fullName": "Nguyen Van A",
                "phone": "0901234567",
                "latestVisitDate": "2026-07-30",
                "latestVisitStatus": "CREATED"
            }
        ],
        "page": 1,
        "pageSize": 20,
        "totalItems": 1,
        "totalPages": 1
    }
}
```

`PatientSummaryResponse` never includes `email`, account `status`, or `mustChangePassword` — those are Module 02's Admin-facing account fields, out of scope for this clinical view (per the catalog's own note distinguishing `/patients` from `/users`).

**Error Responses**

| Code | Condition                                                    |
| ---- | ------------------------------------------------------------ |
| 400  | Invalid `visitStatus` value; `page`/`pageSize` out of range. |
| 401  | Missing/expired access token.                                |
| 403  | Caller authenticated but not `Doctor`/`Nurse`.               |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse**.

---

## 27. `GET /api/v1/cases/{id}/report`

| Field   | Value                                                                                                                                 |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Generate and return the PDF visit report — Confirmed conclusion + prescription only, never raw AI confidence/unreviewed data (UC-12). |
| Auth    | **Role-restricted (Doctor, Nurse, Patient-own)**                                                                                      |
| Type    | `[ACTION]`                                                                                                                            |

**Path Parameters**

| Name | Type          | Required | Rule                                                                                                                     |
| ---- | ------------- | -------- | ------------------------------------------------------------------------------------------------------------------------ |
| `id` | string (UUID) | Yes      | `caseId`. Must exist and be `CONFIRMED` (UC-12 BR-01). For a `Patient` caller, must belong to their own Patient Profile. |

**Success Response** — `200 OK` — **binary file response, not the JSON envelope** (see F4).

| Header                | Value                                              |
| --------------------- | -------------------------------------------------- |
| `Content-Type`        | `application/pdf`                                  |
| `Content-Disposition` | `attachment; filename="visit-report-{caseId}.pdf"` |

Body: raw PDF bytes containing exactly the Doctor's Confirmed conclusion and the corresponding prescription (UC-12 BR-01) — no raw AI confidence score, no unreviewed data, for either Patient or Doctor/Nurse callers.

**Error Responses** — these still use the standard `{code, message, data}` JSON envelope, since no file exists to stream yet at the point of failure.

| Code | Condition                                                                                                                                       |
| ---- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| 401  | Missing/expired access token.                                                                                                                   |
| 403  | Caller is `Doctor`/`Nurse` without view permission on this Case (slot reserved, not specified by the current UCS — see the same note as `#23`). |
| 404  | `id` does not exist; or belongs to another Patient (for a `Patient` caller).                                                                    |
| 422  | The Case is not yet `CONFIRMED` (UC-12 AF-01).                                                                                                  |

**Security:** `bearerAuth: []`. Roles: **Doctor, Nurse, Patient (own, Confirmed only)**.

---

## Module 04 Summary

| #   | Method | Endpoint                                   | Auth                                         | Type   |
| --- | ------ | ------------------------------------------ | -------------------------------------------- | ------ |
| 17  | POST   | `/api/v1/patient-profiles`                 | Role-restricted (Doctor, Nurse)              | CRUD   |
| 18  | PUT    | `/api/v1/patient-profiles/{id}`            | Role-restricted (Doctor, Nurse)              | CRUD   |
| 19  | GET    | `/api/v1/patient-profiles/{id}`            | Role-restricted (Doctor, Nurse)              | CRUD   |
| 20  | POST   | `/api/v1/cases`                            | Role-restricted (Doctor, Nurse)              | CRUD   |
| 21  | POST   | `/api/v1/cases/{caseId}/ultrasound-images` | Role-restricted (Doctor, Nurse)              | CRUD   |
| 22  | GET    | `/api/v1/cases/{caseId}/ultrasound-images` | Role-restricted (Doctor, Nurse)              | CRUD   |
| 23  | GET    | `/api/v1/cases/{id}`                       | Role-restricted (Doctor, Nurse, Patient-own) | CRUD   |
| 24  | GET    | `/api/v1/cases?patientProfileId=`          | Role-restricted (Doctor, Nurse)              | CRUD   |
| 25  | GET    | `/api/v1/cases/me`                         | Role-restricted (Patient)                    | CRUD   |
| 26  | GET    | `/api/v1/patients`                         | Role-restricted (Doctor, Nurse)              | CRUD   |
| 27  | GET    | `/api/v1/cases/{id}/report`                | Role-restricted (Doctor, Nurse, Patient-own) | ACTION |

No endpoint outside the approved catalog was added.

## Shared DTOs Introduced in Module 04

| DTO                                      | Used by                            | Notes                                                                                                                                                                            |
| ---------------------------------------- | ---------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PatientProfileResponse`                 | #17, #18, #19, embedded in #23     | Includes read-only `fullName`/`phone`/`dateOfBirth` pulled from `users`.                                                                                                         |
| `UltrasoundImageResponse`                | #21, #22, embedded in #23/#20      | `imageUrl` is a signed URL, never the raw `file_ref` (see F5).                                                                                                                   |
| `CaseResponse`                           | #20 (create result), #23           | Field visibility varies by caller role — see the table under `#23`.                                                                                                              |
| `CaseSummaryResponse`                    | #24, #25 (inside `PagedResult<T>`) | Lightweight list shape.                                                                                                                                                          |
| `PatientSummaryResponse`                 | #26 (inside `PagedResult<T>`)      | Clinical view — no account/admin fields.                                                                                                                                         |
| `AiResultSummary`, `PrescriptionSummary` | Embedded (read-only) in `#23`      | Minimal forward-referenced shapes — will be finalized when Module 05 (AI Diagnosis) and Module 07 (Prescription & Adherence) specs are written; not to be treated as final here. |

Waiting on your review before continuing to Module 05 (AI Diagnosis Core) onward.
