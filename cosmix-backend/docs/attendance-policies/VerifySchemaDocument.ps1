param([string]$OpenApiPath = (Join-Path $PSScriptRoot '../../artifacts/attendance-completion-http/openapi.json'))
$ErrorActionPreference = 'Stop'
$document = Get-Content -LiteralPath $OpenApiPath -Raw | ConvertFrom-Json
$checks = 0
function Resolve-Schema($schema) {
    $depth = 0
    while ($schema.'$ref') {
        if (++$depth -gt 8 -or -not $schema.'$ref'.StartsWith('#/components/schemas/')) { throw 'Unexpected policy schema reference.' }
        $name = $schema.'$ref'.Substring('#/components/schemas/'.Length)
        $schema = $document.components.schemas.$name
    }
    if ($null -eq $schema -or $schema -is [bool]) { throw 'Policy schema is absent or unconstrained.' }
    return $schema
}
$operation = $document.paths.'/api/v1/hr/attendance-policies/validate'.post
if (-not $operation -or -not $operation.security -or -not $operation.responses.'200') { throw 'Registered policy form endpoint missing.' }
$checks++
$recommendations = $document.paths.'/api/v1/hr/attendance-policies/recommended-financial-settings'.get
if (-not $recommendations -or -not $recommendations.security -or -not $recommendations.responses.'200') { throw 'Protected financial form recommendations endpoint missing.' }
$checks++
$cases = @(
    @('AttendancePaidTimeRules','basis',@('actual_elapsed_duty','scheduled_window_overlap')),
    @('AttendancePaidTimeRules','breakTreatment',@('count_all_breaks','deduct_scheduled_unpaid_break')),
    @('AttendancePaidTimeRules','earlyExitTreatment',@('actual_worked_time','half_day_credit')),
    @('AttendancePaidTimeRules','offDayTreatment',@('actual_worked_time','no_credit','scheduled_day_credit')),
    @('AttendancePaidTimeRules','holidayTreatment',@('actual_worked_time','no_credit','scheduled_day_credit')),
    @('AttendanceLateDeductionRules','resetPeriod',@('calendar_month','calendar_year','fiscal_july_june','weekly')),
    @('AttendanceLateDeductionRules','salaryBasis',@('base_salary','gross_salary')),
    @('AttendanceLateDeductionRules','salaryDivisor',@('actual_calendar_month_days','fixed_days')),
    @('AttendanceLateDeductionRules','rounding',@('whole_currency_unit_half_up','two_decimal_places_half_up','whole_currency_unit_half_even','two_decimal_places_half_even')),
    @('AttendanceOrdinaryPayRules','salaryBasis',@('base_salary','gross_salary')),
    @('AttendanceOrdinaryPayRules','salaryDivisor',@('actual_calendar_month_days','fixed_days')),
    @('AttendanceOrdinaryPayRules','rounding',@('whole_currency_unit_half_up','two_decimal_places_half_up','whole_currency_unit_half_even','two_decimal_places_half_even')),
    @('AttendanceOrdinaryPayRules','hourlyShortfallCalculation',@('daily_rate_over_scheduled_hours')),
    @('AttendanceOrdinaryPayRules','paidLeaveTreatment',@('no_deduction')),
    @('AttendanceOrdinaryPayRules','weeklyOffTreatment',@('no_deduction')),
    @('AttendanceOrdinaryPayRules','applicablePaidHolidayTreatment',@('no_deduction'))
)
foreach ($case in $cases) {
    $owner = $document.components.schemas.($case[0])
    $property = $owner.properties.($case[1])
    # ASP.NET emits Nullable<enum> as oneOf(null, component). The component must exclude null:
    # otherwise both branches accept null and the actual document rejects a valid nullable input.
    $branches = @($property.oneOf)
    if ($branches.Count -ne 2 -or @($branches | Where-Object { $_.type -eq 'null' }).Count -ne 1 -or
        @($branches | Where-Object { $_.'$ref' }).Count -ne 1) { throw "Expected exact nullable policy enum wrapper: $($case[0]).$($case[1])" }
    $checks++
    $schema = Resolve-Schema ($branches | Where-Object { $_.'$ref' })
    $types = @($schema.type)
    if ($types.Count -ne 1 -or 'string' -notin $types) { throw "Referenced enum permits null or non-string input: $($case[0]).$($case[1])" }
    $checks++
    $actual = @($schema.enum | Where-Object { $null -ne $_ })
    if (($actual -join '|') -cne ($case[2] -join '|') -or @($schema.enum).Count -ne $actual.Count) { throw "Incorrect named choices or duplicate null branch: $($case[0]).$($case[1])" }
    $checks++
    # Semantic checks over both resolved branches: null and every choice match exactly once.
    foreach ($value in @($null) + $case[2]) {
        $matches = 0
        foreach ($branch in $branches) {
            $resolved = Resolve-Schema $branch
            if ($null -eq $value) {
                if ('null' -in @($resolved.type) -and (-not $resolved.PSObject.Properties['enum'] -or $null -in @($resolved.enum))) { $matches++ }
            } elseif ('string' -in @($resolved.type) -and $value -cin @($resolved.enum)) { $matches++ }
        }
        if ($matches -ne 1) { throw "oneOf does not accept the exact policy value once: $($case[0]).$($case[1])" }
        $checks++
    }
}
$optionalCases = @(
    @('AttendancePaidTimeRules','partialLeaveHalfDayTreatment',@('half_day_total_includes_paid_leave')),
    @('AttendanceLateDeductionRules','policyChangeCountingStrategy',@('restart_on_late_rule_change')),
    @('AttendanceLateDeductionRules','salaryEffectiveDateBasis',@('threshold_completion_date')),
    @('AttendanceLateDeductionRules','deductionKind',@('day_fraction','fixed_amount','hours')),
    @('AttendanceLateDeductionRules','weekStartsOn',@('sunday','monday','tuesday','wednesday','thursday','friday','saturday')),
    @('AttendanceLateDeductionRules','overlapTreatment',@('larger_of_ordinary_or_late'))
)
foreach ($case in $optionalCases) {
    $owner = $document.components.schemas.($case[0])
    $optional = $owner.properties.($case[1])
    if (@($optional.type).Count -ne 2 -or 'string' -notin @($optional.type) -or 'null' -notin @($optional.type)) { throw "Optional versioned choice must accept legacy null: $($case[1])" }; $checks++
    $values = @($optional.enum | Where-Object { $null -ne $_ })
    if (@($optional.enum).Count -ne $case[2].Count + 1 -or ($values -join '|') -cne ($case[2] -join '|') -or $null -notin @($optional.enum)) { throw "Optional versioned choice has incorrect values: $($case[1])" }; $checks++
    if ($case[1] -in @($owner.required)) { throw "Legacy policy unexpectedly requires a new schema field: $($case[1])" }; $checks++
    if ($optional.oneOf -or $optional.'$ref') { throw "Optional inline choice has competing nullable reference branches: $($case[1])" }; $checks++
}
$enabled = Resolve-Schema $document.components.schemas.AttendanceLateDeductionRules.properties.enabled
if (@($enabled.type).Count -ne 2 -or 'boolean' -notin @($enabled.type) -or 'null' -notin @($enabled.type) -or
    'enabled' -in @($document.components.schemas.AttendanceLateDeductionRules.required)) { throw 'Versioned late enablement must be a strict nullable boolean without breaking legacy presence.' }; $checks++
