# ADSUS API Specification — Module 05: AI Diagnosis (Core)

| Field            | Value                                                                                                                                                                                                                                            |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Document version | v0.1 (draft) — 2026-07-30                                                                                                                                                                                                                        |
| Role             | Senior API Architect + Detail Designer                                                                                                                                                                                                           |
| Scope            | Endpoints #28–#32 of `ADSUS_API_Catalog_v1.1.md` (approved catalog — **no endpoint added, removed, or renamed here**)                                                                                                                            |
| Sources          | `.ai-context/project_context.md` · `.ai-context/api_design_rule/api_design_rules_v0.2.md` · `Reports_md/Report_3.1_UCS_ADSUS.md` (UC-19) · `Documents/02_Requirements/SQL/ADSUS_Physical_Schema_PostgreSQL_v1.sql` (`ai_results`, `ai_findings`) |
| Language         | English                                                                                                                                                                                                                                          |

> Same ERD substitution note as prior modules: field types below come from `ADSUS_Physical_Schema_PostgreSQL_v1.sql`, not the Logical ERD.

---

## 0. Flags & Open Issues

| #   | Issue                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               | Where it shows up            | Recommendation                                                                                                                                                                                                                                                                                                                                                          |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| F1  | UC-19 step 4 describes "the AI Result's overall prediction and confidence score," but `ai_results` has **no** overall-confidence/overall-prediction column — only `ai_findings.confidence` exists, one value **per finding**, not per result.                                                                                                                                                                                                                                                                                       | `AiResultResponse`           | This spec exposes only per-finding `confidence`/`lesionType` (what the DB actually has) and does **not** invent an aggregate `overallConfidence` field. If the UI needs a single headline number, it must be computed client-side (e.g. max/average of findings) or a new column must be added at TDS — not decided here.                                               |
| F2  | **Missing endpoint, not silently added.** UC-19 AF-01's second branch says: after rejecting an AI Result, the Doctor "either records a manual diagnostic conclusion for the Case directly (moving the Case to `Confirmed`)… or re-runs the analysis." The approved catalog has **no endpoint** for "set a Case's conclusion directly, independent of an AI Result confirmation" — `#31` only moves a Case to `Confirmed` as a side effect of _confirming_ an AI Result (UC-19 BR-04), not of a manual override after a _rejection_. | `#31`, Case status lifecycle | This is a real gap in the approved catalog, surfaced here per the task's own instruction rather than inventing a `PATCH /cases/{id}` endpoint unilaterally. Recommend adding e.g. `PATCH /api/v1/cases/{id}` (`{"status":"CONFIRMED","conclusion":"…"}`) to the catalog in a future revision, gated by "Case must have ≥1 `Rejected` AI Result and no `Confirmed` one." |
| F3  | `ai_findings.mask_ref` is a raw storage path, same shape as `ultrasound_images.file_ref` (Module 04 F5).                                                                                                                                                                                                                                                                                                                                                                                                                            | `AiFindingResponse.maskUrl`  | Same substitution as Module 04: exposed as a signed `maskUrl`, not the raw path — an inference from the Supabase Storage decision, not an explicit UCS field.                                                                                                                                                                                                           |
| F4  | `#29`'s list of AI Results per Case is kept **unpaginated** (plain array), the same judgment call already made for `ultrasound-images` in Module 04 §F-none (a narrowing of §7.1's literal "every list endpoint" wording, justified there by the list being small and bounded per Case, not growing per-patient).                                                                                                                                                                                                                   | `#29`                        | Carried forward for consistency within the approved Module 04 spec. Flagging again here rather than silently repeating the same exception without a pointer.                                                                                                                                                                                                            |

---

## 28. `POST /api/v1/cases/{caseId}/ai-results`

