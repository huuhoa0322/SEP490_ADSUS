# ADSUS API Specification — Consolidated Flags & Open Issues (Modules 01–10)

| Field | Value |
|---|---|
| Document version | v0.2 — 2026-07-30 (added precise Report 3.0/3.1 references for cross-checking) |
| Purpose | Single tracking checklist for every `F`-numbered flag raised across `01_Module01_...` through `10_Module10_...`, with the exact source location in Report 3.0 (PRD) / Report 3.1 (UCS) so each item can be cross-checked and, where applicable, corrected at the source. |
| Scope | 63 endpoints, 10 modules, all approved by `ADSUS_API_Catalog_v1.1.md` |
| Status column | `Open` (default) — update to `Resolved`/`Won't fix`/`Deferred` as each item is triaged |

> This file does not introduce any new finding — it consolidates the "0. Flags & Open Issues" section already present at the top of each of the 10 module spec files, and adds one thing those files didn't have: the **exact PRD/UCS section this item traces back to**, verified by re-reading `Report_3.0_PRD_ADSUS_md.md` §2.2 directly (not from memory).

---

## Reading guide — which items are actually Report 3.0/3.1 candidates

Not every flag below is fixable by editing Report 3.0/3.1. Three different root causes got mixed together across the 10 module files:

| Root cause | Meaning | Where the fix belongs |
|---|---|---|
| **(A) PRD/UCS content issue** | The PRD entity list, permission matrix, or a UC's own text is missing something, self-contradicts, or conflicts with another UC. | **Report 3.0 and/or 3.1 — these are the ones you asked about.** |
| **(B) Physical schema issue** | `ADSUS_Physical_Schema_PostgreSQL_v1.sql` disagrees with, or fails to implement, something PRD/UCS already says correctly. | The SQL schema / TDS — editing Report 3.0/3.1 would not fix this. |
| **(C) L2/task-instruction issue** | Conflict between `api_design_rules_v0.2.md` and this task's own instructions, or a judgment call made while writing the spec. | `api_design_rules_v0.2.md` or a design decision — not Report 3.0/3.1 at all. |

The table below tags every row with **(A)**, **(B)**, or **(C)** so you know at a glance whether Report 3.0/3.1 is even the right place to look.

---

## 🔴 High priority — blocks implementation

| # | Module | Flag ID | Root cause | Report 3.0/3.1 reference | Issue | Status |
|---|---|---|---|---|---|---|
| 1 | 08 | F1 | **(A) + (B)** — PRD/UCS agree with each other but the DB disagrees with both | **Report 3.0 §2.2.k** Entity: Schedule Slot — "Key business attributes: **Date, start/end time, status**" (no Capacity attribute at all). **Report 3.1**, Conventions & Notation → "Schedule Slot status workflow" — "_(Decision resolved 2026-07-23)_: PRD Entity Schedule Slot (§2.2.k) lists only 3 attributes… no Capacity field… a slot never reaches a 'full' condition" + UC-13 Main Flow boundary note (confirmed 2026-07-23) removing the old "slot just became full" AF. | Physical `schedule_slots.status` enum still has `FULL`, and the table's own DB comment describes an app-level "1 patient per slot, auto-`FULL`" behavior — contradicting **both** PRD §2.2.k and the UCS's own dated decision. **This one is NOT a Report 3.0/3.1 defect** — both reports already agree with each other (no Capacity/Full). The physical SQL schema is the outlier and needs correcting (or someone needs to re-open the 2026-07-23 decision and update both reports again, which would then require re-editing Report 3.1). | Open |
| 2 | 05 | F2 | **(A)** — a real UCS gap | **Report 3.1**, UC-19 "AI-assisted diagnosis & prognosis tracking" → **Alternative Flows, AF-01**: "…The Doctor then either records a manual diagnostic conclusion for the Case directly (moving the Case to Confirmed with that manual conclusion) or re-runs the analysis…" | UC-19 AF-01 describes a Doctor action ("record a manual conclusion directly on the Case") that has no corresponding UC-ID, FT-ID, or Request Fields anywhere in Report 3.1 — it's a sentence describing behavior with no formal spec backing it, which is why the API Catalog never got an endpoint for it either. **This is a Report 3.1 gap** — UC-19 should either get a new Business Rule/Request Fields entry for this manual-override action, or reference a distinct UC. | Open |

