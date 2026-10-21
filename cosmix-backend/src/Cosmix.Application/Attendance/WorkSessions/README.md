# WorkSessions

Owner: Developer 2.

Source: SRS 7, SITE-009; MD-006.

Pair/checkpoint raw punches across midnight. Preserve evidence and flag missing/overlapping sequences. Extended-duty resets need approved examples.

Status: factual session opening/closing revisions and self history are implemented by
`AttendanceCaptureService` in the adjacent Punches feature. Raw evidence and earlier session revisions
remain immutable. Policy IDs may be absent for factual elapsed time; no pay/continuous-duty reset
rules are invented. See `docs/Attendance-Capture.md` and its verification record.

Session persistence is module-specific through `IAttendanceCaptureRepository`; history DTOs distinguish
factual elapsed minutes from unresolved policy-dependent payable results.
