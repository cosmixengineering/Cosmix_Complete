# Pure definitions only: safe to load in offline tests. No IO or environment changes.
function Get-P1Profile {
    return @{
        Profile='HumaHrPermissions20261020P1';Application='Cosmix.HrPermissions.P1'
        Root='C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020P1Retry01'
        Url='https://localhost:7481';Owner='COSMIXRND\Huma.Khan'
        Server='Software-PC1\LOCALDB#9F3001F7';Pipe='np:\\.\pipe\LOCALDB#9F3001F7\tsql\query'
        Family='D9C34627-18A2-41CF-A804-821E39516303'
        DataFile='C:\Users\Huma.Khan\Cosmix_Testing.mdf';LogFile='C:\Users\Huma.Khan\Cosmix_Testing_log.ldf'
        AdminAccount='94a83b62-5b56-4450-9175-2a7a9e3261bf'
        AdminEmployee='7c505086-87ea-4fd2-980e-823ecccbe36a'
        HrAccount='8475187f-bf3b-4f9c-a3ed-4bac32253005'
        HrEmployee='35d19922-70a8-4c80-8578-e1029360e9fc'
        AdminCode='TEST-HR-P1-20261020-ADMIN';RequesterCode='TEST-HR-P1-20261020-REQUESTER'
        AdminPermissions=@('attendance.leaves.benefits.review','attendance.vehicle.policy.manage','attendance.deployments.read','attendance.deployments.decide')
        RequesterPermissions=@('attendance.deployments.read','attendance.deployments.request')
        Reason='TEST ONLY P1 bounded permission provisioning'
    }
}
function Assert-P1([bool]$value,[string]$message){if(!$value){throw $message}}
function Get-P1SqlConnectionString {
    $p=Get-P1Profile
    return "Data Source=$($p.Pipe);Initial Catalog=Cosmix_Testing;Integrated Security=True;Encrypt=False;Pooling=True;Min Pool Size=0;Max Pool Size=1;Connect Timeout=30;ConnectRetryCount=0;Application Name=Cosmix.HrPermissions.P1.Inspect"
}
function Assert-P1SqlConnectionString([string]$value){
    $connection=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($value)
    $allowed=@('Data Source','Initial Catalog','Integrated Security','Encrypt','Pooling',
        'Min Pool Size','Max Pool Size','Connect Timeout','ConnectRetryCount','Application Name')
    foreach($key in $connection.Keys){
        Assert-P1 (!$connection.ShouldSerialize($key) -or $key -cin $allowed) 'Unapproved P1 SQL connection option.'
    }
    Assert-P1 ($connection.DataSource -ceq (Get-P1Profile).Pipe -and
        $connection.InitialCatalog -ceq 'Cosmix_Testing' -and $connection.IntegratedSecurity -and
        !$connection.Encrypt -and $connection.Pooling -and $connection.MinPoolSize -eq 0 -and
        $connection.MaxPoolSize -eq 1 -and $connection.ConnectTimeout -eq 30 -and
        $connection.ConnectRetryCount -eq 0 -and
        $connection.ApplicationName -ceq 'Cosmix.HrPermissions.P1.Inspect') 'P1 SQL connection binding differs.'
}
function Assert-P1CooldownLimits([int]$cooldownSeconds,[int]$lifetimeSeconds){
    Assert-P1 ($lifetimeSeconds -ge 60 -and $lifetimeSeconds -le 600 -and
        $cooldownSeconds -ge 30 -and $cooldownSeconds -lt $lifetimeSeconds) 'Invalid P1 provider cooldown limits.'
}
function Get-P1ResendWaitMilliseconds([string]$email,[string]$resendAvailableAt,
    [datetimeoffset]$now,[int]$cooldownSeconds,[int]$lifetimeSeconds){
    Assert-P1CooldownLimits $cooldownSeconds $lifetimeSeconds
    Assert-P1 ($email -cin @('admin@example.invalid','hr@example.invalid','superadmin@example.invalid')) 'Unknown cooldown account.'
    $deadline=[datetimeoffset]::MinValue
    Assert-P1 ($resendAvailableAt -cmatch '(Z|\+00:00)$' -and
        [datetimeoffset]::TryParse($resendAvailableAt,[Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::None,[ref]$deadline) -and
        $deadline.Offset -eq [timespan]::Zero -and $now.Offset -eq [timespan]::Zero) 'Invalid server resend timestamp.'
    $remaining=($deadline-$now).TotalMilliseconds
    if($remaining -le 0){return 0}
    $wait=[int][math]::Ceiling($remaining)+250
    Assert-P1 ($wait -le $cooldownSeconds*1000+1000) 'Server resend timestamp exceeds bounded cooldown.'
    return $wait
}
function Assert-P1PhoneCounts([int]$activeCount,$approvedCounts){
    Assert-P1 ($activeCount -eq 3 -and @($approvedCounts).Count -eq 3 -and
        @($approvedCounts|Where-Object {$_ -ne 1}).Count -eq 0) 'Three already-verified approved phone bindings required.'
}
function Assert-P1Target($o){
    $p=Get-P1Profile
    foreach($key in @('Server','Owner','Family','Root','Url')){
        Assert-P1 ($o[$key] -ieq $p[$key]) "P1 target $key differs."
    }
    Assert-P1 ($o.Operator -ieq $p.Owner -and $o.Database -ceq 'Cosmix_Testing' -and
        $o.ProcessId -eq 25768 -and $o.State -ceq 'ONLINE' -and $o.Access -ceq 'MULTI_USER' -and
        $o.ReadOnly -eq $false -and $o.RootExists -eq $false -and $o.PortOccupied -eq $false -and
        $o.LinkedPath -eq $false -and $o.Triggers -eq 0 -and $o.Synonyms -eq 0 -and
        $o.ExternalDependencies -eq 0 -and $o.AccountCount -eq 3 -and $o.MarkerCount -eq 0 -and
        $o.MissingPermissionCount -eq 5 -and $o.Transport -ceq 'Named pipe' -and
        $o.Encryption -cin @('FALSE','TRUE')) 'P1 target state differs.'
    Assert-P1 (@($o.Files).Count -eq 2 -and $o.Files[0] -ieq $p.DataFile -and
        $o.Files[1] -ieq $p.LogFile) 'P1 database files differ.'
}
function Assert-P1Schema($expected,$applied){
    Assert-P1 (@($expected).Count -eq 25 -and @($applied).Count -eq 25 -and
        ($expected -join '|') -ceq ($applied -join '|')) 'P1 schema differs; no migration allowed.'
}
function Use-P1Budget($state,[bool]$login){
    Assert-P1 ($state.Requests -lt 100 -and (!$login -or $state.Logins -lt 5)) 'P1 request/MFA budget exceeded.'
    $state.Requests++
    if($login){$state.Logins++}
}
function Get-P1Commands($b,[string]$adminRoleVersion,[string]$requesterRoleVersion,
    [string]$adminAccountVersion,[string]$hrAccountVersion){
    $p=Get-P1Profile
    return @(
        @{Path='/api/v1/access/roles';Key='P1-create-admin';Body=@{code=$p.AdminCode;name=$p.AdminCode}}
        @{Path='/api/v1/access/roles';Key='P1-create-requester';Body=@{code=$p.RequesterCode;name=$p.RequesterCode}}
        @{Path="/api/v1/access/roles/$($b.AdminRole)/permissions";Key='P1-grant-admin';Body=@{
            permissions=$p.AdminPermissions;expectedRoleVersion=$adminRoleVersion;reason=$p.Reason}}
        @{Path="/api/v1/access/roles/$($b.RequesterRole)/permissions";Key='P1-grant-requester';Body=@{
            permissions=$p.RequesterPermissions;expectedRoleVersion=$requesterRoleVersion;reason=$p.Reason}}
        @{Path="/api/v1/access/accounts/$($p.AdminAccount)/assignments";Key='P1-assign-admin';Body=@{
            roleId=$b.AdminRole;scopes=@(@{kind=7;targetId=$null});effectiveFrom=$b.EffectiveFrom;effectiveTo=$b.EffectiveTo
            expectedAccountVersion=$adminAccountVersion;expectedRoleVersion=$adminRoleVersion
            configureAsInitialAccess=$false;reason=$p.Reason}}
        @{Path="/api/v1/access/accounts/$($p.HrAccount)/assignments";Key='P1-assign-requester';Body=@{
            roleId=$b.RequesterRole;scopes=@(@{kind=2;targetId=$p.HrEmployee});effectiveFrom=$b.EffectiveFrom;effectiveTo=$b.EffectiveTo
            expectedAccountVersion=$hrAccountVersion;expectedRoleVersion=$requesterRoleVersion
            configureAsInitialAccess=$false;reason=$p.Reason}}
    )
}
function Assert-P1Fields($body,[string[]]$fields){
    Assert-P1 ($body -is [Collections.IDictionary] -and $body.Count -eq $fields.Count -and
        @($body.Keys|Where-Object {$_ -cnotin $fields}).Count -eq 0) 'P1 fields differ.'
}
function Assert-P1Command([string]$path,$body,[string]$key,$b){
    $p=Get-P1Profile
    if($key -cin @('P1-create-admin','P1-create-requester')){
        Assert-P1Fields $body @('code','name')
        $code=if($key -ceq 'P1-create-admin'){$p.AdminCode}else{$p.RequesterCode}
        Assert-P1 ($path -ceq '/api/v1/access/roles' -and $body.code -ceq $code -and $body.name -ceq $code) 'P1 role differs.'
        return
    }
    $admin=$key -cin @('P1-grant-admin','P1-assign-admin')
    $role=if($admin){$b.AdminRole}else{$b.RequesterRole}
    Assert-P1 ($role -cmatch '^[0-9a-f-]{36}$' -and [guid]$role -ne [guid]::Empty) 'P1 captured role required.'
    if($key -cin @('P1-grant-admin','P1-grant-requester')){
        Assert-P1Fields $body @('permissions','expectedRoleVersion','reason')
        $codes=if($admin){$p.AdminPermissions}else{$p.RequesterPermissions}
        Assert-P1 ($path -ceq "/api/v1/access/roles/$role/permissions" -and
            ($body.permissions -join '|') -ceq ($codes -join '|')) 'P1 permissions differ.'
    } elseif($key -cin @('P1-assign-admin','P1-assign-requester')){
        Assert-P1Fields $body @('roleId','scopes','effectiveFrom','effectiveTo','expectedAccountVersion','expectedRoleVersion','configureAsInitialAccess','reason')
        $account=if($admin){$p.AdminAccount}else{$p.HrAccount}
        Assert-P1 ($path -ceq "/api/v1/access/accounts/$account/assignments" -and $body.roleId -ceq $role -and
            $body.configureAsInitialAccess -is [bool] -and !$body.configureAsInitialAccess -and @($body.scopes).Count -eq 1) 'P1 assignment differs.'
        $scope=$body.scopes[0];Assert-P1Fields $scope @('kind','targetId')
        Assert-P1 ($(if($admin){$scope.kind -eq 7 -and $null -eq $scope.targetId}else{
            $scope.kind -eq 2 -and $scope.targetId -ceq $p.HrEmployee})) 'P1 scope differs.'
        $from=[datetimeoffset]::Parse($body.effectiveFrom);$to=[datetimeoffset]::Parse($body.effectiveTo)
        Assert-P1 ($body.effectiveFrom -ceq $b.EffectiveFrom -and $body.effectiveTo -ceq $b.EffectiveTo -and
            $from.Offset -eq [timespan]::Zero -and $to-$from -eq [timespan]::FromHours(24)) 'P1 assignment window differs.'
        Assert-P1 ($body.expectedAccountVersion -cmatch '^[A-Za-z0-9+/]{11}=$') 'P1 account version required.'
    } else {throw 'P1 command not approved.'}
    Assert-P1 ($body.reason -ceq $p.Reason -and $body.expectedRoleVersion -cmatch '^[A-Za-z0-9+/]{11}=$') 'P1 version/reason differs.'
}
function Test-P1Command($path,$body,$key,$bindings){
    try {Assert-P1Command $path $body $key $bindings;return $true} catch {return $false}
}
function Test-P1Route([string]$method,[string]$path,$body,[string]$key,$bindings){
    $p=Get-P1Profile
    if($method -ceq 'GET'){
        return !$key -and $null -eq $body -and $path -cin @(
            '/api/v1/me/permissions','/api/v1/auth/web/csrf',
            "/api/v1/access/accounts/$($p.AdminAccount)","/api/v1/access/accounts/$($p.HrAccount)")
    }
    if($method -cne 'POST'){return $false}
    if($path.StartsWith('/api/v1/auth/web/',[StringComparison]::Ordinal)){
        if($key){return $false}
        try {
            switch -CaseSensitive ($path) {
                '/api/v1/auth/web/login' {
                    Assert-P1Fields $body @('email','password')
                    return $body.email -cin @('superadmin@example.invalid','admin@example.invalid','hr@example.invalid') -and
                        $body.password -is [string] -and $body.password.Length -gt 0 -and $body.password.Length -le 1024
                }
                '/api/v1/auth/web/local-testing/otp' {
                    Assert-P1Fields $body @('challengeId')
                    return [guid]$body.challengeId -ne [guid]::Empty
                }
                '/api/v1/auth/web/otp/verify' {
                    Assert-P1Fields $body @('challengeId','code')
                    return [guid]$body.challengeId -ne [guid]::Empty -and $body.code -cmatch '^[0-9]{6}$'
                }
                '/api/v1/auth/web/logout' {Assert-P1Fields $body @();return $true}
                default {return $false}
            }
        } catch {return $false}
    }
    return Test-P1Command $path $body $key $bindings
}
function Assert-P1Actors($actors){
    $p=Get-P1Profile
    Assert-P1 (@($actors).Count -eq 3) 'Exactly three existing accounts required.'
    $names=@('Engr. Tariq Mehmood (Site Supervisor)','Muhammad Rashid (Lead Engineer)','Engr. Imran Saeed (Project Director)')
    $emails=@('admin@example.invalid','hr@example.invalid','superadmin@example.invalid')
    $phones=@('+12025550102','+12025550103','+12025550101')
    for($i=0;$i -lt 3;$i++){
        $a=$actors[$i]
        Assert-P1 ($a.LoginName -ceq $emails[$i] -and $a.FullLegalName -ceq $names[$i] -and
            $a.ApprovedLoginPhoneNumber -ceq $phones[$i] -and $a.Status -eq 3 -and
            $a.WebAccessEnabled -eq $true -and $a.MfaRequired -eq $true -and $a.Unlocked -eq $true -and
            [guid]$a.Id -ne [guid]::Empty -and [guid]$a.EmployeeId -ne [guid]::Empty) 'Synthetic identity differs.'
    }
    Assert-P1 ([guid]$actors[0].Id -eq [guid]$p.AdminAccount -and [guid]$actors[0].EmployeeId -eq [guid]$p.AdminEmployee -and
        [guid]$actors[1].Id -eq [guid]$p.HrAccount -and [guid]$actors[1].EmployeeId -eq [guid]$p.HrEmployee -and
        @($actors.Id | ForEach-Object {([guid]$_).ToString('D')} | Select-Object -Unique).Count -eq 3 -and
        @($actors.EmployeeId | ForEach-Object {([guid]$_).ToString('D')} | Select-Object -Unique).Count -eq 3) 'Synthetic account binding differs.'
}
