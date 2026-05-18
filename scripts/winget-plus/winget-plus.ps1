function Read-Request {
    if (-not [Console]::IsInputRedirected) {
        return [pscustomobject]@{
            raw = ""
            trigger = ""
            terms = ""
            maxResults = 8
        }
    }

    $json = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($json)) {
        return [pscustomobject]@{
            raw = ""
            trigger = ""
            terms = ""
            maxResults = 8
        }
    }

    return $json | ConvertFrom-Json
}

function Invoke-WingetCapture([string[]]$Arguments) {
    $output = & winget @Arguments 2>&1
    return @($output | ForEach-Object { $_.ToString() })
}

function Convert-WingetTable([string[]]$Lines, [string[]]$Headers) {
    $separatorIndex = -1
    for ($index = 0; $index -lt $Lines.Count; $index++) {
        if ($Lines[$index] -match "^\s*-{2,}") {
            $separatorIndex = $index
            break
        }
    }

    if ($separatorIndex -lt 0) {
        return @()
    }

    $rows = @()
    for ($index = $separatorIndex + 1; $index -lt $Lines.Count; $index++) {
        $line = $Lines[$index].TrimEnd()
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $cells = @($line -split "\s{2,}")
        if ($cells.Count -eq 0) {
            continue
        }

        $row = [ordered]@{}
        for ($cellIndex = 0; $cellIndex -lt $Headers.Count; $cellIndex++) {
            $value = if ($cellIndex -lt $cells.Count) { $cells[$cellIndex].Trim() } else { "" }
            $row[$Headers[$cellIndex]] = $value
        }

        $rows += [pscustomobject]$row
    }

    return $rows
}

function Search-Packages([string]$Query, [int]$MaxResults) {
    $headers = @("name", "id", "version", "match", "source")
    $rows = Convert-WingetTable (Invoke-WingetCapture @("search", "--query", $Query, "--accept-source-agreements")) $headers
    return @(
        $rows |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_.id) } |
            Select-Object -First $MaxResults |
            ForEach-Object {
                @{
                    id = "install:$($_.id)"
                    title = "Install $($_.name)"
                    subtitle = "$($_.id)  $($_.version)"
                    kind = "Command"
                    score = 900
                    action = @{
                        kind = "execute"
                        title = "Install"
                        command = "winget"
                        arguments = "install --id `"$($_.id)`" --exact --accept-source-agreements --accept-package-agreements"
                        runAsAdministrator = $true
                        requiresConfirmation = $true
                        confirmationText = "Install $($_.name)?"
                    }
                }
            }
    )
}

function Find-Installed([string]$Query, [int]$MaxResults) {
    $headers = @("name", "id", "version", "available", "source")
    $rows = Convert-WingetTable (Invoke-WingetCapture @("list", "--query", $Query, "--accept-source-agreements")) $headers
    return @(
        $rows |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_.id) } |
            Select-Object -First $MaxResults |
            ForEach-Object {
                @{
                    id = "remove:$($_.id)"
                    title = "Remove $($_.name)"
                    subtitle = "$($_.id)  installed $($_.version)"
                    kind = "Command"
                    score = 850
                    action = @{
                        kind = "execute"
                        title = "Remove"
                        command = "winget"
                        arguments = "uninstall --id `"$($_.id)`" --exact --accept-source-agreements"
                        runAsAdministrator = $true
                        requiresConfirmation = $true
                        confirmationText = "Remove $($_.name)?"
                    }
                }
            }
    )
}

function Find-Upgrades([string]$Query, [int]$MaxResults) {
    $headers = @("name", "id", "version", "available", "source")
    $rows = Convert-WingetTable (Invoke-WingetCapture @("upgrade", "--accept-source-agreements")) $headers
    if (-not [string]::IsNullOrWhiteSpace($Query)) {
        $rows = @(
            $rows | Where-Object {
                $_.name -like "*$Query*" -or $_.id -like "*$Query*"
            }
        )
    }

    return @(
        $rows |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_.id) } |
            Select-Object -First $MaxResults |
            ForEach-Object {
                @{
                    id = "upgrade:$($_.id)"
                    title = "Update $($_.name)"
                    subtitle = "$($_.version) -> $($_.available)"
                    kind = "Command"
                    score = 880
                    action = @{
                        kind = "execute"
                        title = "Update"
                        command = "winget"
                        arguments = "upgrade --id `"$($_.id)`" --exact --accept-source-agreements --accept-package-agreements"
                        runAsAdministrator = $true
                        requiresConfirmation = $true
                        confirmationText = "Update $($_.name)?"
                    }
                }
            }
    )
}

function New-HelpResults {
    return @(
        @{
            id = "help-install"
            title = "Install packages"
            subtitle = "Type: install <name>"
            kind = "Command"
            score = 300
            action = @{
                kind = "set-query"
                title = "Install query"
                query = "install "
            }
        },
        @{
            id = "help-update"
            title = "Update packages"
            subtitle = "Type: update  or  update <name>"
            kind = "Command"
            score = 299
            action = @{
                kind = "set-query"
                title = "Update query"
                query = "update "
            }
        },
        @{
            id = "help-remove"
            title = "Remove packages"
            subtitle = "Type: remove <name>"
            kind = "Command"
            score = 298
            action = @{
                kind = "set-query"
                title = "Remove query"
                query = "remove "
            }
        }
    )
}

$request = Read-Request
$query = if ([string]::IsNullOrWhiteSpace($request.trigger)) { $request.raw.Trim() } else { $request.terms.Trim() }
$maxResults = if ($request.maxResults) { [Math]::Max(1, [int]$request.maxResults) } else { 8 }

$verb = ""
$term = ""
if ($query -match "^(install|add)\s+(.+)$") {
    $verb = "install"
    $term = $Matches[2].Trim()
}
elseif ($query -match "^(remove|uninstall)\s+(.+)$") {
    $verb = "remove"
    $term = $Matches[2].Trim()
}
elseif ($query -match "^(update|upgrade)(?:\s+(.+))?$") {
    $verb = "update"
    $term = if ($Matches[1] -and $Matches[2]) { $Matches[2].Trim() } else { "" }
}
elseif (-not [string]::IsNullOrWhiteSpace($query)) {
    $verb = "install"
    $term = $query
}

$results = switch ($verb) {
    "install" { if ([string]::IsNullOrWhiteSpace($term)) { @() } else { Search-Packages $term $maxResults } }
    "remove" { if ([string]::IsNullOrWhiteSpace($term)) { @() } else { Find-Installed $term $maxResults } }
    "update" { Find-Upgrades $term $maxResults }
    default { @() }
}

if ($results.Count -eq 0) {
    $results = New-HelpResults
}

@{
    prompt = "Winget"
    message = "Use install, update, or remove. Enter runs selected winget action."
    results = $results
} | ConvertTo-Json -Depth 6 -Compress
