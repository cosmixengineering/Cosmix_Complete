[CmdletBinding()]
param([Alias('Execute')][switch]$ExecuteAuth,[string]$ApprovedManifestSha256,[string]$AuthApproval)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Invoke-A02Setup.ps1')
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')

function Assert-A02AuthIntent([bool]$Requested,[string]$Hash,[string]$Actual,[string]$Approval) {
    if (-not $Requested -or $Hash -cnotmatch '^[A-F0-9]{64}$' -or $Hash -cne $Actual -or
        $Approval -cne 'APPROVE A02 SETUP AND ONE HR WEB MFA ON 7481 ONLY') { throw 'A02 authentication execution requires the reviewed exact manifest and combined bounded authorization.' }
}

function Write-A02AuthReceipt([string]$Name,$Value) {
    if ($null -eq $script:FreshExecutionContext -or -not $script:FreshExecutionContext.Authorized -or
        $Name -cnotin @('auth-attempt.json','owned-host.json','auth-result.json','auth-failed.json')) { throw 'A02 receipt context invalid.' }
    $directory=(Get-FreshSqlScope).PrivateRoot+'\results'
    Assert-FreshPrivateAcl $directory
    $path=Join-Path $directory $Name
    Assert-FreshNoLinks $path
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 10))
    $stream=[IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    Assert-FreshPrivateAcl $path
}

function Get-A02RetainedDigests($Context,$Connection) {
    $mutable=@('auth.UserAccounts','auth.LoginPhoneBindings','auth.MobileOtpChallenges','auth.OtpDeliveries',
        'auth.UserSessions','auth.RefreshTokens','auth.AuthRateBuckets','audit.AuditEvents','dbo.__EFMigrationsHistory')
    $digests=[ordered]@{}
    foreach($table in $Context.Review.Packet.SchemaContract.Tables) {
        if ($table -cin $mutable) { continue }
        if ($table -cnotmatch '^[A-Za-z0-9_]+\.[A-Za-z0-9_]+$') { throw 'Unexpected table identifier.' }
        $quoted='['+$table.Replace('.','].[')+']'
        $count=@(Invoke-FreshSqlRead $Context $Connection ('SELECT COUNT_BIG(*) AS RowCount FROM '+$quoted+';') @{})
        Assert-FreshBusinessRowCount $table $count[0].RowCount $Context.Review.Packet.FixtureBudget.Tables $true
        if ($count[0].RowCount -eq 0) { $digests[$table]='EMPTY'; continue }
        $rows=@(Invoke-FreshSqlRead $Context $Connection ("SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM "+$quoted+" ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2) AS Digest;") @{})
        if($rows.Count -ne 1 -or $rows[0].Digest -cnotmatch '^[A-F0-9]{64}$') { throw 'Retained-source digest unavailable.' }
        $digests[$table]=$rows[0].Digest
    }
    return $digests
}

