# Definitions only. No SQL, filesystem, HTTP, or host operations.
function Get-O1IdDelta($OriginalIds, $CurrentIds) {
    # Return one string[] object even for zero/one results; use direct assignment.
    $sets = [System.Collections.Generic.List[System.Collections.Generic.HashSet[guid]]]::new()
    foreach ($inputIds in @($OriginalIds, $CurrentIds)) {
        if ($null -eq $inputIds -or $inputIds -isnot [array] -or $inputIds.Rank -ne 1) {
            throw 'ID input must be a non-null, one-dimensional array.'
        }
        if ($inputIds.Count -gt 10000) { throw 'ID input exceeds 10000 entries.' }
        $set = [System.Collections.Generic.HashSet[guid]]::new()
        foreach ($value in $inputIds) {
            $id = [guid]::Empty
            if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value) -or
                ![guid]::TryParse($value, [ref]$id) -or $id -eq [guid]::Empty) {
                throw 'ID input contains an empty or malformed GUID string.'
            }
            if (!$set.Add($id)) { throw 'ID input contains duplicate GUIDs.' }
        }
        $sets.Add($set)
    }
    if (!$sets[0].IsSubsetOf($sets[1])) { throw 'Current IDs are missing an original ID.' }
    $delta = [System.Collections.Generic.List[string]]::new()
    foreach ($id in $sets[1]) {
        if (!$sets[0].Contains($id)) { $delta.Add($id.ToString('D')) }
    }
    $delta.Sort([System.StringComparer]::Ordinal)
    return ,($delta.ToArray())
}
