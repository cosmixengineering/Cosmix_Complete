# Dot-source only. No request, process or filesystem mutation occurs while loading this file.
# Returned clients contain live cookies/CSRF material: keep them in memory, never serialize them.
if (-not (Get-Variable -Name LocalAcceptanceNextLogin -Scope Script -ErrorAction SilentlyContinue)) {
    $script:LocalAcceptanceNextLogin = @{}
}

function Assert-LocalAcceptanceUri([uri]$BaseUri) {
    if (-not $BaseUri.IsAbsoluteUri -or $BaseUri.Scheme -cne 'https' -or
        $BaseUri.Host -ine 'localhost' -or $BaseUri.UserInfo -or $BaseUri.Query -or
        $BaseUri.Fragment -or $BaseUri.AbsolutePath -ne '/') {
        throw 'Acceptance requires a root HTTPS localhost URL without credentials, query or fragment.'
    }
}

function Invoke-LocalAcceptanceRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Client,
        [Parameter(Mandatory)][ValidateSet('GET','POST','PUT','PATCH','DELETE')][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [object]$Body,
        [Collections.IDictionary]$Headers,
        [switch]$WithoutCsrf
    )
    Assert-LocalAcceptanceUri ([uri]$Client.BaseUri)
    if ($Path -notmatch '^/api/v1/[A-Za-z0-9/?=&_.%-]+$' -or $Path.Contains('..') -or $Path.Contains('//')) {
        throw 'Acceptance request path is outside the fixed API surface.'
    }
    $requestHeaders = @{}
    if ($Headers) {
        foreach ($name in $Headers.Keys) {
            if ($name -notin @('Idempotency-Key','X-Correlation-ID')) { throw 'Acceptance header is not allowed.' }
            $requestHeaders[$name] = [string]$Headers[$name]
        }
    }
    if ($Method -ne 'GET' -and -not $WithoutCsrf -and $Client.CsrfToken) {
        $requestHeaders['X-CSRF-TOKEN'] = $Client.CsrfToken
    }
    # Invoke-WebRequest retains explicit headers on WebRequestSession. An omission probe
    # must remove the previously stored token, not merely leave it out of this call's map.
    [void]$Client.WebSession.Headers.Remove('X-CSRF-TOKEN')
    [void]$Client.WebSession.Headers.Remove('Idempotency-Key')
    $arguments = @{
        Uri = [uri]::new([uri]$Client.BaseUri, $Path)
        Method = $Method
        WebSession = $Client.WebSession
        Headers = $requestHeaders
        SkipHttpErrorCheck = $true
        MaximumRedirection = 0
        TimeoutSec = 30
        ErrorAction = 'Stop'
    }
    if ($Client.SkipCertificateCheck) { $arguments.SkipCertificateCheck = $true }
    if ($PSBoundParameters.ContainsKey('Body')) {
        $arguments.ContentType = 'application/json'
        $arguments.Body = ConvertTo-Json -InputObject $Body -Depth 20 -Compress
    }
    $diagnosticPath = $Path.Split('?')[0]
    try { $response = Invoke-WebRequest @arguments }
    catch { throw "Local acceptance HTTP transport failed: $Method $diagnosticPath; sensitive request/response details were suppressed." }
    $statusCode = [int]$response.StatusCode
    $content = $null
    $responseText = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { [string]$response.Content }
    if (-not [string]::IsNullOrWhiteSpace($responseText)) {
        try { $content = ConvertFrom-Json -InputObject $responseText -Depth 30 }
        catch {
            # UseStatusCodePages emits plain text for authentication failures and empty error results.
            # Preserve status for denial assertions, but never expose that unstructured error body.
            if ($statusCode -lt 400) {
                throw "Local acceptance received non-JSON: $Method $diagnosticPath (HTTP $statusCode); content was suppressed."
            }
            $content = $null
        }
    }
    # Do not return Set-Cookie or other credential-bearing response headers.
    $publicHeaders = @{}
    foreach ($name in @('Content-Type','Cache-Control','Location')) {
        if ($response.Headers.ContainsKey($name)) { $publicHeaders[$name] = [string]$response.Headers[$name] }
    }
    return [pscustomobject]@{ StatusCode = $statusCode; Body = $content; Headers = $publicHeaders }
}

