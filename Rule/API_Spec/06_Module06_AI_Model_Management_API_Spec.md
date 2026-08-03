# ADSUS API Specification — Module 06: AI Model Management

| Field            | Value                                                                                                                                                                                                                                    |
| ---------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Document version | v0.1 (draft) — 2026-07-30                                                                                                                                                                                                                |
| Role             | Senior API Architect + Detail Designer                                                                                                                                                                                                   |
| Scope            | Endpoints #33–#36 of `ADSUS_API_Catalog_v1.1.md` (approved catalog — **no endpoint added, removed, or renamed here**)                                                                                                                    |
| Sources          | `.ai-context/project_context.md` · `.ai-context/api_design_rule/api_design_rules_v0.2.md` · `Reports_md/Report_3.1_UCS_ADSUS.md` (UC-20) · `Documents/02_Requirements/SQL/ADSUS_Physical_Schema_PostgreSQL_v1.sql` (`ai_model_versions`) |
| Language         | English                                                                                                                                                                                                                                  |

> Same ERD substitution note as prior modules: field types below come from `ADSUS_Physical_Schema_PostgreSQL_v1.sql`, not the Logical ERD.

---

## 0. Flags & Open Issues

| #   | Issue                                                                                                                                                                                                                                                                                                                                                                              | Where it shows up                                  | Recommendation                                                                                                                                                                                                                                      |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| F1  | UC-20's own boundary note already flags "Model Artifact URI" as "a reasonable technical necessity… but not a PRD-listed attribute." Confirmed now at the physical layer too: `ai_model_versions` has **no** artifact-path/URI column at all (`model_version_id, version_code, description, eval_sensitivity, eval_accuracy, eval_auc, status, registered_by, registered_at` only). | `#33 RegisterModelVersionRequest.modelArtifactUri` | Kept in the request schema as illustrative/optional-in-practice, but there is currently nowhere to persist it. Confirm at TDS whether the model file path belongs in this table, a config file, or a separate artifact registry — not decided here. |
| F2  | `#34`'s list of model versions is kept **unpaginated** (plain array) — same judgment call as Module 05 F4 (small, Admin-managed, bounded list, not a per-patient-growth table).                                                                                                                                                                                                    | `#34`                                              | Flagging for consistency, not repeating the reasoning — see Module 05 F4.                                                                                                                                                                           |

---

## 33. `POST /api/v1/ai-model-versions`

| Field   | Value                                                                                                                               |
| ------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Register a new AI model version (version code, artifact URI, change description, benchmark metrics) at status `REGISTERED` (UC-20). |
| Auth    | **Role-restricted (Admin)**                                                                                                         |
| Type    | `[CRUD]`                                                                                                                            |

