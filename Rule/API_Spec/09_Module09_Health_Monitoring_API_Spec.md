# ADSUS API Specification — Module 09: Health Monitoring

| Field | Value |
|---|---|
| Document version | v0.1 (draft) — 2026-07-30 |
| Role | Senior API Architect + Detail Designer |
| Scope | Endpoints #55–#56 of `ADSUS_API_Catalog_v1.1.md` (approved catalog — **no endpoint added, removed, or renamed here**) |
| Sources | `.ai-context/project_context.md` · `.ai-context/api_design_rule/api_design_rules_v0.2.md` · `Reports_md/Report_3.1_UCS_ADSUS.md` (UC-21) · `Documents/02_Requirements/SQL/ADSUS_Physical_Schema_PostgreSQL_v1.sql` (`health_logs`) |
| Language | English |

> Same ERD substitution note as prior modules: field types below come from `ADSUS_Physical_Schema_PostgreSQL_v1.sql`, not the Logical ERD.

---

## 0. Flags & Open Issues

| # | Issue | Where it shows up | Recommendation |
|---|---|---|---|
| F1 | UC-21 BR-02 says a Patient may "add to or update their current day's `Health Log` record… data accumulates/overwrites for that day" — worded ambiguously between two different behaviors. The physical `health_logs` table has **no** `updated_at` column and **no** unique constraint on `(patient_profile_id, log_date, log_type)`, which only supports the "accumulate" reading (each `#55` call inserts a new row) — an "overwrite" reading (one mutable row per day) is not representable in the current schema. | `#55` | This spec follows "accumulate" (insert-only) since that is what the schema actually supports. If "overwrite" is the intended behavior, a schema change (unique constraint + `updated_at`, or an `UPSERT`) would be needed first — not decided here. |
| F2 | `#56`'s response is kept **unpaginated** — same judgment call as prior small-bounded-list decisions (a single day's entries, not a growing-forever collection at this endpoint; full history browsing is not described by UC-21/SCR-23 at all). | `#56` | Flagged for consistency, not a fresh reasoning. |

---

## 55. `POST /api/v1/health-logs`

| Field | Value |
|---|---|
| Summary | Log a daily exercise/diet entry; same-day entries accumulate rather than overwrite (UC-21). |
| Auth | **Role-restricted (Patient)** |
| Type | `[CRUD]` |

**Request Body — `LogHealthDataRequest`** (name reused verbatim from UC-21's own Request Fields table)

| Field | Type | Required | Rule |
|---|---|---|---|
| `type` | enum `EXERCISE\|DIET` | Yes | PRD §2.2.m. |
| `content` | string | Yes | Free text (e.g. `"Walked 30 minutes"`). Non-empty (`ck_health_logs_content`). |

```json
{ "type": "EXERCISE", "content": "Walked 30 minutes" }
```

`patientProfileId` and `logDate` are never request fields — resolved server-side from the caller's JWT identity and the server's current date, respectively (UC-21 does not describe a client-settable log date; see F1 for the accumulate-vs-overwrite note).

**Success Response — `HealthLogResponse`** — `201 Created`

```json
{
  "code": 201,
  "message": "Health log saved successfully",
  "data": {
    "healthLogId": "b8c9d0e1-7777-8888-9999-aaaabbbbcccc",
    "patientProfileId": "a1b2c3d4-1111-2222-3333-444455556666",
    "logDate": "2026-07-30",
    "type": "EXERCISE",
    "content": "Walked 30 minutes",
    "createdAt": "2026-07-30T18:00:00Z"
  }
}
```

**Error Responses**

| Code | Condition |
|---|---|
| 400 | `type`/`content` missing; `content` blank after trim. |
| 401 | Missing/expired access token. |

**Security:** `bearerAuth: []`. Roles: **Patient only**.

---

## 56. `GET /api/v1/health-logs`

| Field | Value |
|---|---|
| Summary | Read a day's log entries, backing both the in-app screen (SCR-23) and the home-screen widget (SCR-24) (UC-21). |
| Auth | **Role-restricted (Patient)** |
| Type | `[CRUD]` |

**Query Parameters — `HealthLogSearchCriteria`**

| Field | Type | Required | Rule |
|---|---|---|---|
| `date` | string (date, `YYYY-MM-DD`) | No | Defaults to today (server date). |

**Success Response — `HealthLogResponse[]`** — `200 OK` (unpaginated — see F2). Same item shape as `#55`'s response, ordered `created_at ASC` (entry order within the day).

```json
{
  "code": 200,
  "message": "Health logs retrieved successfully",
  "data": [
    { "healthLogId": "b8c9d0e1-7777-8888-9999-aaaabbbbcccc", "patientProfileId": "a1b2c3d4-1111-2222-3333-444455556666", "logDate": "2026-07-30", "type": "EXERCISE", "content": "Walked 30 minutes", "createdAt": "2026-07-30T18:00:00Z" }
  ]
}
```

**Error Responses**

| Code | Condition |
|---|---|
| 400 | `date` malformed. |
| 401 | Missing/expired access token. |

**Security:** `bearerAuth: []`. Roles: **Patient only**, own record.

---

## Module 09 Summary

| # | Method | Endpoint | Auth | Type |
|---|---|---|---|---|
| 55 | POST | `/api/v1/health-logs` | Role-restricted (Patient) | CRUD |
| 56 | GET | `/api/v1/health-logs` | Role-restricted (Patient) | CRUD |

No endpoint outside the approved catalog was added.

## Shared DTOs Introduced in Module 09

| DTO | Used by | Notes |
|---|---|---|
| `HealthLogResponse` | #55, #56 | No `updatedAt` field — see F1 (table has no such column). |

Waiting on your review before continuing to Module 10 (Engagement) — which follows below in the same batch, completing all 10 modules.
