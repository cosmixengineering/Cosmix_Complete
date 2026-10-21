# Authentication Cancellation and Error Disclosure

Date: 15 October 2026. Scoped source handoff; not runtime or SQL acceptance.

## Implemented Change

`src/Cosmix.Api/Middleware/ApiExceptionHandler.cs` now includes public error detail
only for `RequestValidationException` and `ResourceNotFoundException`. Unexpected
500 errors, database errors, request-size failures and authentication failures do
not copy the raw exception message into the response detail. Existing status/title
mappings and the private server log retaining the exception, type, trace and message
are preserved. The composition root still supplies the public `traceId`.

No cancellation catch, timeout increase, authentication shortcut, session/MFA change
or shared startup change was added.

## Cancellation Diagnosis

The reported stack matches the current call chain:

1. `AuthenticationExtensions.OnTokenValidated` passes `HttpContext.RequestAborted`
   into browser session validation.
2. `WebAuthenticationService.ValidateSessionCoreAsync` passes that token into the
   session and account reads; `SessionIsValidAsync` passes it into the consumed
   SMS-proof read. The reported lines 195/197 are the session/account awaits.
3. `AuthenticationRepository` supplies the token to EF asynchronous queries.
4. The validated identity is installed only after those reads and eligibility,
   version, browser-binding and MFA checks complete.

No local timeout or cancellation-to-success catch exists on that path. The reported
30-second client timeout and repeated Visual Studio pauses are consistent with
request cancellation propagating through the awaits. They do not independently
prove what caused the first pause or exclude slow SQL. At the exception, inspect
`RequestAborted.IsCancellationRequested` and the propagated token before classifying
it as a client abort. Do not disclose cookies, JWTs, credentials or connection strings.

The parent reported a credential-free `/health/live` timeout. That endpoint excludes
dependency checks. Authentication middleware precedes it, so a request carrying a
valid access cookie can still trigger session reads, even though the endpoint allows
anonymous access. A credential-free liveness timeout supports a process-wide pause
or stall; it does not establish a session-query defect.

ASP.NET Core 10's existing exception middleware handles an
`OperationCanceledException` with an aborted request before custom exception handlers,
using status 499 when the response has not started. An unrelated cancellation on a
live request is not the same condition. See the
[framework implementation](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs).
Visual Studio can pause before any exception handler runs; exception-specific
break settings are described in
[Microsoft's debugger guidance](https://learn.microsoft.com/en-us/visualstudio/debugger/managing-exceptions-with-the-debugger?view=visualstudio).
No debugger settings were changed by this work.

## Standalone Regression Sources

- `VerifyApiExceptionDisclosure.cs` invokes the real handler and JSON problem writer.
  A private exception sentinel must not appear in unexpected/database/oversized-request
  responses; known validation/not-found detail remains. It checks the existing status
  mappings, server-owned trace in JSON and the correlation middleware's header, and
  retention of the original exception and matching trace in the private logger.
  Cancellation without `RequestAborted` remains a sanitized 500. The response transport
  and log capture are in memory; OnStarting callbacks are explicitly dispatched because
  no HTTP server is started. The problem customization matches Program.cs but does not
  execute the composition root.
- `VerifyAuthenticationCancellation.cs` executes the real web validator with in-memory
  domain objects and controlled cancellable repository reads. It covers browser and
  server-actor cancellation at the session/account/MFA-proof boundaries, preservation
  of the original cancellation token, no subsequent reads, and unchanged session/proof
  state. Fresh requests still validate the consumed MFA evidence. Actual configured JWT
  callbacks reject cancelled reads without installing an identity; valid, revoked,
  stale-version, missing-proof and wrong-browser controls exercise the same service.
  One in-memory middleware pipeline characterizes the framework's existing 499 behavior.
  Credential issuance, delivery and repository writes throw if unexpectedly invoked.

These are .NET 10 standalone `.cs` checks, not test projects. They do not validate
signed JWT cryptography, EF/SQL cancellation, real-browser disconnects, providers,
deployment configuration, or authenticated HTTP/SQL acceptance. They do not start a
host, open a SQL connection, write fixtures/private storage, or load private settings.

## Parent Verification

Source review and scoped whitespace checks were performed. Neither new source was
compiled or executed by this agent. No red/green result, assertion total, current
Release pass or resolution of the reported Visual Studio incident is claimed.
The parent owns builds and shared build outputs.

After the parent builds the normal Release outputs, run these sequentially from
`C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend`:

```powershell
dotnet run --file .\docs\access-provisioning\VerifyApiExceptionDisclosure.cs --configuration Release --property:BuildProjectReferences=false --property:NuGetAudit=false --property:UseSharedCompilation=false -tl:off
dotnet run --file .\docs\access-provisioning\VerifyAuthenticationCancellation.cs --configuration Release --property:BuildProjectReferences=false --property:NuGetAudit=false --property:UseSharedCompilation=false -tl:off
dotnet run --file .\docs\access-provisioning\VerifyWebBoundary.cs --configuration Release --property:BuildProjectReferences=false --property:NuGetAudit=false --property:UseSharedCompilation=false -tl:off
```

`BuildProjectReferences=false` requires already-built matching Release references;
these commands still compile the standalone programs. For a parent-selected frozen
Release directory, the existing `docs/attendance-gap-c/VerifyManaged.ps1` accepts the
same three source paths, strips file-app directives and compiles against that frozen
output without rebuilding application projects. That runner was read, not edited
or invoked here.

## A02 Boundary

The parent reported that the exact read-only A02 preflight passed and the user declined
execution. This work does not authorize or perform setup/auth execution, SQL/private
writes or hosts. No packet/script was edited, invoked or regenerated. The handler
source change means historical packet source hashes must not be presented as matching
the current source. Current A02 approval/status and any later revalidation remain
parent-owned; this document does not certify packet drift or execution readiness.