function Invoke-A02AuthExecution {
    if ($PSVersionTable.PSVersion.Major -lt 7 -or -not $IsWindows) { throw 'PowerShell 7 on Windows required.' }
    $manifestPath=Join-Path $PSScriptRoot 'A02-Review.json'
    Assert-FreshNoLinks $manifestPath
    Assert-A02AuthIntent ([bool]$ExecuteAuth) $ApprovedManifestSha256 (Get-FileHash -LiteralPath $manifestPath).Hash $AuthApproval
    $review=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable -Depth 30
    Set-A02ReleaseDirectory $review.Packet.Scope.ReleaseRoot
    $scope=Get-FreshSqlScope
    if ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ine $scope.Owner -or
        $review.Packet.Scope.Instance -cne $scope.Instance -or $review.Packet.Scope.PrivateRoot -cne $scope.PrivateRoot -or
        $review.Packet.Scope.ReleaseRoot -cne $scope.ReleaseRoot) { throw 'A02 manifest target mismatch.' }
    Assert-FreshExecutorHashes $review.Packet.InputHashes @(Get-FreshExecutorInputHashes)
    if ((Get-FileHash -LiteralPath (Get-FreshExecutorSqlArtifact)).Hash -cne $review.Packet.FullChainSqlSha256) { throw 'A02 SQL hash changed.' }
    Assert-FreshPrivateAcl $scope.PrivateRoot
    $stageRoot=$scope.PrivateRoot+'\manifest'
    Assert-FreshPrivateAcl $stageRoot
    $complete=@(Get-ChildItem -LiteralPath $stageRoot -Filter '*-SetupOnlySeedAndRetention-Complete.json')
    $metadata=@(Get-ChildItem -LiteralPath $stageRoot -Filter '*-NewInstanceMetadata-Complete.json')
    if ($complete.Count -ne 1 -or $metadata.Count -ne 1) { throw 'Exact completed A02 setup is required.' }
    Assert-FreshPrivateAcl $complete[0].FullName; Assert-FreshPrivateAcl $metadata[0].FullName
    $seed=Get-Content -LiteralPath $complete[0].FullName -Raw | ConvertFrom-Json
    $identity=Get-Content -LiteralPath $metadata[0].FullName -Raw | ConvertFrom-Json
    if ($seed.PacketSha256 -cne $review.PacketSha256 -or $identity.PacketSha256 -cne $review.PacketSha256 -or
        $seed.Status -cne 'Complete' -or $seed.Evidence.HostStarted) { throw 'Setup receipt binding failed.' }
    $expectedPaths=@($scope.Settings,($scope.PrivateRoot+'\Test-Accounts.json'),($scope.PrivateRoot+'\keys\files.key'),($scope.PrivateRoot+'\keys\snapshot.key'))
    if (@($seed.Evidence.PrivateHashes).Count -ne 4 -or
        (@($seed.Evidence.PrivateHashes.Path | Sort-Object) -join '|') -cne (@($expectedPaths | Sort-Object) -join '|')) { throw 'Setup private inventory failed.' }
    foreach($file in $seed.Evidence.PrivateHashes) {
        Assert-FreshPrivateAcl $file.Path
        if((Get-FileHash -LiteralPath $file.Path).Hash -cne $file.Sha256) { throw 'Setup private material changed.' }
    }
    $info=(& $review.Packet.Tools.LocalDb info $scope.Instance 2>$null) -join "`n"
    if($LASTEXITCODE -ne 0) { throw 'A02 instance metadata unavailable.' }
    $pipe=(ConvertFrom-FreshLocalDbInfo $info).Pipe
    if($pipe -cne $identity.Evidence.Pipe) { throw 'A02 pipe changed; review restart binding before continuing.' }
    Assert-FreshExecutorSqlIdentity $pipe $identity.Evidence.ActualServer $scope.Owner 'Cosmix_Testing'
    if(@(Get-NetTCPConnection -State Listen -ErrorAction Stop | Where-Object LocalPort -eq 7481).Count -ne 0) { throw 'Port 7481 is occupied.' }
    if(Test-Path -LiteralPath ($scope.PrivateRoot+'\results\auth-attempt.json')) { throw 'Prior auth attempt exists; never rerun automatically.' }
    $context=[pscustomobject]@{Authorized=$true;Review=$review;Pipe=$pipe;Server=$identity.Evidence.ActualServer;RecordNumber=0}
    $script:FreshExecutionContext=$context
    $connection=$null; $process=$null; $client=$null; $credentials=$null
    $locks=[Collections.Generic.List[IO.FileStream]]::new()
    try {
        foreach($path in @($review.Packet.InputHashes.Path)+@((Get-FreshExecutorSqlArtifact),$manifestPath)+$expectedPaths) {
            $locks.Add([IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read))
        }
        Assert-FreshInputSnapshot $context
        $connection=New-FreshSqlConnection $context $pipe 'Cosmix_Testing'
        Assert-FreshSchemaAndData $context $connection -AfterSeed
        $before=Get-A02RetainedDigests $context $connection
        if(@(Get-ChildItem -LiteralPath $scope.DataProtectionDirectory -File).Count -ne 0) { throw 'Initial A02 token keyring must be empty.' }
        Write-A02AuthReceipt 'auth-attempt.json' @{PacketSha256=$review.PacketSha256;Actor='hr';Utc=[DateTimeOffset]::UtcNow.ToString('o');MaximumSessions=1}
        $start=[Diagnostics.ProcessStartInfo]::new($review.Packet.Tools.Dotnet)
        $start.ArgumentList.Add($review.Packet.ApiAssembly)
        $start.UseShellExecute=$false; $start.CreateNoWindow=$true
        $start.WorkingDirectory=$review.Packet.SourceWorkingApiContentDir
        $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
        $start.Environment.Clear()
        foreach($pair in (New-FreshChildEnvironment).GetEnumerator()) { $start.Environment.Add($pair.Key,$pair.Value) }
        $process=[Diagnostics.Process]::new(); $process.StartInfo=$start
        if(-not $process.Start()) { throw 'Owned A02 host failed to start.' }
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        $ready=$false
        for($attempt=0;$attempt -lt 60;$attempt++) {
            if($process.HasExited) { throw 'Owned A02 host exited; output suppressed.' }
            $listeners=@(Get-NetTCPConnection -State Listen -ErrorAction Stop | Where-Object LocalPort -eq 7481)
            if($listeners.Count -gt 0) {
                if(@($listeners | Where-Object { $_.OwningProcess -ne $process.Id -or $_.LocalAddress -notin @('127.0.0.1','::1') }).Count -ne 0) { throw 'A02 listener ownership failed.' }
                $ready=$true; break
            }
            Start-Sleep -Milliseconds 500
        }
        if(-not $ready) { throw 'A02 host startup timed out.' }
        Write-A02AuthReceipt 'owned-host.json' @{ProcessId=$process.Id;StartedUtc=$process.StartTime.ToUniversalTime().ToString('o');ApiAssembly=$review.Packet.ApiAssembly;BaseUri='https://localhost:7481/';PacketSha256=$review.PacketSha256}
        # Startup must not write grants or any business records.
        Assert-FreshSchemaAndData $context $connection -AfterSeed
        $credentials=@(Get-Content -LiteralPath ($scope.PrivateRoot+'\Test-Accounts.json') -Raw | ConvertFrom-Json)
        if($credentials.Count -ne 3 -or @($credentials | Where-Object Email -CEQ 'hr@example.invalid').Count -ne 1) { throw 'Unexpected credentials inventory.' }
        $account=$credentials | Where-Object Email -CEQ 'hr@example.invalid'
        $client=New-LocalAcceptanceSession -BaseUri 'https://localhost:7481/' -Account $account
        $account=$null; $credentials=$null
        $denied=Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path '/api/v1/auth/web/logout' -WithoutCsrf
        if($denied.StatusCode -ne 400 -or $denied.Body.title -cne 'invalid_csrf_token') { throw 'CSRF denial failed.' }
        $active=Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/me/permissions'
        if($active.StatusCode -ne 200) { throw 'CSRF denial altered the live session.' }
        $replay=[pscustomobject]@{BaseUri=$client.BaseUri;WebSession=[Microsoft.PowerShell.Commands.WebRequestSession]::new();CsrfToken=$client.CsrfToken;SkipCertificateCheck=$false}
        foreach($cookie in $client.WebSession.Cookies.GetCookies([uri]$client.BaseUri)) {
            $copy=[Net.Cookie]::new($cookie.Name,$cookie.Value,$cookie.Path,$cookie.Domain)
            $copy.Secure=$cookie.Secure; $copy.HttpOnly=$cookie.HttpOnly; $copy.Expires=$cookie.Expires
            $replay.WebSession.Cookies.Add($copy)
        }
        $logout=Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path '/api/v1/auth/web/logout'
        $revoked=Invoke-LocalAcceptanceRequest -Client $replay -Method GET -Path '/api/v1/me/permissions'
        if($logout.StatusCode -ne 204 -or $revoked.StatusCode -ne 401) { throw 'Persisted logout/replay denial failed.' }
        $client=$null; $replay=$null; $cookie=$null; $copy=$null; $active=$null
        # A new physical connection verifies committed state independently of HTTP and the preflight connection.
        $connection.Dispose(); $connection=New-FreshSqlConnection $context $pipe 'Cosmix_Testing'
        [void](Assert-FreshLiveDatabase $context $connection 'Cosmix_Testing')
        $after=Get-A02RetainedDigests $context $connection
        if(($before | ConvertTo-Json -Compress) -cne ($after | ConvertTo-Json -Compress)) { throw 'An immutable business/grant row changed.' }
        $proof=@(Invoke-FreshSqlRead $context $connection @'
SELECT
 (SELECT COUNT_BIG(*) FROM auth.MobileOtpChallenges) AS Challenges,
 (SELECT COUNT_BIG(*) FROM auth.OtpDeliveries) AS Deliveries,
 (SELECT COUNT_BIG(*) FROM auth.UserSessions) AS Sessions,
 (SELECT COUNT_BIG(*) FROM auth.RefreshTokens) AS RefreshTokens,
 (SELECT COUNT_BIG(*) FROM auth.AuthRateBuckets) AS RateBuckets,
 (SELECT COUNT_BIG(*) FROM audit.AuditEvents) AS Audits,
 (SELECT COUNT_BIG(*) FROM auth.UserAccounts) AS Accounts,
 (SELECT COUNT_BIG(*) FROM auth.LoginPhoneBindings) AS Bindings,
 (SELECT COUNT_BIG(*) FROM auth.UserSessions s JOIN auth.UserAccounts a ON a.Id=s.UserAccountId
  JOIN auth.MobileOtpChallenges c ON c.Id=s.SmsOtpChallengeId
  WHERE a.LoginName=N'hr@example.invalid' AND s.Client=1 AND s.RevokedAt IS NOT NULL
    AND s.RevocationReason=N'user-logout' AND c.ConsumedAt IS NOT NULL AND c.Purpose=N'WebLogin') AS RevokedHrProof;
'@ @{})
        if($proof.Count -ne 1 -or $proof[0].Challenges -ne 1 -or $proof[0].Deliveries -ne 1 -or
            $proof[0].Sessions -ne 1 -or $proof[0].RefreshTokens -ne 1 -or $proof[0].RevokedHrProof -ne 1 -or
            $proof[0].Accounts -ne 3 -or $proof[0].Bindings -ne 3 -or $proof[0].RateBuckets -gt 20 -or
            $proof[0].Audits -lt 5 -or $proof[0].Audits -gt 13) { throw 'Auth SQL write budget/proof failed.' }
        $dp=@(Get-ChildItem -LiteralPath $scope.DataProtectionDirectory -File)
        if($dp.Count -ne 1 -or $dp[0].Name -notmatch '^key-[a-fA-F0-9-]+\.xml$') { throw 'A02 Data Protection key budget failed.' }
        Assert-FreshPrivateAcl $dp[0].FullName
        Assert-FreshInputSnapshot $context
        Write-A02AuthReceipt 'auth-result.json' @{PacketSha256=$review.PacketSha256;Passed=$true;SqlProof=$proof[0];GrantsAndBusinessDigestsUnchanged=$true;CsrfDenied=400;RevokedCookie=401;ExternalProvidersCalled=$false}
        Write-Output 'PASS: one real synthetic HR web MFA/CSRF/logout session and independent SQL proof; immutable grants/business rows preserved.'
    } catch {
        if(Test-Path -LiteralPath ($scope.PrivateRoot+'\results\auth-attempt.json')) {
            try { Write-A02AuthReceipt 'auth-failed.json' @{PacketSha256=$review.PacketSha256;Failed=$true;DetailsSuppressed=$true;StateRetained=$true} } catch { }
        }
        throw 'A02 auth stopped; state retained, sensitive details suppressed. No automatic retry is allowed.'
    } finally {
        if($null -ne $process) {
            try { if(-not $process.HasExited) { $process.Kill(); [void]$process.WaitForExit(5000) } } catch { }
            $process.Dispose()
        }
        if($null -ne $connection) { $connection.Dispose() }
        foreach($handle in $locks) { $handle.Dispose() }
        $client=$null; $credentials=$null; $context.Authorized=$false; $script:FreshExecutionContext=$null
    }
}

if($MyInvocation.InvocationName -ne '.') {
    if(-not $ExecuteAuth) { [pscustomobject]@{Mode='ReviewOnly';Instance='CosmixAttendanceHuma20261015A02';Port=7481;MaximumLogins=1;Actor='hr';SqlConnections=0;Writes=0} }
    else { Invoke-A02AuthExecution }
}
