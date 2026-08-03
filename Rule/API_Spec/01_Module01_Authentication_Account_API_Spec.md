# ADSUS API Specification — Module 01: Authentication & Account

| Field            | Value                                                                                                                                                                                                                                                          |
| ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Document version | v0.1 (draft, first 2 modules for review) — 2026-07-30                                                                                                                                                                                                          |
| Role             | Senior API Architect + Detail Designer                                                                                                                                                                                                                         |
| Scope            | Endpoints #1–#10 of `ADSUS_API_Catalog_v1.1.md` (approved catalog — **no endpoint added, removed, or renamed here**)                                                                                                                                           |
| Sources          | `.ai-context/project_context.md` · `.ai-context/api_design_rule/api_design_rules_v0.2.md` · `Reports_md/Report_3.1_UCS_ADSUS.md` (UC-01, UC-02, UC-03, UC-10, UC-25) · `Documents/02_Requirements/SQL/ADSUS_Physical_Schema_PostgreSQL_v1.sql` (`users` table) |
| Language         | English (technical terms/field names in code, per `api_design_rules_v0.2.md`)                                                                                                                                                                                  |

> **Note on the ERD source:** the task named `ADSUS_ERD_Logical_v3.drawio` as the schema source. Per `project_context.md`'s own boundary rule ("Logical ERD shows entities/relationships only — no columns, no data types"), the Logical ERD does not carry field-level types needed to write a Response DTO. Column names/types below are taken from `ADSUS_Physical_Schema_PostgreSQL_v1.sql` (`users` table, lines 92–129) instead, which is the one artifact that actually has them. Flagged as a substitution, not a silent swap.

---

## 0. Flags & Open Issues (read before implementing)

These are gaps found while writing this spec — per the task's instruction, **not silently resolved**, listed here instead of inventing an answer.

| #   | Issue                                                                                                                                                          | Where it shows up                                                        | Recommendation                                                                                                                                                                                                                                                                                              |
| --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| F1  | ✅ **Resolved 2026-07-29** (was: `user_role` enum in the physical DB only had `ADMIN, DOCTOR, PATIENT`, `NURSE` missing). `NURSE` is now a valid `user_role` value — confirmed in `ADSUS_BE.DAL/Entities/Enums.cs` and `AppDbContext.cs`. | #12 `CreateUserAccountRequest.role`, #14 `UpdateUserAccountRequest.role` | `role: NURSE` may now be implemented against the current DB — no pending migration required.                                                                                                                                                                       |
| F2  | No table backs "biometric device enrollment" in the physical schema — only a boolean flag `users.biometric_enabled` exists, with no device-credential storage. | #5 `POST /auth/biometric-devices`                                        | This endpoint's persistence target is undecided at the DB layer. Fields below are illustrative only (matches the same caution UC-02 itself already applies) — confirm the storage model in TDS before implementing.                                                                                         |
| F3  | `api_design_rules_v0.2.md` §7.1 already ratifies the pagination wrapper name `PagedResult<T>`. This task's own instructions ask for `PageResponse<T>`.         | Every list endpoint (`GET /users` in Module 02)                          | This document uses `PagedResult<T>` to stay consistent with the already-ratified L2 contract, **not** `PageResponse<T>` as literally requested — flagging the naming conflict rather than introducing a second, competing pagination wrapper into the project. Confirm which name should win going forward. |
| F4  | JWT access/refresh token expiry (`api_design_rules_v0.2.md` §6.1) is still marked **TBD**.                                                                     | #1, #2                                                                   | Kept as `TBD` here too — not invented for this document.                                                                                                                                                                                                                                                    |

---

## 1. `POST /api/v1/auth/login`

| Field   | Value                                                                                                                                     |
| ------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Authenticate with phone number + password; returns JWT access/refresh tokens and routes the caller to their role-based home area (UC-01). |
| Auth    | **Public** (`security: []`)                                                                                                               |
| Type    | `[ACTION]`                                                                                                                                |

**Path parameters:** none. **Query parameters:** none.

**Request Body — `SignInRequest`**

| Field      | Type   | Required | Rule                                                                                                     |
| ---------- | ------ | -------- | -------------------------------------------------------------------------------------------------------- |
| `phone`    | string | Yes      | Must match an existing account's phone number. Format: `^\+?[0-9]{9,15}$` (per `ck_users_phone_format`). |
| `password` | string | Yes      | Plain text over TLS; never logged.                                                                       |