## 🟠 Medium priority — data/business-rule contradictions needing a decision

| # | Module | Flag ID | Root cause | Report 3.0/3.1 reference | Issue | Status |
|---|---|---|---|---|---|---|
| 3 | 04 | F1 | **(B)** — schema invents an attribute PRD never listed | **Report 3.0 §2.2.c** Entity: Case — "Key business attributes: Visit date, clinical information, **final diagnosis**, status" (only one conclusion-like attribute is named — no second "doctor conclusion" attribute anywhere in Report 3.0 or 3.1). | The physical `cases` table has **two** overlapping text columns (`final_diagnosis`, documented; `doctor_conclusion`, undocumented) where the PRD names only one attribute. **Not a Report defect** — Report 3.0 is actually unambiguous here (one attribute, "final diagnosis"); the DB schema added an extra column with no PRD/UCS grounding. Fix at the schema layer, not the Report. | Open |
| 4 | 04 | F2 | **(A)** — a genuine internal PRD self-contradiction, compounded by a UCS extension | **Report 3.0 §2.2.b** Entity: Patient Profile — Purpose: "…created and managed **by a Doctor**…" (singular, Doctor only) — **vs. Report 3.0 §3.2** Permission Matrix, row "Patient profile \| Create / Edit": `Full` under the shared **Doctor/Nurse** column — **vs. Report 3.1 UC-06** Allowed Roles: "**Doctor, Nurse**. Both may view and update a patient's baseline medical profile." | Report 3.0 contradicts **itself**: §2.2.b's entity purpose text says Doctor only, while §3.2's permission matrix (and Report 3.1's UC-06, which followed §3.2) say Doctor **and** Nurse. The physical DB comment picked up §2.2.b's narrower wording. **This is a genuine Report 3.0 internal inconsistency (§2.2.b vs §3.2) worth fixing** — recommend updating §2.2.b's Purpose text to say "Doctor or Nurse" to match §3.2/UC-06, since the Nurse-inclusive reading is the one already implemented in 2 other places. | Open |
| 5 | 10 | F2 | **(A)** — UCS's own boundary note underestimates a PRD-derived constraint | **Report 3.0 §2.2.o** Entity: Blog Post — "Key business attributes: **Title, content**, status, publish date" (both listed as core attributes, no "required only at Publish" qualifier). **Report 3.1 UC-24** boundary note: "…requiring non-empty Title/Content is a reasonable inference (they are the entity's core attributes already), not a requirement for a thumbnail image." | UC-24's own text already flags this as "a reasonable inference," but frames it softly — the physical schema goes further and makes both columns hard `NOT NULL`, including for a Draft. **Minor Report 3.1 wording gap**, not a contradiction: UC-24 should state explicitly whether a Draft can ever have empty title/content, rather than leaving it as an "inference." | Open |

## 🟡 Low priority — data-model gaps inherited from the UCS/PRD (not introduced by this spec)

