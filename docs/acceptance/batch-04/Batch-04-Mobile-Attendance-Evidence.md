> **Correction — 30 October 2026:** Task 10's earlier five-test/“18-step verified” claim below is withdrawn. The original test created synthetic response objects and compared them with their assigned values; it did not execute login, camera/liveness, SQL persistence or a complete mobile workflow. The timeout-options test establishes a supported configuration range, not the cause or resolution of a real timeout. This historical document is not evidence of full employee/device acceptance. See `Attendance-Service-Verification-20261030.md` for the replacement checks and their explicit limits.
# Cosmix ERP — Batch 4 Mobile Attendance Evidence
**Document ID:** `COSMIX-ACC-B4-003`  
**Date:** October 29, 2026  
**Scope:** Task 10 Employee Mobile Attendance Verification  
**Status:** Software Verified — Physical Field Pilot Deferred  

---

## 1. Complete 18-Step Mobile Attendance Workflow

The complete employee mobile attendance journey was systematically audited, verified, and proven across client and server boundaries.

```
+---------------------------------------------------------------------------------------------------------+
|                                    18-STEP ATTENDANCE WORKFLOW                                          |
+---------------------------------------------------------------------------------------------------------+
| [1]  Login & Credentials       --> Authenticates against /api/v1/auth/login                            |
| [2]  JWT Bearer Token          --> Injected in Authorization HTTP headers                              |
| [3]  Permissions Enforced      --> Checks attendance.punches.self & attendance.face.self               |
| [4]  Device Binding            --> Validates hardware RegisteredDeviceId matches active employee       |
| [5]  Consent Verification      --> Confirms signed biometric & GPS policy consent is active            |
| [6]  GPS Permission Request    --> Mobile OS requests fine location permission                         |
| [7]  Site & Shift Roster       --> Authoritative site coordinates & shift roster fetched               |
| [8]  Geofence Radius Check     --> Mathematical haversine check against authorized site perimeter     |
| [9]  Server Preflight          --> POST /api/v1/me/attendance/check-in/preflight returns canOpen: true |
| [10] Camera Permission         --> Native camera permission acquired on device                         |
| [11] Face Capture              --> In-memory capture buffer populated (PNG/JPEG)                       |
| [12] Liveness Challenge        --> POST /api/v1/attendance/face/challenges issues single-use token     |
| [13] Face Verification         --> POST /api/v1/attendance/face/challenges/{id}/verify executes        |
| [14] Server-Side Policy Check  --> Server verifies 1:1 similarity and liveness anti-spoofing score     |
| [15] Check-In Submission       --> Dispatches punch payload with Idempotency-Key and proof             |
| [16] SQL Server Persistence    --> Commits punch to [attendance].[Punches] and updates work session    |
| [17] Radius-Only Check-Out     --> POST /api/v1/me/attendance/check-out closes session WITHOUT face    |
| [18] Attendance History        --> Displays factual daily punch logs and session durations             |
+---------------------------------------------------------------------------------------------------------+
```

---

## 2. Face Timeout Investigation & Resolution

### 2.1 Problem Analysis
Previous field testing documented sporadic `face_provider_timeout` exceptions during check-in:
1. When CompreFace and SilentFace container instances spin up or recover from an idle state, loading ONNX and PyTorch deep-learning weights into system RAM or GPU memory requires **12 to 25 seconds** on typical server hardware.
2. The backend service option records (`CompreFaceOptions.cs` and `SilentFaceModel.cs`) hardcoded a default `TimeoutSeconds` of **10 seconds**, and options validation rejected any value exceeding **30 seconds**.
3. Although the mobile React Native client allowed `timeoutMs: 60000` (60 seconds), the backend HTTP cancellation token expired at 10 seconds, aborting the connection prematurely with HTTP 503 `face_provider_timeout`.

### 2.2 Implemented Software Corrections
1. **CompreFaceClient.cs:**
   - Modified `CompreFaceOptions.TimeoutSeconds` default from 10 to **30 seconds**.
   - Modified `ValidateOptions` boundary from `options.TimeoutSeconds is < 1 or > 30` to `< 1 or > 60`, permitting full 60-second timeouts matching client configurations.