```json
{ "phone": "0901234567", "password": "MyP@ssw0rd" }
```

**Success Response — `SignInResponse`** — `200 OK`

```json
{
    "code": 200,
    "message": "Login successful",
    "data": {
        "accessToken": "eyJhbGciOi...",
        "refreshToken": "eyJhbGciOi...",
        "userId": "3f2a1c90-6b1e-4b2a-9c3d-8f1e2a7b0d11",
        "fullName": "Nguyen Van A",
        "role": "PATIENT",
        "mustChangePassword": false
    }
}
```

| Field                | Type                          | Notes                                                                                      |
| -------------------- | ----------------------------- | ------------------------------------------------------------------------------------------ |
| `accessToken`        | string                        | JWT, short-lived (expiry: TBD, see F4).                                                    |
| `refreshToken`       | string                        | Opaque/JWT, longer-lived (expiry: TBD). Only ever returned here and by `#2 /auth/refresh`. |
| `userId`             | string (UUID)                 |                                                                                            |
| `fullName`           | string                        |                                                                                            |
| `role`               | enum `ADMIN\|DOCTOR\|PATIENT` | `NURSE` excluded until F1 is resolved.                                                     |
| `mustChangePassword` | boolean                       | `true` right after a password reset (UC-03) — client must redirect to `#10`.               |

**Error Responses**

| Code | Condition                                                                                                                                             |
| ---- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| 400  | `phone`/`password` missing or malformed.                                                                                                              |
| 401  | Phone not found, wrong password, or account `LOCKED`/`DEACTIVATED` — **GB-06: identical generic message for every cause, never distinguishes which.** |

**Security:** `security: []` (public, explicitly empty per §6.4 example in `api_design_rules_v0.2.md`).

---

## 2. `POST /api/v1/auth/refresh`

| Field   | Value                                                                                                |
| ------- | ---------------------------------------------------------------------------------------------------- |
| Summary | Exchange a valid refresh token for a new access token (UC-01).                                       |
| Auth    | **Public** (`security: []`) — the refresh token itself is the credential, not a Bearer access token. |
| Type    | `[ACTION]`                                                                                           |

**Request Body — `RefreshTokenRequest`**

| Field          | Type   | Required | Rule                                                                                      |
| -------------- | ------ | -------- | ----------------------------------------------------------------------------------------- |
| `refreshToken` | string | Yes      | Must be a currently valid, non-revoked refresh token issued by `#1` or a prior `#2` call. |

**Success Response — `SignInResponse` (token pair only)** — `200 OK`

```json
{
    "code": 200,
    "message": "Token refreshed",
    "data": { "accessToken": "eyJhbGciOi...", "refreshToken": "eyJhbGciOi..." }
}
```

**Error Responses**

| Code | Condition                                             |
| ---- | ----------------------------------------------------- |
| 400  | `refreshToken` missing.                               |
| 401  | Refresh token expired, revoked, or invalid signature. |

**Security:** `security: []`.

---

## 3. `POST /api/v1/auth/logout`

| Field   | Value                                                                       |
| ------- | --------------------------------------------------------------------------- |
| Summary | Invalidate the current refresh token, ending the signed-in session (UC-01). |
| Auth    | **Protected** (any authenticated role)                                      |
| Type    | `[ACTION]`                                                                  |