| # | Module | Flag ID | Root cause | Report 3.0/3.1 reference | Issue | Status |
|---|---|---|---|---|---|---|
| 6 | 04 | F3 | **(A)** — UCS invents a field with no PRD entity backing | **Report 3.1 UC-09** Request Fields — "Search keyword… Searches by Name, Phone number, **or Patient code**." — **vs. Report 3.0 §2.2.a** Entity User (no code/MRN attribute) and no `patient_profiles` equivalent anywhere in §2.2. | UC-09 names a search field ("Patient code") for an attribute that doesn't exist on **any** entity in Report 3.0 §2.2. **This is a Report 3.1 gap** — either remove "Patient code" from UC-09's Request Fields, or add a `code`/MRN attribute to the User or Patient Profile entity in Report 3.0 §2.2 first. | Open |
| 7 | 10 | F1 | **(A)** — same pattern as #6 | **Report 3.1 UC-23** Request Fields — "Category / Topic… Filters posts by topic." — **vs. Report 3.0 §2.2.o** Entity Blog Post (Title, content, status, publish date — no category/topic attribute). | Same shape of gap as #6, different UC. **Report 3.1 fix**: remove the field from UC-23, or add a `category` attribute to §2.2.o in Report 3.0 first. | Open |
| 8 | 10 | F3 | **(A)** — same pattern again | **Report 3.1 UC-26** Request Fields — "Context Case ID… if asking the AI to explain a result." — **vs. Report 3.0 §2.2.r** Entity AI Chat History ("Role, content, timestamp" only — no case reference attribute). | Same shape of gap a third time. **Report 3.1 fix**: either UC-26 drops this field, or §2.2.r in Report 3.0 gains a case-reference attribute. | Open |
| 9 | 05 | F1 | **(B)** — PRD asks for this, the schema just doesn't have it | **Report 3.0 §2.2.e** Entity: AI Result — "Key business attributes: Overall AI prediction, **confidence score**, review status, confirming doctor & timestamp, doctor's note." | **Important nuance versus what the module file says:** Report 3.0 §2.2.e *does* list an overall "confidence score" as an AI Result-level attribute — it is **not** a gap in the PRD. The physical `ai_results` table simply never implemented this column (only per-finding `ai_findings.confidence` exists). **Not a Report defect — a schema implementation gap.** Recommend adding an `overall_confidence` (or similar) column to `ai_results` to match §2.2.e, rather than changing Report 3.0. | Open |
| 10 | 06 | F1 | **(A)** — UCS invents a field with no PRD entity backing | **Report 3.1 UC-20** Request Fields — "Model Artifact URI… The path to the trained model file." — **vs. Report 3.0 §2.2.g** Entity AI Model Version ("Version code, description, evaluation metrics (Sensitivity, Accuracy, AUC), status" — no artifact/path attribute). UC-20's own boundary note already flags this itself: "there is NO field for the model file's path/URI… to be confirmed when writing the TDS." | Already self-flagged inside Report 3.1 — **not a hidden gap**, just carried forward here. If the team decides the model needs a tracked file path, add it to §2.2.g in Report 3.0 first. | Open |

## ⚪ Naming / design conventions — not Report 3.0/3.1 issues at all

| # | Module | Flag ID | Root cause | Reference | Issue | Status |
|---|---|---|---|---|---|---|
| 11 | 01 (+02) | F3 | **(C)** | `api_design_rules_v0.2.md` §7.1 (`PagedResult<T>`) vs. this task's own instructions (`PageResponse<T>`) | Naming conflict lives entirely between the L2 API-rules document and this task's prompt — **no Report 3.0/3.1 section is involved.** Resolve directly in `api_design_rules_v0.2.md` or by choosing a name for this task. | Open |
| 12 | 07 | F1 | **(C)** | Report 3.0 §2.2.i (Prescription Item: "daily intake slots") uses "intake slots" wording, not "schedule slots" — the collision is purely a physical-schema column-naming accident (`prescription_items.schedule_slots`), not a Report wording problem. | Not a Report issue — PRD/UCS wording is actually fine ("intake slots"); only the DB column name collides with the unrelated `schedule_slots` table. Fix at the schema/EF Core layer. | Open |
| 13 | 04, 05, 06, 09 | (unpaginated lists) | **(C)** | `api_design_rules_v0.2.md` §7.1 wording ("required on every list endpoint") | Judgment call made while writing the spec, not a Report 3.0/3.1 matter. | Open |

## 🔧 Infrastructure / technical gaps — not Report 3.0/3.1 issues

