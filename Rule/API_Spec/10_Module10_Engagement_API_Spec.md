# ADSUS API Specification — Module 10: Engagement

| Field            | Value                                                                                                                                                                                                                                                                                           |
| ---------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Document version | v0.1 (draft) — 2026-07-30                                                                                                                                                                                                                                                                       |
| Role             | Senior API Architect + Detail Designer                                                                                                                                                                                                                                                          |
| Scope            | Endpoints #57–#63 of `ADSUS_API_Catalog_v1.1.md` (approved catalog — **no endpoint added, removed, or renamed here**) — **final module; completes all 10.**                                                                                                                                     |
| Sources          | `.ai-context/project_context.md` · `.ai-context/api_design_rule/api_design_rules_v0.2.md` · `Reports_md/Report_3.1_UCS_ADSUS.md` (UC-22, UC-23, UC-24, UC-26) · `Documents/02_Requirements/SQL/ADSUS_Physical_Schema_PostgreSQL_v1.sql` (`service_feedbacks`, `blog_posts`, `ai_chat_messages`) |
| Language         | English                                                                                                                                                                                                                                                                                         |

> Same ERD substitution note as prior modules: field types below come from `ADSUS_Physical_Schema_PostgreSQL_v1.sql`, not the Logical ERD.

---

## 0. Flags & Open Issues

| #   | Issue                                                                                                                                                                                                                                                                                                                                                                                                                                               | Where it shows up                     | Recommendation                                                                                                                                                                                                                                                              |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| F1  | Restates and confirms, at the physical-schema level, the gap already noted in the API Catalog's own Method & Boundary Notes: `blog_posts` has exactly `post_id, author_id, title, content, status, published_at, created_at, updated_at` — **no `category`/topic column, and no thumbnail/summary column.**                                                                                                                                         | `#58 category` query param            | Kept accepted but a no-op, same as previously flagged — not re-introduced as new here, just confirmed with the concrete column list.                                                                                                                                        |
| F2  | `blog_posts.title` and `.content` are both `NOT NULL` **at the DB layer, unconditionally** — including for a brand-new `Draft`. UC-24's own boundary note frames non-empty title/content as "a reasonable inference… only a requirement for a thumbnail image" was removed, implying the _hard_ requirement was thought of as Publish-time only; the physical schema is **stricter** than that framing, requiring both fields even to save a Draft. | `#60 CreateBlogPostRequest`           | This spec requires `title`+`content` on `#60` (matching the DB's actual `NOT NULL` columns), not treating them as Draft-optional. Flagging the mismatch with UC-24's looser framing rather than silently picking the lenient reading, which the schema would reject anyway. |
| F3  | UC-26's own `AskChatbotRequest` Request Fields table includes an optional `Context Case ID`, but `ai_chat_messages` has **no** column to store a case reference (`message_id, user_id, role, content, created_at` only).                                                                                                                                                                                                                            | `#62 AskChatbotRequest.contextCaseId` | Same inherited-gap pattern as F1/Module 04's `category`/`code` gaps — kept as a request-only field used to build the LLM prompt context, **never persisted as a link**, since there is nowhere to store it. Confirm at TDS if this needs to become a real column.           |
| F4  | UC-26 AF-02 ("the LLM API service is disrupted") maps to a genuine external-dependency failure, which the already-ratified `api_design_rules_v0.2.md` §9 error table routes to a generic `500` — outside the `400/401/403/404/409/422` set this task asked to enumerate.                                                                                                                                                                            | `#62`                                 | Listed anyway, flagged as a deliberate inclusion — omitting the one case where the external LLM is down would misrepresent the real contract rather than simplify it.                                                                                                       |

---

## 57. `POST /api/v1/service-feedback`

| Field   | Value                                                                |
| ------- | -------------------------------------------------------------------- |
| Summary | Submit a 1–5 star rating with an optional free-text comment (UC-22). |
| Auth    | **Role-restricted (Patient)**                                        |
| Type    | `[CRUD]`                                                             |

