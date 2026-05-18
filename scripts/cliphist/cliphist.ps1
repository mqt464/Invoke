$historyPath = Join-Path $PSScriptRoot "history.json"

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

function Read-History {
    if (-not (Test-Path $historyPath)) {
        return @()
    }

    try {
        $raw = Get-Content -Path $historyPath -Raw -ErrorAction Stop
        if ([string]::IsNullOrWhiteSpace($raw)) {
            return @()
        }

        $parsed = $raw | ConvertFrom-Json -ErrorAction Stop
        if ($parsed -is [System.Array]) {
            return @($parsed)
        }

        return @($parsed)
    }
    catch {
        return @()
    }
}

function New-Preview([string]$text) {
    $singleLine = ($text -replace "\r?\n", " ").Trim()
    if ($singleLine.Length -le 72) {
        return $singleLine
    }

    return $singleLine.Substring(0, 69) + "..."
}

function Matches-Filter($item, [string]$filter) {
    if ([string]::IsNullOrWhiteSpace($filter)) {
        return $true
    }

    return $item.text -like "*$filter*" -or $item.preview -like "*$filter*"
}

$request = Read-Request
$filter = if ([string]::IsNullOrWhiteSpace($request.trigger)) { $request.raw } else { $request.terms }
$maxResults = if ($request.maxResults) { [Math]::Max(1, [int]$request.maxResults) } else { 8 }

$results = @(
    Read-History |
        Where-Object { Matches-Filter $_ $filter } |
        Select-Object -First $maxResults |
        ForEach-Object {
            $copiedAt = ""
            if ($_.copiedAtUtc) {
                try {
                    $copiedAt = ([DateTime]::Parse($_.copiedAtUtc)).ToLocalTime().ToString("g")
                }
                catch {
                    $copiedAt = $_.copiedAtUtc
                }
            }

            @{
                id = $_.id
                title = $_.preview
                subtitle = if ([string]::IsNullOrWhiteSpace($copiedAt)) { "Copy back to clipboard" } else { "Copied $copiedAt" }
                kind = "Command"
                score = 1000
                action = @{
                    kind = "copy-text"
                    title = "Copy"
                    text = $_.text
                }
            }
        }
)

if ($results.Count -eq 0) {
    $message = if ([string]::IsNullOrWhiteSpace($filter)) { "No clips recorded yet." } else { "No clips matched search." }
    $results = @(
        @{
            id = "cliphist-empty"
            title = "Cliphist"
            subtitle = $message
            kind = "Info"
            score = -100
            action = @{
                kind = "set-query"
                title = "Clear query"
                query = ""
            }
        }
    )
}

@{
    prompt = "Cliphist"
    message = "Enter copies selected clip back to clipboard."
    results = $results
} | ConvertTo-Json -Depth 6 -Compress