| Field   | Value                                                                                                                                                               |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Run AI analysis (CLAHE preprocessing + model inference) on the Case's images; creates a new AI Result at `PENDING_REVIEW` and moves the Case to `ANALYZED` (UC-19). |
| Auth    | **Role-restricted (Doctor)**                                                                                                                                        |
| Type    | `[ACTION]`                                                                                                                                                          |

**Path Parameters**

| Name     | Type          | Required | Rule                                                                                         |
| -------- | ------------- | -------- | -------------------------------------------------------------------------------------------- |
| `caseId` | string (UUID) | Yes      | Must exist, have ≥1 `Ultrasound Image`, and not already be `CONFIRMED` (GB-01, UC-19 AF-03). |

**Request Body:** none — UC-19's Main Flow takes no input beyond selecting "Run AI Analysis"; the model version used is the current `ACTIVE` `AiModelVersion` (Module 06), resolved server-side, never client-supplied.

**Success Response — `AiResultResponse`** — `201 Created`

```json
{
    "code": 201,
    "message": "AI analysis completed",
    "data": {
        "aiResultId": "8f9012ab-3333-4444-5555-666677778888",
        "caseId": "5c6d7e8f-0000-1111-2222-333344445555",
        "modelVersionId": "1a2b3c4d-aaaa-bbbb-cccc-ddddeeeeffff",
        "status": "PENDING_REVIEW",
        "confirmedBy": null,
        "confirmedAt": null,
        "doctorNote": null,
        "findings": [
            {
                "findingId": "9012ab34-4444-5555-6666-777788889999",
                "imageId": "6d7e8f90-1111-2222-3333-444455556666",
                "maskUrl": "https://…/signed-mask-url",
                "lesionType": "hypoechoic_mass",
                "confidence": 0.9123,
                "sizeMm": 12.5
            }
        ],
        "createdAt": "2026-07-30T09:15:00Z",
        "updatedAt": "2026-07-30T09:15:00Z"
    }
}
```

`findings` may be an empty array (`[]`) if the model detects no abnormal region — `ai_findings` is a 0..n child table (schema comment: "1 lần chạy AI phát hiện 0..n vùng bất thường").

**Error Responses**

| Code | Condition                                                                                                                                                                                                                                                                                            |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 401  | Missing/expired access token.                                                                                                                                                                                                                                                                        |
| 403  | Caller authenticated but not `Doctor`.                                                                                                                                                                                                                                                               |
| 404  | `caseId` does not exist.                                                                                                                                                                                                                                                                             |
| 422  | Case has no uploaded image, or the image(s) are unreadable (UC-19 AF-02); or the Case is already `CONFIRMED` (UC-19 AF-03, GB-01); or there is no `ACTIVE` model version to run (operational precondition, not explicitly named by the UCS — see the single-active-version constraint in Module 06). |

**Security:** `bearerAuth: []`. Roles: **Doctor only**.

---

## 29. `GET /api/v1/cases/{caseId}/ai-results`

| Field   | Value                                                                                                                          |
| ------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Summary | List all AI Result runs for a Case, including their AI Findings (segmentation mask, classification, confidence, size) (UC-19). |
| Auth    | **Role-restricted (Doctor)**                                                                                                   |
| Type    | `[CRUD]`                                                                                                                       |

**Path Parameters**

| Name     | Type          | Required | Rule        |
| -------- | ------------- | -------- | ----------- |
| `caseId` | string (UUID) | Yes      | Must exist. |

**Success Response — `AiResultResponse[]`** — `200 OK` (unpaginated — see F4). Same item shape as `#28`'s response, ordered most-recent-first (`created_at DESC`).

**Error Responses**

| Code | Condition                              |
| ---- | -------------------------------------- |
| 401  | Missing/expired access token.          |
| 403  | Caller authenticated but not `Doctor`. |
| 404  | `caseId` does not exist.               |

**Security:** `bearerAuth: []`. Roles: **Doctor only**. Never exposed to `Patient` directly (GB-05) — a Patient's view of the confirmed conclusion comes from `#23`'s `conclusion` field (Module 04), not from this endpoint.