function New-LocalAcceptanceSession {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][uri]$BaseUri,
        [Parameter(Mandatory)]$Account,
        [switch]$SkipCertificateCheck
    )
    Assert-LocalAcceptanceUri $BaseUri
    $email = ([string]$Account.Email).Trim().ToLowerInvariant()
    if ($email -notin @('superadmin@example.invalid','admin@example.invalid','hr@example.invalid') -or
        [string]::IsNullOrWhiteSpace([string]$Account.Password)) {
        throw 'Acceptance login requires one of the three private synthetic fixture credentials.'
    }
    if ($script:LocalAcceptanceNextLogin.ContainsKey($email)) {
        $delay = [Math]::Ceiling(($script:LocalAcceptanceNextLogin[$email] - [DateTimeOffset]::UtcNow).TotalMilliseconds)
        if ($delay -gt 60000) { throw 'The advertised login cooldown exceeds this runner bound; wait before retrying.' }
        if ($delay -gt 0) { Start-Sleep -Milliseconds ([int]$delay) }
    }
    $client = [pscustomobject]@{
        BaseUri = $BaseUri
        WebSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        CsrfToken = $null
        CsrfHeaderName = 'X-CSRF-TOKEN'
        SkipCertificateCheck = [bool]$SkipCertificateCheck
        ResendAvailableAt = $null
    }
    $display = [Management.Automation.PSPropertySet]::new('DefaultDisplayPropertySet', [string[]]@('BaseUri'))
    $client | Add-Member -MemberType MemberSet -Name PSStandardMembers -Value @($display)
    $probe = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/auth/web/local-testing'
    if ($probe.StatusCode -ne 200 -or $probe.Body.enabled -ne $true -or $probe.Body.database -cne 'Cosmix_Testing') {
        throw 'Refusing credentials: this host did not confirm the guarded Cosmix_Testing local mode.'
    }
    $csrf = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/auth/web/csrf'
    if ($csrf.StatusCode -ne 200 -or $csrf.Body.headerName -cne 'X-CSRF-TOKEN' -or -not $csrf.Body.requestToken) {
        throw 'Local acceptance could not obtain the anonymous browser CSRF context.'
    }
    $client.CsrfToken = [string]$csrf.Body.requestToken
    $challenge = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path '/api/v1/auth/web/login' -Body @{
        email = $email; password = [string]$Account.Password
    }
    if ($challenge.StatusCode -ne 202 -or -not $challenge.Body.challengeId) { throw 'Local acceptance password challenge failed.' }
    $client.ResendAvailableAt = [DateTimeOffset]$challenge.Body.resendAvailableAt
    $script:LocalAcceptanceNextLogin[$email] = $client.ResendAvailableAt.AddSeconds(1)
    $preMfa = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/me/permissions'
    if ($preMfa.StatusCode -ne 401) { throw 'Password challenge unexpectedly established an authenticated session before OTP proof.' }
    $otp = $null
    try {
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            $view = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path '/api/v1/auth/web/local-testing/otp' -Body @{
                challengeId = [string]$challenge.Body.challengeId
            }
            if ($view.StatusCode -eq 200 -and [string]$view.Body.code -match '^\d{6}$') { $otp = [string]$view.Body.code; break }
            if ($view.StatusCode -ne 404) { throw 'The guarded local OTP viewer rejected the browser context.' }
            Start-Sleep -Milliseconds 500
        }
        if (-not $otp) { throw 'No current synthetic web OTP was delivered within the bounded wait; login was not bypassed.' }
        $verified = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path '/api/v1/auth/web/otp/verify' -Body @{
            challengeId = [string]$challenge.Body.challengeId; code = $otp
        }
        if ($verified.StatusCode -ne 200 -or $verified.Body.nextStep -cne 'authenticated') { throw 'Local acceptance web OTP verification failed.' }
    } finally { $otp = $null; $view = $null }
    # Antiforgery binds identity. Replace the anonymous request token after successful MFA.
    $csrf = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/auth/web/csrf'
    if ($csrf.StatusCode -ne 200 -or -not $csrf.Body.requestToken) { throw 'Authenticated browser CSRF refresh failed.' }
    $client.CsrfToken = [string]$csrf.Body.requestToken
    $current = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/me/permissions'
    if ($current.StatusCode -ne 200) { throw 'The real MFA session failed current persisted-session validation.' }
    return $client
}
