# ADSUS API Specification — Module 03: Dashboard & Reporting

| Field            | Value                                                                                                                                    |
| ---------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Document version | v0.1 (draft) — 2026-07-30                                                                                                                |
| Role             | Senior API Architect + Detail Designer                                                                                                   |
| Scope            | Endpoint #16 of `ADSUS_API_Catalog_v1.1.md` (approved catalog — **no endpoint added, removed, or renamed here**)                         |
| Sources          | `.ai-context/project_context.md` · `.ai-context/api_design_rule/api_design_rules_v0.2.md` · `Reports_md/Report_3.1_UCS_ADSUS.md` (UC-05) |
| Language         | English                                                                                                                                  |

> Same ERD substitution note as Modules 01–02: the Logical ERD carries no column-level detail (`project_context.md`'s own boundary rule). FT-10/UC-05 confirm the Dashboard has **no dedicated entity at all** — it is a derived, real-time aggregate over `users`, `cases`, `ai_results`, `appointments`, and `medication_intake_logs` (per `ADSUS_Physical_Schema_PostgreSQL_v1.sql`), so there is no single table to cite for this module's schema.

---

## 0. Flags & Open Issues

| #   | Issue                                                                                                                                                                                                                                       | Where it shows up                               | Recommendation                                                                                                        |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| F1  | UC-05's own boundary note states the chart types (KPI cards, pie/trend charts) are **not specified by the PRD** — a UX choice for FDS.                                                                                                      | Whole endpoint                                  | Response below returns raw numeric metrics only; no chart-rendering concern belongs in the API contract.              |
| F2  | The metric `scheduleSlotUtilizationRate` is flagged by UC-05 itself as **self-derived, not officially required by the PRD** — the underlying data (`schedule_slots.status`) technically supports it, but no FT/GBR names this exact metric. | `data.appointments.scheduleSlotUtilizationRate` | Kept in the schema as optional/nullable; confirm with the PRD owner before treating it as a committed contract field. |
| F3  | No default date-range value is specified by the PRD for FT-10. UC-05's own boundary note proposes "last 30 days" as a self-derived default.                                                                                                 | `from`/`to` query params                        | Documented below as the assumed default — **not a PRD citation** — confirm at FDS/TDS.                                |

---

## 16. `GET /api/v1/dashboard/statistics`

| Field   | Value                                                                                                                                               |
| ------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Aggregated, anonymized operational KPIs (accounts, cases, AI confirm/reject ratio, appointments, adherence rate), filterable by date range (UC-05). |
| Auth    | **Role-restricted (Admin)**                                                                                                                         |
| Type    | `[ACTION]`                                                                                                                                          |

**Query Parameters — `DashboardStatisticsSearchCriteria`**

| Field  | Type                        | Required | Rule                                                                 |
| ------ | --------------------------- | -------- | -------------------------------------------------------------------- |
| `from` | string (date, `YYYY-MM-DD`) | No       | Defaults to 30 days before `to` if omitted (see F3).                 |
| `to`   | string (date, `YYYY-MM-DD`) | No       | Defaults to today if omitted. Must be ≥ `from` if both are provided. |

**Path parameters:** none. **Request Body:** none (read-only lookup, UC-05 BR-02).

**Success Response — `DashboardStatisticsResponse`** — `200 OK`

```json
{
    "code": 200,
    "message": "Dashboard statistics retrieved successfully",
    "data": {
        "dateRange": { "from": "2026-07-01", "to": "2026-07-30" },
        "accounts": {
            "totalAccounts": 142,
            "doctorCount": 12,
            "patientCount": 128,
            "activeCount": 138,
            "lockedOrDeactivatedCount": 4
        },
        "casesAndAi": {
            "totalCases": 310,
            "totalAiRuns": 298,
            "confirmedCount": 271,
            "rejectedCount": 27
        },
        "appointments": {
            "totalBooked": 94,
            "totalCancelled": 11,
            "scheduleSlotUtilizationRate": 0.62
        },
        "adherence": {
            "averageAdherenceRate": 0.81
        }
    }
}
```

| Field group    | Field                                                                                     | Type                 | Notes                                                                                                                                                      |
| -------------- | ----------------------------------------------------------------------------------------- | -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `dateRange`    | `from`, `to`                                                                              | string (date)        | Echoes the effective range actually applied (after defaulting).                                                                                            |
| `accounts`     | `totalAccounts`, `doctorCount`, `patientCount`, `activeCount`, `lockedOrDeactivatedCount` | integer              | `NURSE` not broken out separately until F1 in Module 02 is resolved — counted under a generic total for now.                                               |
| `casesAndAi`   | `totalCases`, `totalAiRuns`, `confirmedCount`, `rejectedCount`                            | integer              | Counts `AiResult` rows by status, per UC-05 Main Flow step 2.                                                                                              |
| `appointments` | `totalBooked`, `totalCancelled`                                                           | integer              | Counts `Appointment` rows by status.                                                                                                                       |
| `appointments` | `scheduleSlotUtilizationRate`                                                             | number (0–1) \| null | Self-derived metric — see F2. Nullable so the field can be withheld without breaking the schema if it is dropped later.                                    |
| `adherence`    | `averageAdherenceRate`                                                                    | number (0–1)         | Clinic-wide average, same formula basis as UC-11's per-patient Adherence Rate (BR-01 there — itself flagged as self-derived, not a PRD-confirmed formula). |

BR-01 (UC-05): this endpoint never returns any patient's name, phone number, or other identifying data — every figure above is a count/ratio, never a per-patient row.

**Error Responses**

| Code | Condition                              |
| ---- | -------------------------------------- |
| 400  | `from`/`to` malformed, or `from > to`. |
| 401  | Missing/expired access token.          |
| 403  | Caller authenticated but not `Admin`.  |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## Module 03 Summary

| #   | Method | Endpoint                       | Auth                    | Type   |
| --- | ------ | ------------------------------ | ----------------------- | ------ |
| 16  | GET    | `/api/v1/dashboard/statistics` | Role-restricted (Admin) | ACTION |

No endpoint outside the approved catalog was added. Continue to `04_Module04_Medical_Record_API_Spec.md`.
