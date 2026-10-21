# Dot-source only. Loading this file performs no HTTP request or private-file write.
# Invoke explicitly AFTER scheduling Run and restart Replay have completed.
. (Join-Path $PSScriptRoot 'LocalAcceptanceHttp.ps1')

function Revoke-LocalAcceptanceRosterGrant {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Client,
        [Parameter(Mandatory)]$Manifest,
        [Parameter(Mandatory)]$AccessEvidence,
        [Parameter(Mandatory)][ValidatePattern('^[A-Za-z][A-Za-z0-9_.-]{5,50}$')][string]$ExpectedRunId,
        $PriorAdminClient
    )
    function Require-RevocationCondition([bool]$Condition, [string]$Message) {
        if (-not $Condition) { throw "Acceptance revocation stopped: $Message. Response content suppressed." }
    }
    function Read-RevocationMetadata([string]$Path) {
        $response = Invoke-LocalAcceptanceRequest -Client $Client -Method GET -Path $Path
        Require-RevocationCondition ($response.StatusCode -eq 200) "metadata HTTP $($response.StatusCode)"
        return $response.Body
    }
    function Read-RevocationPages([string]$Path) {
        $items = @()
        for ($page = 1; $page -le 10; $page++) {
            $result = Read-RevocationMetadata "${Path}?page=$page&pageSize=100"
            $items += @($result.items)
            if ($items.Count -ge $result.totalCount) { return $items }
        }
        throw 'Acceptance revocation metadata exceeded the bounded page limit.'
    }
    Assert-LocalAcceptanceUri ([uri]$Client.BaseUri)
    Require-RevocationCondition ($Manifest.Database -ceq 'Cosmix_Testing' -and $Manifest.Committed -eq $true -and
        $Manifest.RunId -ceq $ExpectedRunId -and $AccessEvidence.Summary.RunId -ceq $ExpectedRunId -and
        $AccessEvidence.Summary.Passed -ge 65 -and $AccessEvidence.Summary.GrantsRetained -eq $true) 'completed run evidence mismatch'
    try {
        $accountId = [Guid]::Parse([string]$Manifest.AdminAccountId)
        $siteId = [Guid]::Parse([string]$Manifest.SiteId)
        $roleId = [Guid]::Parse([string]$AccessEvidence.Roles.RosterSite.id)
        $assignmentId = [Guid]::Parse([string]$AccessEvidence.Grants.AdminRoster.assignment.id)
    } catch { throw 'Acceptance revocation identifiers are missing or malformed.' }
    Require-RevocationCondition (-not (@($accountId,$siteId,$roleId,$assignmentId) -contains [Guid]::Empty)) 'empty identifier'
    $expectedRoleCode = $ExpectedRunId.ToUpperInvariant() + '_ROSTER_SITE'
    $saved = $AccessEvidence.Grants.AdminRoster.assignment
    Require-RevocationCondition ($saved.roleId -eq $roleId -and $saved.roleCode -ceq $expectedRoleCode -and
        $AccessEvidence.Roles.RosterSite.code -ceq $expectedRoleCode -and
        $AccessEvidence.Roles.RosterSite.isSystemRole -eq $false -and
        @($saved.scopes).Count -eq 1 -and $saved.scopes[0].kind -eq 5 -and
        $saved.scopes[0].targetId -eq $siteId -and $null -eq $saved.revokedAt) 'saved assignment is not this run scoped roster grant'

    $mode = Read-RevocationMetadata '/api/v1/auth/web/local-testing'
    Require-RevocationCondition ($mode.enabled -eq $true -and $mode.database -ceq 'Cosmix_Testing') 'host is not guarded local testing'
    $account = Read-RevocationMetadata "/api/v1/access/accounts/$accountId"
    Require-RevocationCondition ($account.id -eq $accountId) 'account metadata mismatch'
    $roles = @(Read-RevocationPages '/api/v1/access/roles' | Where-Object { $_.id -eq $roleId })
    Require-RevocationCondition ($roles.Count -eq 1) 'run role missing or ambiguous'
    $role = $roles[0]
    Require-RevocationCondition ($role.code -ceq $expectedRoleCode -and $role.isSystemRole -eq $false -and
        @($role.permissions).Count -eq 2 -and 'attendance.rosters.read' -cin $role.permissions -and
        'attendance.rosters.manage' -cin $role.permissions) 'current role is outside this run roster permissions'
    $assignments = @(Read-RevocationPages "/api/v1/access/accounts/$accountId/assignments" |
        Where-Object { $_.id -eq $assignmentId })
    Require-RevocationCondition ($assignments.Count -eq 1) 'run assignment missing or ambiguous'
    $assignment = $assignments[0]
    Require-RevocationCondition ($assignment.roleId -eq $roleId -and $assignment.roleCode -ceq $expectedRoleCode -and
        @($assignment.scopes).Count -eq 1 -and $assignment.scopes[0].kind -eq 5 -and
        $assignment.scopes[0].targetId -eq $siteId -and $null -eq $assignment.revokedAt) 'current assignment is not the retained run scoped roster grant'
    $response = Invoke-LocalAcceptanceRequest -Client $Client -Method POST -Path "/api/v1/access/accounts/$accountId/assignments/$assignmentId/revoke" -Body @{
        reason = "$ExpectedRunId synthetic acceptance: revoke only the run roster grant after restart replay"
        expectedAccountVersion = [string]$account.version
        expectedAssignmentVersion = [string]$assignment.version
    }
    Require-RevocationCondition ($response.StatusCode -eq 200 -and $response.Body.id -eq $accountId -and
        $response.Body.version -cne $account.version) "revoke HTTP $($response.StatusCode) or account version mismatch"
    $after = @(Read-RevocationPages "/api/v1/access/accounts/$accountId/assignments" |
        Where-Object { $_.id -eq $assignmentId })
    Require-RevocationCondition ($after.Count -eq 1 -and $null -ne $after[0].revokedAt) 'revocation not visible in assignment history'
    $priorRejected = $null
    if ($null -ne $PriorAdminClient) {
        $prior = Invoke-LocalAcceptanceRequest -Client $PriorAdminClient -Method GET -Path '/api/v1/me/permissions'
        Require-RevocationCondition ($prior.StatusCode -eq 401) 'prior Admin session survived revocation'
        $priorRejected = $true
    }
    return [pscustomobject]@{
        RunId = $ExpectedRunId; Action = 'RevokeAdminRosterOnly'; AccountId = $accountId
        RoleId = $roleId; AssignmentId = $assignmentId; RevokedAt = $after[0].revokedAt
        PriorAdminSessionRejected = $priorRejected; OtherGrantsRetained = $true
    }
}
