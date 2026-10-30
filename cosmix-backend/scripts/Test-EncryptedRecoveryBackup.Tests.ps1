#requires -Version 5.1
# Synthetic inputs and a local executable double only; no SQL, repository or company data is touched.
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('cosmix-backup-tests-' + [guid]::NewGuid().ToString('N'))
$previousRepository = $env:RESTIC_REPOSITORY
$previousPassword = $env:RESTIC_PASSWORD_FILE
$checks = 0
try {
    $null = New-Item -ItemType Directory -Path $root,(Join-Path $root 'evidence'),(Join-Path $root 'custody')
    $sql = Join-Path $root 'synthetic.bak'
    $manifestPath = Join-Path $root 'manifest.json'
    Set-Content -LiteralPath $sql -Value 'synthetic backup bytes; not a SQL backup'
    Set-Content -LiteralPath (Join-Path $root 'evidence/proof.txt') -Value 'synthetic evidence'
    $env:RESTIC_REPOSITORY = Join-Path $root 'fake-repository'
    $env:RESTIC_PASSWORD_FILE = Join-Path $root 'custody/password.txt'
    Set-Content -LiteralPath $env:RESTIC_PASSWORD_FILE -Value 'synthetic-password'
    @{schemaVersion=1;coordinatedSnapshot=$true;sqlRestoreVerified=$true;keyCustodyVerified=$true;
        keyCustodyReference='synthetic-test-only';snapshotAtUtc=[DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o');
        sqlBackupSha256=(Get-FileHash -LiteralPath $sql -Algorithm SHA256).Hash.ToLowerInvariant()} |
        ConvertTo-Json | Set-Content -LiteralPath $manifestPath
    $stub = Join-Path $root 'fake-restic.cmd'
    $arguments = @{SqlBackupFile=$sql;PrivateEvidenceSnapshotDirectory=(Join-Path $root 'evidence');
        RecoveryManifest=$manifestPath;KeyCustodyPaths=@((Join-Path $root 'custody'));ResticPath=$stub;LocalDrill=$true}
    foreach ($case in @(
        @{Name='success';BackupExit=0;CheckExit=0;Expected=$true},
        @{Name='partial-backup';BackupExit=3;CheckExit=0;Expected=$false},
        @{Name='integrity-failure';BackupExit=0;CheckExit=1;Expected=$false})) {
        @('@echo off',
            ('if "%1"=="check" exit /b ' + $case.CheckExit),
            ('echo {"message_type":"summary","snapshot_id":"' + ('a'*64) + '"}'),
            ('exit /b ' + $case.BackupExit)) | Set-Content -LiteralPath $stub -Encoding ascii
        $output = Join-Path $root ($case.Name+'.json')
        $succeeded = $false
        $failure = $null
        try {
            & (Join-Path $PSScriptRoot 'Invoke-EncryptedRecoveryBackup.ps1') @arguments -EvidenceOutput $output | Out-Null
            $succeeded = $true
        } catch { $failure = $_.Exception.Message }
        if ($succeeded -ne $case.Expected) { throw "Backup case '$($case.Name)' returned unexpected result: $failure" }
        if ($case.Expected) {
            $report = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
            if ($report.mode -ne 'LocalDrill' -or -not $report.repositoryIntegrityChecked -or $report.restoredByThisRun -or
                $report.snapshotId -cne ('a'*64)) { throw 'Backup report contract failed.' }
        } elseif (Test-Path -LiteralPath $output) { throw 'Failed backup wrote success evidence.' }
        $checks++
    }
    [pscustomobject]@{Passed=$checks;Failed=0;PowerShellVersion=$PSVersionTable.PSVersion.ToString();RealBackupTested=$false}
} finally {
    $env:RESTIC_REPOSITORY = $previousRepository
    $env:RESTIC_PASSWORD_FILE = $previousPassword
    $resolved = [IO.Path]::GetFullPath($root)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    if ($resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolved) -match '^cosmix-backup-tests-[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