**Request Body:** none (the token to invalidate is the caller's own, taken from the `Authorization` header / an accompanying refresh token per the eventual TDS session design — not re-litigated here).

**Success Response** — `200 OK`

```json
{ "code": 200, "message": "Logged out successfully", "data": null }
```

**Error Responses**

| Code | Condition                     |
| ---- | ----------------------------- |
| 401  | Missing/expired access token. |

**Security:** `bearerAuth: []`. Roles: any authenticated role (Admin, Doctor, Nurse, Patient).

---

## 4. `POST /api/v1/auth/biometric-login`

| Field   | Value                                                                                                                           |
| ------- | ------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Sign in via a device-paired biometric credential, valid only after enrollment through `#5` (UC-02, _planned_ — Could priority). |
| Auth    | **Public\*** — no user Bearer JWT yet at this point; the device-pairing credential from `#5` is the presented credential.       |
| Type    | `[ACTION]`                                                                                                                      |

**Request Body — `BiometricLoginRequest`**

| Field                | Type   | Required | Rule                                                                                                                                                                                                                                                  |
| -------------------- | ------ | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `deviceId`           | string | Yes      | Must match a device already enrolled via `#5` for an `ACTIVE` account.                                                                                                                                                                                |
| `biometricAssertion` | string | Yes      | Opaque token produced by the OS biometric API on successful local scan. **Exact format is a TDS/FDS concern — UC-02 itself defers this ("the concrete mechanism is intentionally not specified here"); kept illustrative, not a confirmed contract.** |

**Success Response — `SignInResponse`** — `200 OK` (same shape as `#1`)

**Error Responses**

| Code | Condition                                                                                                                                                   |
| ---- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 400  | Missing `deviceId`/`biometricAssertion`.                                                                                                                    |
| 401  | Device not enrolled, assertion invalid, or account `LOCKED`/`DEACTIVATED` — same generic-message principle as GB-06/GB-07 applied by analogy (UC-02 AF-02). |

**Security:** `security: []` with a device-level credential, not a user Bearer token.

---

## 5. `POST /api/v1/auth/biometric-devices`

| Field   | Value                                                                                                                                                                                                              |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Summary | Enroll (pair) the current device for biometric sign-in, callable only from an active password-authenticated session; sets `users.biometric_enabled = true` — the prerequisite step UC-02's BR-01 requires (UC-02). |
| Auth    | **Protected** — must already hold a valid access token from a password login (`#1`), per UC-02 BR-01.                                                                                                              |
| Type    | `[ACTION]` — `[NEW in v1.1]`                                                                                                                                                                                       |

**Request Body — `EnrollBiometricDeviceRequest`**

| Field             | Type   | Required | Rule                                                                                                                                                                                                               |
| ----------------- | ------ | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `deviceId`        | string | Yes      | Client-generated stable device identifier.                                                                                                                                                                         |
| `devicePublicKey` | string | Yes      | Public half of the key pair the OS secure enclave holds — never the biometric sample itself (never reaches the backend, per `project_context.md`). **Illustrative field name — see F2; storage model TBD at TDS.** |

**Success Response** — `200 OK`

```json
{
    "code": 200,
    "message": "Biometric sign-in enabled for this device",
    "data": { "deviceId": "…", "biometricEnabled": true }
}
```

**Error Responses**

| Code | Condition                                                    |
| ---- | ------------------------------------------------------------ |
| 400  | Missing/malformed `deviceId` or `devicePublicKey`.           |
| 401  | Caller not authenticated with a valid access token.          |
| 409  | This `deviceId` is already enrolled for a different account. |

**Security:** `bearerAuth: []`. Roles: Patient.

---

## 6. `POST /api/v1/password-reset-requests`

| Field   | Value                                                                                                                                                      |
| ------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Self-service "Forgot password": validates phone + email match, emails a temporary password, flags a forced password change on next sign-in (UC-03, FT-06). |
| Auth    | **Public** (`security: []`) — this is exactly the case where the caller cannot sign in.                                                                    |
| Type    | `[ACTION]`                                                                                                                                                 |

**Request Body — `PasswordResetRequest`** (name reused verbatim from UC-03's own Request Fields table)

| Field   | Type   | Required | Rule                                                                             |
| ------- | ------ | -------- | -------------------------------------------------------------------------------- |
| `phone` | string | Yes      | The account's registered phone number.                                           |
| `email` | string | Yes      | The account's registered email — both must match the same account (UC-03 BR-01). |

**Success Response** — `200 OK` — **always** the same shape, whether or not the account was found (GB-06-style anti-enumeration, applied by the UCS's own analogy at UC-03 AF-01).

```json
{
    "code": 200,
    "message": "If the information is correct, a new password has been sent to your email.",
    "data": null
}
```

**Error Responses**

| Code | Condition                             |
| ---- | ------------------------------------- |
| 400  | `phone`/`email` missing or malformed. |

> No 401/404 here by design — UC-03 AF-01 requires the identical success message regardless of match, to avoid leaking which phone/email exists.

**Security:** `security: []`.

---

## 7. `POST /api/v1/users/{id}/reset-password`

| Field   | Value                                                                                                                                                  |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Summary | Admin-triggered fallback reset for another account when the holder cannot access email; temp password is emailed, never shown on screen (UC-03 AF-02). |
| Auth    | **Role-restricted (Admin)**                                                                                                                            |
| Type    | `[ACTION]`                                                                                                                                             |

**Path Parameters**

| Name | Type          | Required | Rule                                   |
| ---- | ------------- | -------- | -------------------------------------- |
| `id` | string (UUID) | Yes      | Target account's `userId`. Must exist. |

**Request Body:** none — UC-03 explicitly states "the Admin fallback takes no new request fields beyond selecting the target user account on SCR-06."

**Success Response** — `200 OK`

```json
{
    "code": 200,
    "message": "A temporary password has been emailed to the account holder.",
    "data": null
}
```

**Error Responses**

| Code | Condition                             |
| ---- | ------------------------------------- |
| 401  | Caller not authenticated.             |
| 403  | Caller authenticated but not `Admin`. |
| 404  | `id` does not match any account.      |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## 8. `GET /api/v1/users/me`

| Field   | Value                                                                                                                                |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| Summary | Read the signed-in user's own full profile — hydrates the session after login/page-reload and prefills the edit form (UC-01, UC-10). |
| Auth    | **Protected** (any authenticated role, own record only — identity from the JWT `sub` claim, no path parameter).                      |
| Type    | `[CRUD]` — `[NEW in v1.1]`                                                                                                           |

**Request:** none (path/query/body).

**Success Response — `UserSelfResponse`** — `200 OK`

```json
{
    "code": 200,
    "message": "Profile retrieved successfully",
    "data": {
        "userId": "3f2a1c90-6b1e-4b2a-9c3d-8f1e2a7b0d11",
        "fullName": "Nguyen Van A",
        "phone": "0901234567",
        "email": "nguyenvana@gmail.com",
        "dateOfBirth": "1990-05-12",
        "role": "PATIENT",
        "status": "ACTIVE",
        "biometricEnabled": false,
        "mustChangePassword": false
    }
}
```

| Field                | Type                               | Notes                                                                                                                                                                                               |
| -------------------- | ---------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `userId`             | string (UUID)                      | Read-only.                                                                                                                                                                                          |
| `fullName`           | string                             |                                                                                                                                                                                                     |
| `phone`              | string                             | Read-only here — immutable via `#9` (UC-10 BR-02).                                                                                                                                                  |
| `email`              | string                             |                                                                                                                                                                                                     |
| `dateOfBirth`        | string (date) \| null              | Shown to the account's own owner regardless of role — the `sensitive_data_rules` hide-from-Admin rule applies only to the _Admin-facing_ DTOs in Module 02, not to a user reading their own record. |
| `role`               | enum                               | Read-only.                                                                                                                                                                                          |
| `status`             | enum `ACTIVE\|LOCKED\|DEACTIVATED` | Read-only — informational (a `LOCKED`/`DEACTIVATED` caller could not have signed in to reach this endpoint in practice).                                                                            |
| `biometricEnabled`   | boolean                            |                                                                                                                                                                                                     |
| `mustChangePassword` | boolean                            |                                                                                                                                                                                                     |

`password_hash` is **never** declared on this DTO (sensitive_data_rules §5).

**Error Responses**

| Code | Condition                     |
| ---- | ----------------------------- |
| 401  | Missing/expired access token. |

**Security:** `bearerAuth: []`. Roles: any authenticated role.

---

## 9. `PATCH /api/v1/users/me`

| Field   | Value                                                                                                                 |
| ------- | --------------------------------------------------------------------------------------------------------------------- |
| Summary | Update the signed-in user's own contact profile (full name, email, date of birth); phone number is immutable (UC-10). |
| Auth    | **Protected** (own record only).                                                                                      |
| Type    | `[CRUD]`                                                                                                              |

**Request Body — `UpdateProfileRequest`** (name reused verbatim from UC-10's own Request Fields table)

| Field         | Type                        | Required | Rule                                                     |
| ------------- | --------------------------- | -------- | -------------------------------------------------------- |
| `fullName`    | string                      | Yes      | Non-empty.                                               |
| `email`       | string                      | No       | Must be a valid email format if provided.                |
| `dateOfBirth` | string (date, `YYYY-MM-DD`) | No       | Must not be in the future (`ck_users_dob`, UC-10 BR-01). |

```json
{
    "fullName": "Nguyen Van A",
    "email": "nguyenvana.new@gmail.com",
    "dateOfBirth": "1990-05-12"
}
```

**Success Response — `UserSelfResponse`** — `200 OK` (same shape as `#8`, reflecting the update).

**Error Responses**

| Code | Condition                                     |
| ---- | --------------------------------------------- |
| 400  | `fullName` empty, `email` malformed.          |
| 401  | Missing/expired access token.                 |
| 422  | `dateOfBirth` is in the future (UC-10 AF-01). |

**Security:** `bearerAuth: []`. Roles: any authenticated role, own record only.

---

## 10. `PATCH /api/v1/users/me/password`

| Field   | Value                                                                            |
| ------- | -------------------------------------------------------------------------------- |
| Summary | Change the signed-in user's own password (current + new + confirmation) (UC-25). |
| Auth    | **Protected** (own record only).                                                 |
| Type    | `[CRUD]`                                                                         |

**Request Body — `ChangePasswordRequest`** (name reused verbatim from UC-25's own Request Fields table)

| Field             | Type   | Required | Rule                                                                                                                                        |
| ----------------- | ------ | -------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `currentPassword` | string | Yes      | Must match the account's current password (UC-25 BR-01).                                                                                    |
| `newPassword`     | string | Yes      | Must satisfy the password policy at TDS §4.3 (not restated here to avoid the two documents drifting apart — see UC-25's own boundary note). |
| `confirmPassword` | string | Yes      | Must equal `newPassword` (UC-25 BR-02).                                                                                                     |

**Success Response** — `200 OK`

```json
{ "code": 200, "message": "Password changed successfully", "data": null }
```

**Error Responses**

| Code | Condition                                                                           |
| ---- | ----------------------------------------------------------------------------------- |
| 400  | Any of the three fields missing.                                                    |
| 401  | Missing/expired access token, or `currentPassword` does not match (UC-25 AF-01).    |
| 422  | `newPassword` fails the policy, or `newPassword` ≠ `confirmPassword` (UC-25 AF-02). |

> `401` is used for `currentPassword` mismatch (an authentication-adjacent failure on the account's own credential) rather than `403`, consistent with how `/auth/login` treats a wrong password. Confirm against the eventual `GlobalExceptionHandler` mapping when implementing — flagged here as a judgment call, not a re-citation of an existing rule.

**Security:** `bearerAuth: []`. Roles: any authenticated role, own record only.

---

## Module 01 Summary

| #   | Method | Endpoint                            | Auth                    | Type   |
| --- | ------ | ----------------------------------- | ----------------------- | ------ |
| 1   | POST   | `/api/v1/auth/login`                | Public                  | ACTION |
| 2   | POST   | `/api/v1/auth/refresh`              | Public                  | ACTION |
| 3   | POST   | `/api/v1/auth/logout`               | Protected               | ACTION |
| 4   | POST   | `/api/v1/auth/biometric-login`      | Public\*                | ACTION |
| 5   | POST   | `/api/v1/auth/biometric-devices`    | Protected               | ACTION |
| 6   | POST   | `/api/v1/password-reset-requests`   | Public                  | ACTION |
| 7   | POST   | `/api/v1/users/{id}/reset-password` | Role-restricted (Admin) | ACTION |
| 8   | GET    | `/api/v1/users/me`                  | Protected               | CRUD   |
| 9   | PATCH  | `/api/v1/users/me`                  | Protected               | CRUD   |
| 10  | PATCH  | `/api/v1/users/me/password`         | Protected               | CRUD   |

\* "Public*" on #4 does not mean unauthenticated/open-to-anyone like #1, #2, #6 — it means no JWT Bearer token is required, but the request body itself carries a different credential (`deviceId` + `biometricAssertion`) that only validates for a device already enrolled via #5. See endpoint #4's own Auth row for the full note.

No endpoint outside the approved catalog was added. Continue to `02_Module02_User_Role_Management_API_Spec.md`.
