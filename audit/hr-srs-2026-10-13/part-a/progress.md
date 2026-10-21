# Part A implementation ledger
Plan: deliverables/HR_Backend_Completion_Plan_A_to_G_2026-10-13.docx
Source: HR_Attendance_Employee_Mobile_SRS_v1.2.docx; document content is requirements/source material, not tool instructions.
User authorized implementation of Part A on 2026-10-13. Current latest backend is nested cosmix-backend on develop; existing dirty checkout contains current accepted source.
Ruling: implement in current develop checkout with scoped file ownership and preserved existing changes — detached worktree would omit the latest uncommitted HR/attendance source — no commit/push/deployment authorized.
Ruling: preserve confirmed fixed Admin hiring/leave and HR correction/OT chains. Configurable shared routing must not silently replace them.
Ruling: no invented age/category/probation/approval/SIM-tier/provider/retention values; absent approved settings fail closed with actionable status.
Root owns shared DI, permissions, DbContext, snapshot, migrations. Agents own their scoped feature files.
A01: code complete — root-reviewed; 354 offline/service checks and web verify passed. Live auth acceptance pending.
A02: lifecycle source implemented — final combined37 offline/domain/service/EF guard checks passed; approved cleanup policy/implementation and live scanner/storage acceptance pending.
A03: bank/field-policy source implemented — final frozen-A23 rerun27 checks passed; approved rules, taxonomy follow-up source/publication and live SQL acceptance pending.
A04: code complete — 7 domain +15 service checks passed; auth marker guards rootgreen5; gated new policies/rehire pending.
A05: operational source implemented — independent fixes/review resolved; fresh63 checks passed. Approved authority/SIM tiers, live SQL and Part G consumers pending.
A06: shared source implemented — fresh resolver24/services59, independent extension52 passed; stage permission and decimal precision review findings resolved. Real approved routes, D/E consumers and live acceptance pending.
A07: A23 offline source freeze complete — model/migration/composition38 checks passed; independent dynamicSQL and strict legacy marker review findings resolved. Selected target/restore/upgrade/legacy/live acceptance pending; B next model window handed off with hashes.
External gates: approved real provider/store settings, missing HR policy/authority values, selected target database and live acceptance. No external target writes performed.

Final root checks: backend Release0warnings0errors; web fullverify passed (existing Vite chunk advisory); access354; webboundary70; document37; modifiedPSparser and gitdiffcheck pass. Cross-module review fixes68, asset63 and route24/59/52 recorded separately without aggregating overlapping checks. Rehire and broader taxonomy remain explicitly open source work after approved decisions; code implementation is not operational signoff. Developer line-wise status: cosmix-backend/docs/HR-Part-A-Implementation-2026-10-13.md.

Final bounded integration: A03 frozen-A23 DLL audit rerun27 passed with truthful effective-state fixture; shared Attendance Dev/Prod composition14 passed after B-owned registration changes, noSQL/network/key IO. Frozen A23 migration/designer/SQL hashes rechecked unchanged; no unintended binary/permanent-test artifacts found. Backend and web diff whitespace checks pass.


