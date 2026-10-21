# Cosmix frontend

- This is the React web frontend, not the .NET backend or React Native mobile app.
- Read README.md, docs/Architecture.md and the relevant module README before editing.
- Work in one feature/module. Keep controllers, EF entities/migrations and business policy calculations in the backend.
- Use the existing React, TypeScript, React Router and TanStack Query patterns. Do not add Redux, microfrontends or a second UI framework without a concrete requirement.
- Shared UI goes in src/components. Feature-only UI stays in src/modules/<module>/components. Avoid cross-module deep imports.
- Do not invent employee fields, permission names, salary formulas, approval chains or integration endpoints. Use the approved SRS/backend contracts.
- The PM approved global web login and the HR workspace in docs/HR-Web-Design.md and docs/HR-Web-Implementation-Plan.md, then authorized Admin hiring/change/salary/status review on 2026-10-08. See docs/HR-Approval-Completion-Plan.md for that extension. Other module folders remain architecture-only. Do not add demo/preview routes, mock dashboards, placeholder business screens or screenshot/artifact folders without an explicit request.
- Follow docs/Global-Web-Login.md and docs/Backend-Integration.md for cookie/CSRF web authentication. Never fabricate tokens, sample users, a localStorage login or an authorization bypass. Live SMS/account acceptance requires approved backend configuration.
- Server permissions and record/field scopes are authoritative. UI permission checks are only presentation.
- Never commit credentials, production employee data, face images/templates, bank/medical records or tokens. All VITE_ variables are public.
- Automated test projects/files are not requested. Do not add them without PM approval. Run lint, formatting, type/build checks and relevant manual/browser verification. Report missing regression coverage honestly.
- Do not push, create a remote repository, deploy or change the backend without authorization.
- Run npm run verify before handoff. Preserve package-lock.json and document actual checks.
