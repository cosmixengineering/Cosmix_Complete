# Actual local scanner acceptance

13 October 2026, Part A continuation. The installed ClamAV engine was started as a hidden task-owned process on its configured loopback port 13310 after checking that no other process owned the port. Private process metadata binds PID, exact creation time, executable hash and configuration hash. No employee file or database was used.

The official `freshclam` updater completed successfully. Daily definitions advanced from 28147 to 28152; the database test passed, and main/bytecode definitions were current according to the updater. The task-owned daemon acknowledged reload. The actual adapter subsequently reported `ClamAV 1.5.4/28152/Tue Oct 13 11:26:14 2026`.

`VerifyLiveScanner.cs` passed **10 actual local scanner checks**, exit 0: bounded synthetic text returned Clean; the standard inert antimalware test returned Rejected; both retained actual engine/version identity and left caller bytes unchanged; disabled, empty and oversized requests never reported Clean; cancellation remained cancellation. The test marker existed only in memory and was streamed directly to the daemon; no test file or employee attachment was created.

Command, from the backend:

```powershell
dotnet run --file ..\audit\hr-srs-2026-10-13\part-a-continuation\VerifyLiveScanner.cs --artifacts-path "$env:TEMP\cosmix-part-a-continuation-20261013\scanner-artifacts" -c Release -p:UseSharedCompilation=false
```

This closes the installed local engine/definition/adapter detection check within that scope. It does not establish every file type/malware family, authorized HTTP document quarantine/rescan persistence, production deployment availability or backup/restore. Those are separate end-to-end acceptance checks. Process disposition and additional checks are recorded below when executed.
