# Cosmix Mobile

- Current owner-approved feature scope is recorded in docs/Product-Direction.md, docs/Employee-Attendance-Implementation.md and docs/Employee-Self-Service.md. The 20 October 2026 login/dashboard/attendance and employee self-service requests supersede the historical architecture-only restriction; all other architecture/security boundaries still apply.

- Read README.md before working. This is the separate React Native mobile project, not the web frontend.
- Implement only approved real features. Do not add mock screens, placeholder dashboards, demo accounts or a permanent test suite without an explicit request. Keep generated screenshots and artifacts outside Git.
- Do not create employee/customer/client-specific application trees. Keep the generic shared structure. Roles control approved access, not duplicate source folders.
- App.tsx delegates to src/app/App.tsx. Preserve the shared composition and generic folder layout; empty foundation folders may remain.
- Keep screens in src/screens, shared controls in src/components, navigation in src/navigation and integration adapters in src/services.
- Do not add state, navigation, API, storage, camera or location libraries until an approved feature requires them.
- .NET must enforce permissions, record scopes and business rules. Mobile route visibility is not security enforcement.
- Do not invent business fields, API contracts, approval chains, attendance calculations or device permissions.
- Never commit production data, tokens or signing secrets. No fake authentication, attendance or identity checks.
- React Native cannot treat .env values or application bundles as secret storage.
- Preserve the existing web, backend and reference mockup projects. No deployment, Git initialization or push without authorization.
- The owner reauthorized builds, app/emulator execution and testing on 20 October 2026 after the code-only phase. Run npm run verify and appropriate focused checks. Keep evidence outside Git; distinguish synthetic checks, build results and real employee/device acceptance.