**Request Body — `RegisterModelVersionRequest`** (name reused verbatim from UC-20's own Request Fields table)

| Field              | Type         | Required | Rule                                                                                     |
| ------------------ | ------------ | -------- | ---------------------------------------------------------------------------------------- |
| `versionCode`      | string       | Yes      | Unique system-wide (`uq_ai_model_versions_code`), e.g. `"v1.2.0"`. Max 50 chars.         |
| `modelArtifactUri` | string (URI) | No       | **See F1 — no backing column yet; accepted but not currently persisted.**                |
| `description`      | string       | No       | Change description (architecture/training-data notes). Maps to the `description` column. |
| `evalSensitivity`  | number       | No       | Percent, `0–100` (`ck_ai_model_versions_sensitivity`). Research KPI target: `> 90`.      |
| `evalAccuracy`     | number       | No       | Percent, `0–100` (`ck_ai_model_versions_accuracy`). Research KPI target: `> 85`.         |
| `evalAuc`          | number       | No       | Scale `0–1` (`ck_ai_model_versions_auc`). Research KPI target: `> 0.90`.                 |

```json
{
    "versionCode": "v1.2.0",
    "description": "Improved augmentation, +15% training set",
    "evalSensitivity": 92.3,
    "evalAccuracy": 88.1,
    "evalAuc": 0.912
}
```

**Success Response — `AiModelVersionResponse`** — `201 Created`

```json
{
    "code": 201,
    "message": "AI model version registered successfully",
    "data": {
        "modelVersionId": "1a2b3c4d-aaaa-bbbb-cccc-ddddeeeeffff",
        "versionCode": "v1.2.0",
        "description": "Improved augmentation, +15% training set",
        "evalSensitivity": 92.3,
        "evalAccuracy": 88.1,
        "evalAuc": 0.912,
        "status": "REGISTERED",
        "registeredBy": "9a1b2c3d-4e5f-6789-0abc-def123456789",
        "registeredAt": "2026-07-30T09:20:00Z"
    }
}
```

**Error Responses**

| Code | Condition                                                                     |
| ---- | ----------------------------------------------------------------------------- |
| 400  | `versionCode` missing/malformed.                                              |
| 401  | Missing/expired access token.                                                 |
| 403  | Caller authenticated but not `Admin`.                                         |
| 409  | `versionCode` already registered (UC-20 AF-02, `uq_ai_model_versions_code`).  |
| 422  | `evalSensitivity`/`evalAccuracy` outside `0–100`, or `evalAuc` outside `0–1`. |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## 34. `GET /api/v1/ai-model-versions`

| Field   | Value                                                                                        |
| ------- | -------------------------------------------------------------------------------------------- |
| Summary | List registered model versions with status and metrics (Sensitivity, Accuracy, AUC) (UC-20). |
| Auth    | **Role-restricted (Admin)**                                                                  |
| Type    | `[CRUD]`                                                                                     |

**Success Response — `AiModelVersionResponse[]`** — `200 OK` (unpaginated — see F2). Same item shape as `#33`'s response, ordered most-recent-first (`registered_at DESC`).

**Error Responses**

| Code | Condition                             |
| ---- | ------------------------------------- |
| 401  | Missing/expired access token.         |
| 403  | Caller authenticated but not `Admin`. |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## 35. `GET /api/v1/ai-model-versions/{id}`

| Field   | Value                                                        |
| ------- | ------------------------------------------------------------ |
| Summary | Read one model version's detail/performance metrics (UC-20). |
| Auth    | **Role-restricted (Admin)**                                  |
| Type    | `[CRUD]`                                                     |

**Path Parameters**

| Name | Type          | Required | Rule                          |
| ---- | ------------- | -------- | ----------------------------- |
| `id` | string (UUID) | Yes      | `modelVersionId`. Must exist. |

**Success Response — `AiModelVersionResponse`** — `200 OK` (same shape as `#33`'s response).

**Error Responses**

| Code | Condition                             |
| ---- | ------------------------------------- |
| 401  | Missing/expired access token.         |
| 403  | Caller authenticated but not `Admin`. |
| 404  | `id` does not exist.                  |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## 36. `PATCH /api/v1/ai-model-versions/{id}`

| Field   | Value                                                                                                                                                                    |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Summary | Activate or roll back a version via `{"status":"ACTIVE"}`; the previously Active version automatically moves to `INACTIVE` (two-way toggle, exception to GB-01) (UC-20). |
| Auth    | **Role-restricted (Admin)**                                                                                                                                              |
| Type    | `[CRUD]`                                                                                                                                                                 |

**Path Parameters**

| Name | Type          | Required | Rule                          |
| ---- | ------------- | -------- | ----------------------------- |
| `id` | string (UUID) | Yes      | `modelVersionId`. Must exist. |

**Request Body — `UpdateModelVersionStatusRequest`**

| Field    | Type                    | Required | Rule                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| -------- | ----------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `status` | enum `ACTIVE\|INACTIVE` | Yes      | `→ ACTIVE` (Activate/Rollback-to-this-version): the DB enforces "exactly one `ACTIVE` version at a time" via a **partial unique index** (`uq_ai_model_versions_one_active`) — activating this version implicitly deactivates whichever version was previously `ACTIVE`, in the same transaction (UC-20 BR-02, BR-03). `REGISTERED` is not a valid target of this endpoint (a version only ever reaches `ACTIVE` from here; it starts at `REGISTERED` via `#33`). |

```json
{ "status": "ACTIVE" }
```

**Success Response — `AiModelVersionResponse`** — `200 OK` (same shape as `#33`'s response, reflecting the new status). The response only describes the version acted upon — the caller can confirm the previous `ACTIVE` version's new `INACTIVE` state via `#34`/`#35` if needed; this endpoint does not return the deactivated version in the same payload (kept single-resource, per the general PATCH convention in `api_design_rules_v0.2.md` §3.3).

**Error Responses**

| Code | Condition                                                                                                                                                                                                                                                              |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 400  | `status` missing or not `ACTIVE`/`INACTIVE`.                                                                                                                                                                                                                           |
| 401  | Missing/expired access token.                                                                                                                                                                                                                                          |
| 403  | Caller authenticated but not `Admin`.                                                                                                                                                                                                                                  |
| 404  | `id` does not exist.                                                                                                                                                                                                                                                   |
| 422  | `status: "INACTIVE"` requested directly on a version that is not currently `ACTIVE` (no-op/invalid transition — a version only leaves `ACTIVE` as the side effect of _another_ version being activated, never a direct standalone deactivation per UC-20's Main Flow). |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## Module 06 Summary

| #   | Method | Endpoint                         | Auth                    | Type |
| --- | ------ | -------------------------------- | ----------------------- | ---- |
| 33  | POST   | `/api/v1/ai-model-versions`      | Role-restricted (Admin) | CRUD |
| 34  | GET    | `/api/v1/ai-model-versions`      | Role-restricted (Admin) | CRUD |
| 35  | GET    | `/api/v1/ai-model-versions/{id}` | Role-restricted (Admin) | CRUD |
| 36  | PATCH  | `/api/v1/ai-model-versions/{id}` | Role-restricted (Admin) | CRUD |

No endpoint outside the approved catalog was added.

## Shared DTOs Introduced in Module 06

| DTO                      | Used by            | Notes                                                                                                           |
| ------------------------ | ------------------ | --------------------------------------------------------------------------------------------------------------- |
| `AiModelVersionResponse` | #33, #34, #35, #36 | One shape for create/read/list/update responses — no role-based field variation (Admin-only module throughout). |

Waiting on your review before continuing to Module 07 (Prescription & Adherence) onward.