---

## 30. `GET /api/v1/ai-results/{id}`

| Field   | Value                                     |
| ------- | ----------------------------------------- |
| Summary | Read one AI Result's full detail (UC-19). |
| Auth    | **Role-restricted (Doctor)**              |
| Type    | `[CRUD]`                                  |

**Path Parameters**

| Name | Type          | Required | Rule                      |
| ---- | ------------- | -------- | ------------------------- |
| `id` | string (UUID) | Yes      | `aiResultId`. Must exist. |

**Success Response — `AiResultResponse`** — `200 OK` (same shape as `#28`'s response).

**Error Responses**

| Code | Condition                              |
| ---- | -------------------------------------- |
| 401  | Missing/expired access token.          |
| 403  | Caller authenticated but not `Doctor`. |
| 404  | `id` does not exist.                   |

**Security:** `bearerAuth: []`. Roles: **Doctor only**.

---

## 31. `PATCH /api/v1/ai-results/{id}`

| Field   | Value                                                                                                                                                     |
| ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Confirm or reject a `PENDING_REVIEW` AI Result via `{"status":"CONFIRMED"\|"REJECTED","doctorNote":"..."}` — one-way, Doctor-only (GB-01, GB-02) (UC-19). |
| Auth    | **Role-restricted (Doctor)**                                                                                                                              |
| Type    | `[CRUD]`                                                                                                                                                  |

**Path Parameters**

| Name | Type          | Required | Rule                                                                                                                                  |
| ---- | ------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| `id` | string (UUID) | Yes      | `aiResultId`. Must currently be `PENDING_REVIEW` (`ck_ai_results_review_state` — no transition out of `Confirmed`/`Rejected`, GB-01). |

**Request Body — `ReviewDecisionRequest`** (name reused verbatim from UC-19's own Request Fields table)

| Field        | Type                       | Required | Rule                                                                                                                                                                                      |
| ------------ | -------------------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `status`     | enum `CONFIRMED\|REJECTED` | Yes      | Maps to UC-19's "Decision" field (there described as Yes/No; expressed here as an explicit status enum to match this catalog's `PATCH + status field` convention, §3.3 of the API rules). |
| `doctorNote` | string                     | No       | Free-text note explaining the decision.                                                                                                                                                   |

```json
{
    "status": "CONFIRMED",
    "doctorNote": "Consistent with benign fibroadenoma; recommend 6-month follow-up."
}
```

**Success Response — `AiResultResponse`** — `200 OK`

- **On `CONFIRMED`:** `ai_results.status → CONFIRMED`, `confirmedBy`/`confirmedAt` set to the caller/now (`ck_ai_results_review_state`); **the parent Case also moves to `CONFIRMED` in the same transaction** (UC-19 BR-04) — the confirmed conclusion becomes visible to the Patient via Module 04's `#23` (GB-05).
- **On `REJECTED`:** `ai_results.status → REJECTED`, `confirmedBy`/`confirmedAt` still set (the DB constraint requires both regardless of Confirm/Reject — effectively "reviewed by/at"). **The parent Case's `status` is left unchanged** (stays `ANALYZED`) — moving it to `CONFIRMED` via a manual conclusion is the gap noted in F2; this endpoint alone does not cover that path.

**Error Responses**

| Code | Condition                                                                             |
| ---- | ------------------------------------------------------------------------------------- |
| 400  | `status` missing or not one of `CONFIRMED`/`REJECTED`.                                |
| 401  | Missing/expired access token.                                                         |
| 403  | Caller authenticated but not `Doctor`.                                                |
| 404  | `id` does not exist.                                                                  |
| 422  | The AI Result is not `PENDING_REVIEW` (already reviewed — GB-01, one-way transition). |

**Security:** `bearerAuth: []`. Roles: **Doctor only**.

---

## 32. `GET /api/v1/cases/{caseId}/progress`

| Field   | Value                                                                                                                                         |
| ------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Read-only prognosis-tracking aggregate: conclusion/lesion trend across the patient's prior visits, no separate stored entity (FT-22) (UC-19). |
| Auth    | **Role-restricted (Doctor)**                                                                                                                  |
| Type    | `[ACTION]`                                                                                                                                    |

**Path Parameters**

| Name     | Type          | Required | Rule                                                                                                                                                               |
| -------- | ------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `caseId` | string (UUID) | Yes      | Must exist. Used to resolve the owning `patientProfileId`; the response covers **all** of that patient's Cases, not just this one (UC-19 step 5 — "prior visits"). |

**Success Response — `CaseProgressResponse`** — `200 OK`

```json
{
    "code": 200,
    "message": "Case progression retrieved successfully",
    "data": {
        "patientProfileId": "a1b2c3d4-1111-2222-3333-444455556666",
        "timeline": [
            {
                "caseId": "4b5c6d7e-9999-0000-1111-222233334444",
                "visitDate": "2026-04-10",
                "status": "CONFIRMED",
                "conclusion": "Benign cyst, stable",
                "findings": [
                    { "lesionType": "cyst", "sizeMm": 8.0, "confidence": 0.87 }
                ]
            },
            {
                "caseId": "5c6d7e8f-0000-1111-2222-333344445555",
                "visitDate": "2026-07-30",
                "status": "ANALYZED",
                "conclusion": null,
                "findings": [
                    { "lesionType": "cyst", "sizeMm": 9.2, "confidence": 0.91 }
                ]
            }
        ]
    }
}
```

Read-only, no dedicated entity (`cases.visit_date` DESC ordering reversed here to ascending, so the trend reads chronologically — UC-08 BR-03 / this endpoint's own "no separate stored state" note apply equally here). `conclusion` uses the same field defined in Module 04's `CaseResponse` (see Module 04 F1 for the underlying-column ambiguity).

**Error Responses**

| Code | Condition                              |
| ---- | -------------------------------------- |
| 401  | Missing/expired access token.          |
| 403  | Caller authenticated but not `Doctor`. |
| 404  | `caseId` does not exist.               |

**Security:** `bearerAuth: []`. Roles: **Doctor only**.

---

## Module 05 Summary

| #   | Method | Endpoint                            | Auth                     | Type   |
| --- | ------ | ----------------------------------- | ------------------------ | ------ |
| 28  | POST   | `/api/v1/cases/{caseId}/ai-results` | Role-restricted (Doctor) | ACTION |
| 29  | GET    | `/api/v1/cases/{caseId}/ai-results` | Role-restricted (Doctor) | CRUD   |
| 30  | GET    | `/api/v1/ai-results/{id}`           | Role-restricted (Doctor) | CRUD   |
| 31  | PATCH  | `/api/v1/ai-results/{id}`           | Role-restricted (Doctor) | CRUD   |
| 32  | GET    | `/api/v1/cases/{caseId}/progress`   | Role-restricted (Doctor) | ACTION |

No endpoint outside the approved catalog was added — **F2 is a documented gap, not a silent addition.**

## Shared DTOs Introduced in Module 05

| DTO                    | Used by                                                                        | Notes                                                                                                                                                                                     |
| ---------------------- | ------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AiResultResponse`     | #28, #29, #30, #31; embedded (minimal) in Module 04's `CaseResponse.aiResults` | Full shape includes `findings: AiFindingResponse[]`; the Module 04 embed is a trimmed-down `aiResultId`/`status`/summary-only projection — this document is the authoritative full shape. |
| `AiFindingResponse`    | Embedded in `AiResultResponse.findings`                                        | `maskUrl` is a signed URL (see F3), never `mask_ref` raw.                                                                                                                                 |
| `CaseProgressResponse` | #32                                                                            | References the same `conclusion` field defined in Module 04.                                                                                                                              |

Waiting on your review before continuing to Module 06 — which follows below in the same batch.