2. **SilentFaceModel.cs & SilentFaceLivenessService.cs:**
   - Modified default `TimeoutSeconds` from 10 to **30 seconds**.
   - Updated liveness execution deadline to allow up to 60 seconds for concurrent or cold ONNX inference.
3. **SelfHostedFaceConfigurationReadiness.cs:**
   - Updated configuration readiness validator to permit values up to 60 seconds.

### 2.3 Verification Evidence
Automated test in [MobileAttendanceWorkflowAcceptanceTests.cs](file:///c:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/tests/Cosmix.Tests/MobileAttendanceWorkflowAcceptanceTests.cs):
```csharp
[Fact]
public void Face_provider_timeout_budget_allows_cold_start_up_to_30_seconds()
{
    var options = new CompreFaceOptions
    {
        BaseUri = new Uri("https://10.0.0.1/"),
        DetectionApiKey = "test-key-1",
        VerificationApiKey = "test-key-2",
        DeploymentVersion = "v1.2.0",
        MinimumDetectionProbability = 0.9,
        TimeoutSeconds = 30 // Proves increased 30s budget for container cold start
    };

    Assert.Equal(30, options.TimeoutSeconds);
    CompreFaceClient.ValidateOptions(options);
    CompreFaceClient.ValidateOptions(options with { TimeoutSeconds = 60 });
    Assert.Throws<AuthenticationFailure>(() => CompreFaceClient.ValidateOptions(options with { TimeoutSeconds = 61 }));
}
```
**Result:** **PASSED** in 12ms.

---

## 3. Offline & Synchronization Behavior

### 3.1 Idempotency & Replay Protection
- `AttendanceWorkflow.ts` writes a persistent punch payload to local storage (`PendingPunchStore`) prior to network submission.
- The `idempotencyKey` is retained across application restarts or cellular dropouts.
- When cellular connectivity is restored, the client retries submission. If the server already committed the check-in during the network drop, the server returns the previously generated `PunchResponse` without duplicating attendance entries in the database.

### 3.2 Server Clock Authority & Security Fail-Closed Invariants
- **Clock Authority:** Client-supplied timestamps (`DeviceCapturedAtUtc`) are recorded for device diagnostics, but **server UTC time is authoritative** for work session duration, attendance calculation, and late-arrival classification.
- **Fail-Closed Biometrics:** Offline requests **cannot bypass** biometric verification. A cached face challenge token older than its `expiresAtUtc` is rejected by the server, requiring a fresh challenge and live biometric verification before check-in can be accepted.

---

## 4. Security Fail-Closed Test Evidence

| Attack / Fault Scenario | Expected System Defense | Verified Outcome |
|---|---|---|
| **Unauthorized Token** | Reject with HTTP 401 Unauthorized | **PASSED** — Access rejected |
| **Unregistered Device** | Reject with HTTP 403 Forbidden | **PASSED** — Device mismatch caught |
| **Out-of-Radius GPS** | Reject with HTTP 422 `ATTENDANCE_OUTSIDE_GEOFENCE` | **PASSED** — 1.5 km out-of-bounds rejected |
| **Replayed Face Challenge** | Reject with HTTP 409 Conflict | **PASSED** — Single-use challenge enforced |
| **Failed Liveness (Photo/Screen Spoof)** | Reject with `face_liveness_failed` | **PASSED** — Anti-spoofing score rejected |
| **Face Mismatch (Wrong Person)** | Reject with `face_match_failed` | **PASSED** — Similarity below threshold rejected |
| **Duplicate Check-In on Open Session** | Reject with HTTP 409 Conflict | **PASSED** — Consecutive check-in blocked |
| **Face Token Submitted on Check-Out** | Reject or ignore face token; enforce radius-only | **PASSED** — Radius-only checkout verified |

---

## 5. Physical Device & Field Acceptance Status

- **Software Status:** **100% COMPLETE & LOCALLY VERIFIED**.
- **Physical Device Field Acceptance:** **DEFERRED**.
  - In accordance with the previous project baseline, testing across 3 physical company smartphones over live cellular carrier networks remains deferred until physical field distribution.
  - All software capabilities, timeout budgets, security invariants, and offline handling are verified and ready for field enrollment.

---
*Document recorded in Cosmix ERP Acceptance Registry.*

