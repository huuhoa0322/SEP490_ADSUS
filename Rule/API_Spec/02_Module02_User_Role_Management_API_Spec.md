# ADSUS API Specification — Module 02: User & Role Management

| Field            | Value                                                                                                                                                                                                                              |
| ---------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Document version | v0.1 (draft, first 2 modules for review) — 2026-07-30                                                                                                                                                                              |
| Role             | Senior API Architect + Detail Designer                                                                                                                                                                                             |
| Scope            | Endpoints #11–#15 of `ADSUS_API_Catalog_v1.1.md` (approved catalog — **no endpoint added, removed, or renamed here**)                                                                                                              |
| Sources          | `.ai-context/project_context.md` · `.ai-context/api_design_rule/api_design_rules_v0.2.md` · `Reports_md/Report_3.1_UCS_ADSUS.md` (UC-04) · `Documents/02_Requirements/SQL/ADSUS_Physical_Schema_PostgreSQL_v1.sql` (`users` table) |
| Language         | English (technical terms/field names in code, per `api_design_rules_v0.2.md`)                                                                                                                                                      |

> Same ERD substitution note as Module 01 applies: field types below come from `ADSUS_Physical_Schema_PostgreSQL_v1.sql`, since the Logical ERD carries no column-level detail by design.

---

## 0. Flags & Open Issues

| #                           | Issue                                                                                                                                                                                                                                      | Where it shows up                                                | Recommendation                                                                                                                                                                                                                            |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| F1                          | ✅ **Resolved 2026-07-29** (restated from Module 01 — was blocking `#12`/`#14`). `user_role` enum now has `ADMIN, DOCTOR, PATIENT, NURSE` — confirmed in `ADSUS_BE.DAL/Entities/Enums.cs`/`AppDbContext.cs`. | `CreateUserAccountRequest.role`, `UpdateUserAccountRequest.role` | `role: "NURSE"` may be accepted/persisted normally — no `x-not-yet-implemented` flag needed on the OpenAPI schema. |
| F3 (carried from Module 01) | This document uses the already-ratified `PagedResult<T>` (per `api_design_rules_v0.2.md` §7.1) for `#11`'s list response, not the `PageResponse<T>` name given in this task's instructions — see Module 01's flag table for the full note. | `#11 GET /users`                                                 | Confirm the canonical pagination wrapper name once, project-wide.                                                                                                                                                                         |

---

## 11. `GET /api/v1/users`

| Field   | Value                                                                          |
| ------- | ------------------------------------------------------------------------------ |
| Summary | List/search login accounts, paginated, for the Admin User List screen (UC-04). |
| Auth    | **Role-restricted (Admin)**                                                    |
| Type    | `[CRUD]`                                                                       |

**Query Parameters — `UserSearchCriteria`**

| Field      | Type                               | Required | Rule                                                       |
| ---------- | ---------------------------------- | -------- | ---------------------------------------------------------- |
| `search`   | string                             | No       | Case-insensitive substring match on `fullName` or `phone`. |
| `role`     | enum `ADMIN\|DOCTOR\|PATIENT`      | No       | Filters by role. (`NURSE` excluded — see F1.)              |
| `status`   | enum `ACTIVE\|LOCKED\|DEACTIVATED` | No       | Filters by account status.                                 |
| `page`     | integer                            | No       | Default `1`.                                               |
| `pageSize` | integer                            | No       | Default `20`, max `100` (§7.1).                            |

**Request Body:** none.

**Success Response — `PagedResult<UserSummaryResponse>`** — `200 OK`

```json
{
    "code": 200,
    "message": "Users retrieved successfully",
    "data": {
        "items": [
            {
                "userId": "3f2a1c90-6b1e-4b2a-9c3d-8f1e2a7b0d11",
                "fullName": "Dr. Tran Van B",
                "phone": "0988776655",
                "role": "DOCTOR",
                "status": "ACTIVE",
                "createdAt": "2026-07-01T08:00:00Z"
            }
        ],
        "page": 1,
        "pageSize": 20,
        "totalItems": 1,
        "totalPages": 1
    }
}
```

`UserSummaryResponse` intentionally omits `email`, `dateOfBirth`, `biometricEnabled`, `mustChangePassword` — those belong to the single-item `UserResponse` (`#13`), not the list view.

**Error Responses**

| Code | Condition                                                           |
| ---- | ------------------------------------------------------------------- |
| 400  | `page`/`pageSize` out of range, invalid `role`/`status` enum value. |
| 401  | Missing/expired access token.                                       |
| 403  | Caller authenticated but not `Admin`.                               |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## 12. `POST /api/v1/users`

| Field   | Value                                                                                                           |
| ------- | --------------------------------------------------------------------------------------------------------------- |
| Summary | Create a Doctor / Nurse / Patient login account; system auto-generates and emails a temporary password (UC-04). |
| Auth    | **Role-restricted (Admin)**                                                                                     |
| Type    | `[CRUD]`                                                                                                        |

