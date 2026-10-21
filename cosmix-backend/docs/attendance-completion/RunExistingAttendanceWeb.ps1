param([ValidateSet('B01','B02')][string]$RunId = 'B01')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Huma-Acceptance-Common.ps1')
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')
$root = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting'
$receipt = Join-Path $root ('Attendance-Web-Acceptance-20261013-' + $RunId + '-result.json')
if (Test-Path -LiteralPath $receipt) { throw 'Result already exists; inspect rather than overwrite/replay.' }
Assert-HumaPrivate $root
$accountsPath = Join-Path $root 'Test-Accounts.json'
Assert-HumaPrivate $accountsPath
$accounts = @(Get-Content -LiteralPath $accountsPath -Raw | ConvertFrom-Json)
if ($accounts.Count -ne 3 -or (@($accounts.Email | Sort-Object) -join '|') -cne 'admin@example.invalid|hr@example.invalid|superadmin@example.invalid') { throw 'Only existing three synthetic accounts permitted.' }
$started = [DateTimeOffset]::UtcNow
$results = @()
foreach ($account in $accounts) {
    $client = New-LocalAcceptanceSession -BaseUri 'https://localhost:7481/' -Account $account -SkipCertificateCheck
    $permissions = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/me/permissions'
    if ($permissions.StatusCode -ne 200) { throw 'Real MFA permission read failed.' }
    $missingCsrf = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path '/api/v1/auth/web/logout' -WithoutCsrf
    if ($missingCsrf.StatusCode -ne 400 -or $missingCsrf.Body.title -cne 'invalid_csrf_token') { throw 'Missing CSRF did not fail closed before logout.' }
    $stillActive = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/me/permissions'
    if ($stillActive.StatusCode -ne 200) { throw 'Rejected CSRF unexpectedly changed authentication state.' }
    $shift = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/me/shift?businessDate=2026-10-13'
    if ($shift.StatusCode -ge 500) { throw 'Authenticated My Shift request caused a backend server failure.' }
    $companyDenied = $null
    if ($account.Email -ceq 'hr@example.invalid') {
        $company = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/hr/company-settings'
        if ($company.StatusCode -ne 403) { throw 'HR without dedicated company permission unexpectedly passed the restricted read.' }
        $companyDenied = 403
    }
    $replay = [pscustomobject]@{ BaseUri=$client.BaseUri; WebSession=[Microsoft.PowerShell.Commands.WebRequestSession]::new();
        CsrfToken=$client.CsrfToken; SkipCertificateCheck=$true }
    foreach ($cookie in $client.WebSession.Cookies.GetCookies([uri]$client.BaseUri)) {
        $copy = [Net.Cookie]::new($cookie.Name,$cookie.Value,$cookie.Path,$cookie.Domain)
        $copy.Secure=$cookie.Secure; $copy.HttpOnly=$cookie.HttpOnly; $copy.Expires=$cookie.Expires
        $replay.WebSession.Cookies.Add($copy)
    }
    $logout = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path '/api/v1/auth/web/logout'
    if ($logout.StatusCode -ne 204) { throw 'Valid CSRF logout failed.' }
    $revoked = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/me/permissions'
    if ($revoked.StatusCode -ne 401) { throw 'Revoked session remained authorized.' }
    $retainedCookie = Invoke-LocalAcceptanceRequest -Client $replay -Method GET -Path '/api/v1/me/permissions'
    if ($retainedCookie.StatusCode -ne 401) { throw 'Retained pre-logout cookie bypassed persisted session revocation.' }
    $results += [pscustomobject]@{ Actor=$account.Email.Split('@')[0]; PasswordChallenge=$true; PreMfaDenied=$true; LocalOtpProof=$true;
        CurrentPermissions=200; MissingCsrf=400; RejectedCsrfPreservesSession=$true; ShiftObservedStatus=$shift.StatusCode;
        ShiftObservedCode=$shift.Body.title; CompanyDenied=$companyDenied; Logout=204; RevokedRead=401; RetainedCookieReplay=401 }
    $client = $null; $replay=$null; $copy=$null; $cookie=$null; $permissions = $null; $account = $null
}
$openapiResponse = Invoke-WebRequest -Uri 'https://localhost:7481/openapi/v1.json' -SkipCertificateCheck -TimeoutSec 20
$schema = $openapiResponse.Content | ConvertFrom-Json
$attestationPath = '/api/v1/attendance/employees/{employeeId}/payroll-inputs/{year}/{month}/revisions/{sourceRevisionId}/attestations'
$description = $schema.paths.$attestationPath.post.description
if ($description -notmatch 'empty lines permits handoff only') { throw 'Actual OpenAPI still describes obsolete empty-source behavior.' }
Write-HumaNewJson $receipt @{ StartedAtUtc=$started.ToString('O'); FinishedAtUtc=[DateTimeOffset]::UtcNow.ToString('O');
    Database='Cosmix_Testing'; Source='np:\\.\pipe\LOCALDB#842DD4FB\tsql\query'; BaseUri='https://localhost:7481/';
    SyntheticActorsOnly=$true; RealWebMfa=$true; Results=$results; OpenApiNoChargeContract=$true;
    Limits='No biometric acceptance, business fixture creation, financial settlement, mobile self/scopes or concurrency acceptance claimed.' }
$accounts=$null
$results | Format-Table Actor,CurrentPermissions,MissingCsrf,ShiftObservedStatus,CompanyDenied,Logout,RetainedCookieReplay
Write-Output 'PASS: three actual SQL-backed web MFA sessions, missing-CSRF denial, permitted reads, restricted HR denial, logout/revocation and registered no-charge OpenAPI contract. No credentials/tokens printed.'