| # | Module | Flag ID | Root cause | Reference | Issue | Status |
|---|---|---|---|---|---|---|
| 14 | 01 | F2 | **(B)** | Report 3.1 UC-02 BR-01 (pairing requirement) is fine on its own terms; its own text already says "the concrete mechanism… is a TDS/FDS concern, intentionally not specified here." | No table backs biometric device enrollment — **UC-02 already scoped this out of the UCS on purpose.** Confirm the storage model at TDS, not in Report 3.0/3.1. | Open |
| 15 | 01 | F4 | **(C)** | `api_design_rules_v0.2.md` §6.1 (marked TBD there, not in either Report) | JWT expiry — an L2/TDS matter. | Open |
| 16 | 04 | F5 | **(B)** | Report 3.0 §6.4 Technical Assumptions — "Ultrasound images and user-uploaded files… are stored on Supabase Storage… see 02_TDS_ADSUS.md §1 for the access pattern (signed URL, private buckets)." | Report 3.0 §6.4 already establishes the signed-URL pattern in principle — this spec's `imageUrl` field is a direct, consistent application of it. **Not a Report defect**, just needs TDS §1 confirmation of the exact mechanism. | Open |
| 17 | 05 | F3 | **(B)** | Same as #16, applied to `ai_findings.mask_ref`. | Same as #16. | Open |
| 18 | 04 | F4 | **(C)** | `api_design_rules_v0.2.md` §4 (response envelope rule) | API-design-layer accommodation for a binary file response — UC-12 itself never specifies HTTP-level contract details (out of UCS scope by its own template convention). Not a Report matter. | Open |
| 19 | 10 | F4 | **(B)** | Report 3.1 UC-26 AF-02: "the connection to API-03 fails or times out… does not save the failed turn." | UC-26 already anticipates this failure mode narratively; mapping it to HTTP `500` is a technical detail beneath the UCS's abstraction level, not a defect in it. | Open |

---

## Summary — items to actually take back to Report 3.0/3.1

Of the 19 tracked items, only **6 are genuine Report 3.0/3.1 candidates** (root cause **(A)**):

| # | Report | Section(s) | What to check |
|---|---|---|---|
| 2 | Report 3.1 | UC-19 Alternative Flows AF-01 | Add formal Business Rule/Request Fields backing for the "manual conclusion after AI rejection" action, or a new UC-ID for it. |
| 4 | Report 3.0 | §2.2.b vs §3.2 | Resolve the Doctor-only vs. Doctor/Nurse self-contradiction for Patient Profile creation. |
| 5 | Report 3.1 | UC-24 boundary note | State explicitly whether a Draft can have empty title/content, instead of leaving it as an inference. |
| 6 | Report 3.1 | UC-09 Request Fields | Remove "Patient code," or add the attribute to a Report 3.0 §2.2 entity first. |
| 7 | Report 3.1 | UC-23 Request Fields | Remove "Category/Topic," or add the attribute to Report 3.0 §2.2.o first. |
| 8 | Report 3.1 | UC-26 Request Fields | Remove "Context Case ID," or add the attribute to Report 3.0 §2.2.r first. |
| 10 | Report 3.1 | UC-20 (already self-flagged) | Already known/self-flagged in Report 3.1 — no new action beyond what UC-20 itself already says. |

The remaining 12 items are physical-schema gaps (root cause **(B)**) or L2-doc/task-instruction/design-judgment matters (root cause **(C)**) — editing Report 3.0/3.1 would not resolve those; they need schema changes or `api_design_rules_v0.2.md`/TDS decisions instead. Item #1 (Module 08, the highest-priority flag overall) is the one place where **Report 3.0 and 3.1 already agree with each other** — the physical schema is the actual outlier, not either Report.

Source files: `01_Module01_Authentication_Account_API_Spec.md` · `02_Module02_User_Role_Management_API_Spec.md` · `03_Module03_Dashboard_Reporting_API_Spec.md` · `04_Module04_Medical_Record_API_Spec.md` · `05_Module05_AI_Diagnosis_Core_API_Spec.md` · `06_Module06_AI_Model_Management_API_Spec.md` · `07_Module07_Prescription_Adherence_API_Spec.md` · `08_Module08_Appointment_Scheduling_API_Spec.md` · `09_Module09_Health_Monitoring_API_Spec.md` · `10_Module10_Engagement_API_Spec.md` · `Reports_md/Report_3.0_PRD_ADSUS_md.md` · `Reports_md/Report_3.1_UCS_ADSUS.md`.