**Request Body — `CreateUserAccountRequest`** (name reused verbatim from UC-04's own Request Fields table)

| Field         | Type                          | Required | Rule                                                                                                                                                  |
| ------------- | ----------------------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `phone`       | string                        | Yes      | Unique system-wide (`uq_users_phone`). Format `^\+?[0-9]{9,15}$`.                                                                                     |
| `fullName`    | string                        | Yes      | Non-empty, max 100 chars (`VARCHAR(100)`).                                                                                                            |
| `role`        | enum `DOCTOR\|NURSE\|PATIENT` | Yes      | Admin accounts are not created here (UC-04). **`NURSE` is not yet enabled at the DB layer — see F1.**                                                 |
| `email`       | string                        | Yes      | Unique system-wide, case-insensitive (`uq_users_email_lower`). Used only for password recovery, never login.                                          |
| `dateOfBirth` | string (date)                 | No       | Shown/entered only when `role = DOCTOR` on the Admin UI; hidden entirely when `role = PATIENT` (UC-04 BR-01). If provided, must not be in the future. |

```json
{
    "phone": "0988776655",
    "fullName": "Dr. Tran Van B",
    "role": "DOCTOR",
    "email": "tranvanb@clinic.vn",
    "dateOfBirth": "1985-03-20"
}
```

**Success Response — `UserResponse`** — `201 Created`

```json
{
    "code": 201,
    "message": "User account created successfully",
    "data": {
        "userId": "9a1b2c3d-4e5f-6789-0abc-def123456789",
        "fullName": "Dr. Tran Van B",
        "phone": "0988776655",
        "email": "tranvanb@clinic.vn",
        "dateOfBirth": "1985-03-20",
        "role": "DOCTOR",
        "status": "ACTIVE",
        "mustChangePassword": true,
        "createdAt": "2026-07-30T09:00:00Z",
        "updatedAt": "2026-07-30T09:00:00Z"
    }
}
```

`dateOfBirth` is **omitted from this response entirely** (not merely `null`) when `role = PATIENT` (`sensitive_data_rules` §5 — "the Response DTO must not even declare the field").

**Error Responses**

| Code | Condition                                                                                                          |
| ---- | ------------------------------------------------------------------------------------------------------------------ |
| 400  | Missing/malformed `phone`, `fullName`, `email`; invalid `role` enum value.                                         |
| 401  | Missing/expired access token.                                                                                      |
| 403  | Caller authenticated but not `Admin`.                                                                              |
| 409  | `phone` or `email` already registered to another account (UC-04 AF-03, `uq_users_phone` / `uq_users_email_lower`). |
| 422  | `dateOfBirth` is in the future.                                                                                    |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## 13. `GET /api/v1/users/{id}`

| Field   | Value                                                                                                                                    |
| ------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | View one account's admin-facing profile; `dateOfBirth` is omitted when the target's role is Patient (`sensitive_data_rules` §5) (UC-04). |
| Auth    | **Role-restricted (Admin)**                                                                                                              |
| Type    | `[CRUD]`                                                                                                                                 |

**Path Parameters**

| Name | Type          | Required | Rule                                   |
| ---- | ------------- | -------- | -------------------------------------- |
| `id` | string (UUID) | Yes      | Target account's `userId`. Must exist. |

**Success Response — `UserResponse`** — `200 OK` (same shape as `#12`'s response — see field-omission rule above; also applies here).

```json
{
    "code": 200,
    "message": "User retrieved successfully",
    "data": {
        "userId": "3f2a1c90-6b1e-4b2a-9c3d-8f1e2a7b0d11",
        "fullName": "Nguyen Van A",
        "phone": "0901234567",
        "email": "nguyenvana@gmail.com",
        "role": "PATIENT",
        "status": "ACTIVE",
        "mustChangePassword": false,
        "createdAt": "2026-05-10T08:00:00Z",
        "updatedAt": "2026-07-15T10:20:00Z"
    }
}
```

Note `dateOfBirth` is absent above because `role = PATIENT`. A `DOCTOR`/`ADMIN` target would include it.

**Error Responses**

| Code | Condition                             |
| ---- | ------------------------------------- |
| 401  | Missing/expired access token.         |
| 403  | Caller authenticated but not `Admin`. |
| 404  | `id` does not match any account.      |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## 14. `PATCH /api/v1/users/{id}`

| Field   | Value                                                                                                    |
| ------- | -------------------------------------------------------------------------------------------------------- |
| Summary | Update an account's role or status, including Lock ⇄ Unlock via `{"status":"LOCKED"\|"ACTIVE"}` (UC-04). |
| Auth    | **Role-restricted (Admin)**                                                                              |
| Type    | `[CRUD]`                                                                                                 |

**Path Parameters**

| Name | Type          | Required | Rule                                   |
| ---- | ------------- | -------- | -------------------------------------- |
| `id` | string (UUID) | Yes      | Target account's `userId`. Must exist. |

**Request Body — `UpdateUserAccountRequest`** — all fields optional; send only what changes.

| Field         | Type                               | Required | Rule                                                                                                                                                                                                  |
| ------------- | ---------------------------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `fullName`    | string                             | No       | Non-empty if provided.                                                                                                                                                                                |
| `email`       | string                             | No       | Unique system-wide (case-insensitive) if provided.                                                                                                                                                    |
| `dateOfBirth` | string (date)                      | No       | Must not be in the future. Rejected outright if the target's `role = PATIENT` and this field is present (UC-04 BR-01 — Admin never writes this field for a Patient target either, not just reads it). |
| `role`        | enum `DOCTOR\|NURSE\|PATIENT`      | No       | Reassigns the account's role. `NURSE` blocked until F1 is resolved.                                                                                                                                   |
| `status`      | enum `ACTIVE\|LOCKED\|DEACTIVATED` | No       | State transition — see the User state machine: `ACTIVE ⇄ LOCKED` is fully manual (UC-04 BR-04); `→ DEACTIVATED` is one-way and terminal (UC-04 BR-05, GB-07).                                         |

```json
{ "status": "LOCKED" }
```

**Success Response — `UserResponse`** — `200 OK` (same shape as `#13`, reflecting the update).

**Error Responses**

| Code | Condition                                                                                                                                                                                           |
| ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 400  | Malformed `email`; invalid `role`/`status` enum value.                                                                                                                                              |
| 401  | Missing/expired access token.                                                                                                                                                                       |
| 403  | Caller authenticated but not `Admin`.                                                                                                                                                               |
| 404  | `id` does not match any account.                                                                                                                                                                    |
| 409  | `email` already registered to another account.                                                                                                                                                      |
| 422  | `dateOfBirth` in the future, `dateOfBirth` sent for a `PATIENT` target, or `status` requests an illegal transition (e.g. `DEACTIVATED → ACTIVE` — no reactivation path per the User state machine). |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## 15. `DELETE /api/v1/users/{id}`

| Field   | Value                                                                                                                 |
| ------- | --------------------------------------------------------------------------------------------------------------------- |
| Summary | Soft-delete: deactivate an account (`Status = DEACTIVATED`), one-way, never hard-deleted (UC-04 AF-02, GB-03, GB-07). |
| Auth    | **Role-restricted (Admin)**                                                                                           |
| Type    | `[CRUD]`                                                                                                              |

**Path Parameters**

| Name | Type          | Required | Rule                                                                    |
| ---- | ------------- | -------- | ----------------------------------------------------------------------- |
| `id` | string (UUID) | Yes      | Target account's `userId`. Must exist and not already be `DEACTIVATED`. |

**Request Body:** none.

**Success Response** — `200 OK` (per `<http_status_codes>` — soft delete returns `200`, not `204`; `data: null` per the established convention in `api_design_rules_v0.2.md` §4.4).

```json
{ "code": 200, "message": "User account deactivated", "data": null }
```

**Error Responses**

| Code | Condition                                                                                                                  |
| ---- | -------------------------------------------------------------------------------------------------------------------------- |
| 401  | Missing/expired access token.                                                                                              |
| 403  | Caller authenticated but not `Admin`.                                                                                      |
| 404  | `id` does not match any account.                                                                                           |
| 422  | Account is already `DEACTIVATED` (terminal state — see the User state machine; re-deactivating is not a valid transition). |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## Module 02 Summary

| #   | Method | Endpoint             | Auth                    | Type |
| --- | ------ | -------------------- | ----------------------- | ---- |
| 11  | GET    | `/api/v1/users`      | Role-restricted (Admin) | CRUD |
| 12  | POST   | `/api/v1/users`      | Role-restricted (Admin) | CRUD |
| 13  | GET    | `/api/v1/users/{id}` | Role-restricted (Admin) | CRUD |
| 14  | PATCH  | `/api/v1/users/{id}` | Role-restricted (Admin) | CRUD |
| 15  | DELETE | `/api/v1/users/{id}` | Role-restricted (Admin) | CRUD |

No endpoint outside the approved catalog was added.

## Shared DTOs Introduced Across Modules 01–02

| DTO                   | Used by                       | Notes                                                                                                                                                                            |
| --------------------- | ----------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SignInResponse`      | #1, #2, #4                    | Never reused outside auth-flow responses — no other endpoint returns `accessToken`/`refreshToken` (`sensitive_data_rules` §5).                                                   |
| `UserSelfResponse`    | #8, #9                        | Self-view shape — always includes `dateOfBirth` regardless of role (the Admin-hiding rule is specific to _Admin-facing_ DTOs, per `sensitive_data_rules` §5's own scope column). |
| `UserResponse`        | #12, #13, #14                 | Admin-facing detail shape — omits `dateOfBirth` when the target's `role = PATIENT`.                                                                                              |
| `UserSummaryResponse` | #11 (inside `PagedResult<T>`) | Lightweight list shape — no `email`/`dateOfBirth`/flags.                                                                                                                         |

Waiting on your review before continuing to Module 03 (Dashboard & Reporting) onward.
