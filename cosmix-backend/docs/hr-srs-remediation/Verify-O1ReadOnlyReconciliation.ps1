$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$helper = Join-Path $PSScriptRoot 'O1ReadOnlyReconciliation.Helpers.ps1'
if (!(Test-Path -LiteralPath $helper)) { throw 'Missing pure ID delta helper.' }
. $helper

$passed = 0
function Check([string]$Name, [scriptblock]$Action) {
    & $Action
    $script:passed++
}
function Require([bool]$Condition) {
    if (!$Condition) { throw 'Offline assertion failed.' }
}
function Denied([scriptblock]$Action) {
    $rejected = $false
    try { $null = & $Action } catch { $rejected = $true }
    Require $rejected
}
function Id([int]$Index) {
    return ('20102001-0001-4000-8000-{0:x12}' -f $Index)
}

$a = Id 1
$b = Id 2
$c = Id 3
Check 'Zero on empty arrays is a string array' {
    $delta = Get-O1IdDelta @() @()
    Require ($delta -is [string[]] -and $delta.Count -eq 0)
}
Check 'Zero on unchanged originals' {
    $delta = Get-O1IdDelta @($a, $b) @($b, $a)
    Require ($delta -is [string[]] -and $delta.Count -eq 0)
}
Check 'One new canonical ID' {
    $delta = Get-O1IdDelta @($a) @($b.ToUpperInvariant(), $a.ToUpperInvariant())
    Require ($delta -is [string[]] -and $delta.Count -eq 1 -and $delta[0] -ceq $b)
}
Check 'Thirty-two new IDs sorted' {
    $current = @(33..1 | ForEach-Object { Id $_ })
    $delta = Get-O1IdDelta @($a) $current
    Require ($delta -is [string[]] -and $delta.Count -eq 32)
    Require (($delta -join ',') -ceq ((2..33 | ForEach-Object { Id $_ }) -join ','))
}
Check 'Inputs unchanged' {
    $original = @($b.ToUpperInvariant(), $a.ToUpperInvariant())
    $current = @($c.ToUpperInvariant(), $a, $b)
    $beforeOriginal = $original -join ','
    $beforeCurrent = $current -join ','
    $null = Get-O1IdDelta $original $current
    Require (($original -join ',') -ceq $beforeOriginal)
    Require (($current -join ',') -ceq $beforeCurrent)
}
Check 'Missing original' { Denied { Get-O1IdDelta @($a, $b) @($a) } }
Check 'Replacement with same count cannot hide missing original' {
    Denied { Get-O1IdDelta @($a, $b) @($a, $c) }
}
Check 'Missing originals in empty current' { Denied { Get-O1IdDelta @($a) @() } }
foreach ($side in @('Original', 'Current')) {
    foreach ($invalid in @('', ' ', 'not-a-guid', "'); DROP TABLE audit.AuditEvents;--", [guid]::Empty.ToString(), 42, [guid]$a)) {
        Check "$side invalid ID" {
            if ($side -eq 'Original') { Denied { Get-O1IdDelta @($invalid) @($a) } }
            else { Denied { Get-O1IdDelta @() @($invalid) } }
        }
    }
    Check "$side null element" {
        if ($side -eq 'Original') { Denied { Get-O1IdDelta @($null) @($a) } }
        else { Denied { Get-O1IdDelta @() @($null) } }
    }
    Check "$side duplicate" {
        if ($side -eq 'Original') { Denied { Get-O1IdDelta @($a, $a) @($a) } }
        else { Denied { Get-O1IdDelta @() @($a, $a) } }
    }
    Check "$side semantic duplicate casing" {
        $letterId = 'abcdef01-0001-4000-8000-000000000001'
        if ($side -eq 'Original') { Denied { Get-O1IdDelta @($letterId, $letterId.ToUpperInvariant()) @($letterId) } }
        else { Denied { Get-O1IdDelta @() @($letterId, $letterId.ToUpperInvariant()) } }
    }
    Check "$side null input" {
        if ($side -eq 'Original') { Denied { Get-O1IdDelta $null @() } }
        else { Denied { Get-O1IdDelta @() $null } }
    }
    Check "$side scalar rejected" {
        if ($side -eq 'Original') { Denied { Get-O1IdDelta $a @($a) } }
        else { Denied { Get-O1IdDelta @() $a } }
    }
}
$capacity = @(1..10000 | ForEach-Object { Id $_ })
$overCapacity = $capacity + @(Id 10001)
Check 'Exactly 10000 originals/current accepted' {
    $delta = Get-O1IdDelta $capacity $capacity
    Require ($delta.Count -eq 0)
}
Check 'Exactly 10000 new IDs accepted' {
    $delta = Get-O1IdDelta @() $capacity
    Require ($delta.Count -eq 10000)
}
Check 'Original over capacity rejected' { Denied { Get-O1IdDelta $overCapacity $capacity } }
Check 'Current over capacity rejected' { Denied { Get-O1IdDelta $capacity $overCapacity } }
Check 'Semantic GUID representation equality' {
    $delta = Get-O1IdDelta @('abcdef01-0001-4000-8000-000000000001') @('{ABCDEF01-0001-4000-8000-000000000001}')
    Require ($delta.Count -eq 0)
}

[pscustomobject]@{ Passed = $passed; Sql = $false; Http = $false; Host = $false; PrivateEvidence = $false }