**Request Body — `SubmitFeedbackRequest`** (name reused verbatim from UC-22's own Request Fields table)

| Field     | Type    | Required | Rule                                      |
| --------- | ------- | -------- | ----------------------------------------- |
| `rating`  | integer | Yes      | `1`–`5` (`ck_service_feedbacks_rating`).  |
| `comment` | string  | No       | Free text — maps to the `content` column. |

```json
{ "rating": 5, "comment": "The doctor was very attentive" }
```

**Success Response — `ServiceFeedbackResponse`** — `201 Created`

```json
{
    "code": 201,
    "message": "Feedback submitted successfully",
    "data": {
        "feedbackId": "c9d0e1f2-8888-9999-aaaa-bbbbccccdddd",
        "patientProfileId": "a1b2c3d4-1111-2222-3333-444455556666",
        "rating": 5,
        "comment": "The doctor was very attentive",
        "submittedAt": "2026-07-30T19:00:00Z"
    }
}
```

**Error Responses**

| Code | Condition                               |
| ---- | --------------------------------------- |
| 400  | `rating` missing/non-integer.           |
| 401  | Missing/expired access token.           |
| 422  | `rating` outside `1`–`5` (UC-22 AF-01). |

**Security:** `bearerAuth: []`. Roles: **Patient only**.

---

## 58. `GET /api/v1/blog-posts`

| Field   | Value                                                                                                                                                                |
| ------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | List blog posts; a Patient caller sees `PUBLISHED` only, an Admin caller additionally sees `DRAFT` (GB-05) — same endpoint, role-filtered result set (UC-23, UC-24). |
| Auth    | **Protected†** (any authenticated role may call; visible `status` scope depends on the caller's role)                                                                |
| Type    | `[CRUD]`                                                                                                                                                             |

**Query Parameters — `BlogPostSearchCriteria`**

| Field               | Type    | Required | Rule                                                                                       |
| ------------------- | ------- | -------- | ------------------------------------------------------------------------------------------ |
| `category`          | string  | No       | **Not implementable against the current schema — see F1.** Accepted but currently a no-op. |
| `page` / `pageSize` | integer | No       | Default `1` / `20`, max `pageSize` `100`.                                                  |

**Success Response — `PagedResult<BlogPostSummaryResponse>`** — `200 OK`

```json
{
    "code": 200,
    "message": "Blog posts retrieved successfully",
    "data": {
        "items": [
            {
                "postId": "d0e1f2a3-9999-aaaa-bbbb-ccccddddeeee",
                "title": "At-Home Breast Self-Exam Guide",
                "status": "PUBLISHED",
                "publishedAt": "2026-07-15T00:00:00Z"
            }
        ],
        "page": 1,
        "pageSize": 20,
        "totalItems": 1,
        "totalPages": 1
    }
}
```

`BlogPostSummaryResponse.status` is only meaningful/present for an `Admin` caller (who sees both `DRAFT`/`PUBLISHED`); for a `Patient` caller every returned item is implicitly `PUBLISHED`, so the field may be omitted for that view rather than always echoing the same constant. No `summary`/`thumbnail` field exists — see F1; a list-view excerpt, if needed, must be derived client-side from `content`, not a stored column.

**Error Responses**

| Code | Condition                       |
| ---- | ------------------------------- |
| 400  | `page`/`pageSize` out of range. |
| 401  | Missing/expired access token.   |

**Security:** `bearerAuth: []`. Roles: **Patient, Admin** — per UC-23/UC-24's own Allowed Roles sections specifically (not extended to Doctor/Nurse, even though PRD §3.2's Permission Matrix lists "Blog post | View" as Full for every role — the same "per-UC wording takes precedence over the shared Permission Matrix column" precedent already applied elsewhere in the UCS, e.g. UC-18/UC-19 Doctor-only).

---

## 59. `GET /api/v1/blog-posts/{id}`

| Field   | Value                                                                                             |
| ------- | ------------------------------------------------------------------------------------------------- |
| Summary | Read one post's full content, subject to the same Published/Draft visibility rule (UC-23, UC-24). |
| Auth    | **Protected†**                                                                                    |
| Type    | `[CRUD]`                                                                                          |

**Path Parameters**

| Name | Type          | Required | Rule                                                                                                                                                       |
| ---- | ------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `id` | string (UUID) | Yes      | `postId`. For a `Patient` caller, must be `status = PUBLISHED` — otherwise treated as not found (GB-05, same pattern as Module 04's Case-visibility rule). |

**Success Response — `BlogPostResponse`** — `200 OK`

```json
{
    "code": 200,
    "message": "Blog post retrieved successfully",
    "data": {
        "postId": "d0e1f2a3-9999-aaaa-bbbb-ccccddddeeee",
        "authorId": "9a1b2c3d-4e5f-6789-0abc-def123456789",
        "title": "At-Home Breast Self-Exam Guide",
        "content": "Full article body…",
        "status": "PUBLISHED",
        "publishedAt": "2026-07-15T00:00:00Z",
        "createdAt": "2026-07-10T08:00:00Z",
        "updatedAt": "2026-07-15T00:00:00Z"
    }
}
```

`authorId`/`createdAt`/`updatedAt`/`status` (when not trivially `PUBLISHED`) are informational and mainly relevant to an `Admin` caller — no field is hidden from `Patient` here beyond the visibility gate on `id` itself, since a Patient reaching this response already knows the post is Published.

**Error Responses**

| Code | Condition                                                                                                                                                                                       |
| ---- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 401  | Missing/expired access token.                                                                                                                                                                   |
| 404  | `id` does not exist; or (for a `Patient` caller) the post is not `PUBLISHED` — treated identically to not-found, to avoid signaling that a Draft post exists (same GB-05 pattern as Module 04). |

**Security:** `bearerAuth: []`. Roles: **Patient, Admin**.

---

## 60. `POST /api/v1/blog-posts`

| Field   | Value                                        |
| ------- | -------------------------------------------- |
| Summary | Create a new post at status `DRAFT` (UC-24). |
| Auth    | **Role-restricted (Admin)**                  |
| Type    | `[CRUD]`                                     |

**Request Body — `CreateBlogPostRequest`**

> UC-24 names a single merged DTO (`SaveBlogPostRequest`, with an `Action: SaveDraft|Publish` field) for both create and the publish transition; this spec splits it to match the catalog's separate POST/PATCH endpoints, per this task's `Create{Entity}Request`/`Update{Entity}Request` convention (same rationale as Module 04's `CreatePatientProfileRequest`).

| Field     | Type   | Required | Rule                                                                    |
| --------- | ------ | -------- | ----------------------------------------------------------------------- |
| `title`   | string | Yes      | Max 200 chars (`VARCHAR(200)`). **Required even for a Draft — see F2.** |
| `content` | string | Yes      | Non-empty. **Required even for a Draft — see F2.**                      |

```json
{ "title": "At-Home Breast Self-Exam Guide", "content": "Draft body…" }
```

**Success Response — `BlogPostResponse`** — `201 Created` (same shape as `#59`'s response; `status: "DRAFT"`, `publishedAt: null`).

**Error Responses**

| Code | Condition                             |
| ---- | ------------------------------------- |
| 400  | `title`/`content` missing (see F2).   |
| 401  | Missing/expired access token.         |
| 403  | Caller authenticated but not `Admin`. |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## 61. `PATCH /api/v1/blog-posts/{id}`

| Field   | Value                                                                                                                           |
| ------- | ------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Edit content, or publish via `{"status":"PUBLISHED"}` (requires title + content, sets publish date, one-way per GB-01) (UC-24). |
| Auth    | **Role-restricted (Admin)**                                                                                                     |
| Type    | `[CRUD]`                                                                                                                        |

**Path Parameters**

| Name | Type          | Required | Rule                  |
| ---- | ------------- | -------- | --------------------- |
| `id` | string (UUID) | Yes      | `postId`. Must exist. |

**Request Body — `UpdateBlogPostRequest`** — all fields optional; send only what changes.

| Field     | Type             | Required | Rule                                                                                                                                                                                                    |
| --------- | ---------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `title`   | string           | No       | Max 200 chars.                                                                                                                                                                                          |
| `content` | string           | No       |                                                                                                                                                                                                         |
| `status`  | enum `PUBLISHED` | No       | Only valid target value here — one-way `Draft → Published` (GB-01, UC-24 BR-01); no transition back to `Draft`. Setting this also stamps `publishedAt = now()` server-side (`ck_blog_posts_published`). |

```json
{ "status": "PUBLISHED" }
```

**Success Response — `BlogPostResponse`** — `200 OK` (same shape as `#59`'s response, reflecting the update).

**Error Responses**

| Code | Condition                                                                                                                                                                                                                                                                                |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 400  | `status` present but not `PUBLISHED`.                                                                                                                                                                                                                                                    |
| 401  | Missing/expired access token.                                                                                                                                                                                                                                                            |
| 403  | Caller authenticated but not `Admin`.                                                                                                                                                                                                                                                    |
| 404  | `id` does not exist.                                                                                                                                                                                                                                                                     |
| 422  | The post is already `PUBLISHED` (terminal, GB-01 — no re-publish, no revert to Draft); or `status: "PUBLISHED"` is requested while `title`/`content` would be empty (not reachable in practice since both are `NOT NULL` from `#60` onward — see F2 — but kept as the documented guard). |

**Security:** `bearerAuth: []`. Roles: **Admin only**.

---

## 62. `POST /api/v1/chat-messages`

| Field   | Value                                                                                                                                                                     |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Summary | Ask the AI assistant a general health question; forwards context to the external LLM (API-03), appends the mandatory GB-02 disclaimer, and persists the Q&A turn (UC-26). |
| Auth    | **Role-restricted (Patient)**                                                                                                                                             |
| Type    | `[ACTION]`                                                                                                                                                                |

**Request Body — `AskChatbotRequest`** (name reused verbatim from UC-26's own Request Fields table)

| Field           | Type          | Required | Rule                                                                                                                                                |
| --------------- | ------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `message`       | string        | Yes      | The Patient's question. Non-empty.                                                                                                                  |
| `contextCaseId` | string (UUID) | No       | The Confirmed visit's ID, if asking the assistant to explain a result. **Not persisted — see F3; used only to build the LLM prompt for this call.** |

```json
{ "message": "What does BI-RADS 2 on a breast ultrasound mean?" }
```

**Success Response — `ChatMessageResponse`** — `200 OK` — returns the **assistant's reply** (the caller's own question is separately persisted as a `role: "USER"` row, retrievable via `#63`, but is not echoed back in this response body).

```json
{
    "code": 200,
    "message": "Response generated",
    "data": {
        "messageId": "e1f2a3b4-aaaa-bbbb-cccc-ddddeeeeffff",
        "role": "ASSISTANT",
        "content": "BI-RADS 2 means a benign finding… Information provided by the AI Assistant is for general reference only and does not replace the diagnosis and instructions of a specialist Doctor.",
        "createdAt": "2026-07-30T19:10:00Z"
    }
}
```

Even the standard-refusal case (UC-26 AF-01 — the question requests psychological counseling or a new diagnosis) is a **`200 OK`** with a refusal message as `content`, not an error — the assistant always "answers" something, per UC-26's own Main Flow.

**Error Responses**

| Code | Condition                                                                                                                                                                                |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 400  | `message` missing/empty.                                                                                                                                                                 |
| 401  | Missing/expired access token.                                                                                                                                                            |
| 500  | The external LLM service (API-03) is disrupted/times out (UC-26 AF-02) — see F4. The failed turn is **not** saved (neither the `USER` question nor an `ASSISTANT` reply row is written). |

**Security:** `bearerAuth: []`. Roles: **Patient only**.

---

## 63. `GET /api/v1/chat-messages`

| Field   | Value                                                           |
| ------- | --------------------------------------------------------------- |
| Summary | Read the signed-in Patient's own saved AI Chat History (UC-26). |
| Auth    | **Role-restricted (Patient)**                                   |
| Type    | `[CRUD]`                                                        |

**Query Parameters — `ChatMessageSearchCriteria`**

| Field               | Type    | Required | Rule                                      |
| ------------------- | ------- | -------- | ----------------------------------------- |
| `page` / `pageSize` | integer | No       | Default `1` / `20`, max `pageSize` `100`. |

**Success Response — `PagedResult<ChatMessageResponse>`** — `200 OK`, ordered `created_at ASC` (chronological conversation order, per `idx_ai_chat_messages_user_timeline`). Same item shape as `#62`'s response, but includes both `role: "USER"` and `role: "ASSISTANT"` rows interleaved.

```json
{
    "code": 200,
    "message": "Chat history retrieved successfully",
    "data": {
        "items": [
            {
                "messageId": "f2a3b4c5-bbbb-cccc-dddd-eeeeffff0000",
                "role": "USER",
                "content": "What does BI-RADS 2 on a breast ultrasound mean?",
                "createdAt": "2026-07-30T19:09:58Z"
            },
            {
                "messageId": "e1f2a3b4-aaaa-bbbb-cccc-ddddeeeeffff",
                "role": "ASSISTANT",
                "content": "BI-RADS 2 means a benign finding… (disclaimer)",
                "createdAt": "2026-07-30T19:10:00Z"
            }
        ],
        "page": 1,
        "pageSize": 20,
        "totalItems": 2,
        "totalPages": 1
    }
}
```

**Error Responses**

| Code | Condition                       |
| ---- | ------------------------------- |
| 400  | `page`/`pageSize` out of range. |
| 401  | Missing/expired access token.   |

**Security:** `bearerAuth: []`. Roles: **Patient only**, own record (`user_id` scoped from the JWT — `ai_chat_messages` has no `patient_profile_id`, it links directly to `users`, since a chat history is an account-level concept, not tied to any one Patient Profile/Case).

---

## Module 10 Summary

| #   | Method | Endpoint                   | Auth                      | Type   |
| --- | ------ | -------------------------- | ------------------------- | ------ |
| 57  | POST   | `/api/v1/service-feedback` | Role-restricted (Patient) | CRUD   |
| 58  | GET    | `/api/v1/blog-posts`       | Protected†                | CRUD   |
| 59  | GET    | `/api/v1/blog-posts/{id}`  | Protected†                | CRUD   |
| 60  | POST   | `/api/v1/blog-posts`       | Role-restricted (Admin)   | CRUD   |
| 61  | PATCH  | `/api/v1/blog-posts/{id}`  | Role-restricted (Admin)   | CRUD   |
| 62  | POST   | `/api/v1/chat-messages`    | Role-restricted (Patient) | ACTION |
| 63  | GET    | `/api/v1/chat-messages`    | Role-restricted (Patient) | CRUD   |

No endpoint outside the approved catalog was added. **This completes Modules 01–10 (63 endpoints total, matching `ADSUS_API_Catalog_v1.1.md`).**

## Shared DTOs Introduced in Module 10

| DTO                                            | Used by  | Notes                                                                                                               |
| ---------------------------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------- |
| `ServiceFeedbackResponse`                      | #57      |                                                                                                                     |
| `BlogPostResponse` / `BlogPostSummaryResponse` | #58–#61  | Role-based field/status visibility, not role-based access denial.                                                   |
| `ChatMessageResponse`                          | #62, #63 | Same shape for both the single reply (`#62`) and history items (`#63`); `role` distinguishes `USER` vs `ASSISTANT`. |

---

## Cross-Module Flag Register (all 10 modules)

A consolidated pointer to every `F`-numbered flag raised across `01`–`10`, for a single place to triage before implementation:

| Module | Flag          | One-line summary                                                                                                                                                                          |
| ------ | ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 01     | F1–F4         | `NURSE` enum gap (✅ resolved 2026-07-29); no biometric-device table; `PagedResult<T>` vs `PageResponse<T>` naming; JWT expiry TBD.                                                       |
| 02     | F1, F3        | `NURSE` enum gap (✅ resolved, restated); pagination naming (restated).                                                                                                                   |
| 03     | F1–F3         | Chart types unspecified; `scheduleSlotUtilizationRate` self-derived; default date range self-derived.                                                                                     |
| 04     | F1–F5         | `final_diagnosis` vs `doctor_conclusion` ambiguity; Nurse-vs-Doctor `created_by` contradiction; no "patient code" column; PDF binary-response exception; signed-URL inference for images. |
| 05     | F1–F4         | No overall AI confidence column; **missing endpoint for manual Case conclusion after AI rejection**; signed-URL inference for masks; unpaginated list precedent.                          |
| 06     | F1–F2         | No model-artifact-URI column; unpaginated list precedent.                                                                                                                                 |
| 07     | F1–F3         | `schedule_slots` naming collision (prescription intake slots vs. clinic slots); mandatory pagination per §7.1; self-derived query shape for due-doses.                                    |
| 08     | **F1 (high)** | **`slot_status.FULL` vs. UCS's Open/Closed-only decision — unresolved conflict between physical schema comment and dated business decision.**                                             |
| 09     | F1–F2         | Accumulate-vs-overwrite ambiguity for daily health logs; unpaginated list.                                                                                                                |
| 10     | F1–F4         | No blog `category`/thumbnail column; DB stricter than UCS on Draft title/content; no chat `contextCaseId` column; `500` for external LLM failure outside the requested code set.          |

The single item worth escalating before any implementation starts is **Module 08's F1** — every other flag is either a naming/pagination judgment call or a narrow, already-isolated gap.