if ('ordinaryPay' -in @($document.components.schemas.AttendancePolicyRules.required)) { throw 'Schema3 ordinaryPay must not become required for retained schema1/2 documents.' }; $checks++
$numbers = @{
    AttendancePolicyRules = @('schemaVersion')
    AttendanceArrivalRules = @('lateAfterShiftStartSeconds','halfDayAfterShiftStartSeconds','hourlyAfterShiftStartSeconds')
    AttendancePaidTimeRules = @('halfDayCreditSeconds')
    AttendanceLateDeductionRules = @('occurrenceThreshold','fixedSalaryDivisorDays','deductionDaysPerThreshold','fixedAmountPkr','deductionHoursPerThreshold')
    AttendanceOrdinaryPayRules = @('fixedSalaryDivisorDays','fullUnpaidDayDeductionDays','unpaidHalfDayDeductionDays')
}
$decimalFields = @('deductionDaysPerThreshold','fixedAmountPkr','deductionHoursPerThreshold','fullUnpaidDayDeductionDays','unpaidHalfDayDeductionDays')
$optionalNumbers = @('fixedAmountPkr','deductionHoursPerThreshold')
foreach ($name in $numbers.Keys) {
    $owner = $document.components.schemas.$name
    if ($owner.additionalProperties -ne $false) { throw "Unknown members allowed by policy schema: $name" }
    $checks++
    foreach ($field in $numbers[$name]) {
        $schema = Resolve-Schema $owner.properties.$field
        $kind = if ($field -in $decimalFields) { 'number' } else { 'integer' }
        $types = @($schema.type)
        if ($types.Count -ne 2 -or $kind -notin $types -or 'null' -notin $types -or $schema.pattern) { throw "Policy number advertises rejected input: $name.$field" }
        $checks++
        if ($field -in $optionalNumbers) {
            if ($field -in $owner.required) { throw "Legacy policy unexpectedly requires a conditional new amount: $name.$field" }
        } elseif ($field -notin $owner.required) { throw "Policy input lost explicit presence requirement: $name.$field" }
        $checks++
    }
    foreach ($property in $owner.properties.PSObject.Properties) {
        if ($property.Value.PSObject.Properties['default'] -and $null -ne $property.Value.default) { throw "Policy schema introduces a hardcoded business default: $name.$($property.Name)" }
        $checks++
    }
}
$save = $document.components.schemas.SaveAttendancePayrollInputsRequest
$revision = Resolve-Schema $save.properties.expectedRevision
if (@($revision.type).Count -ne 1 -or $revision.type -ne 'integer' -or $revision.pattern -or 'expectedRevision' -notin @($save.required)) { throw 'Payroll-input save revision must be an explicit strict integer.' }; $checks++
"PASS: $checks checks on the actual generated policy OpenAPI document. Metadata only; no authenticated requests or SQL."
