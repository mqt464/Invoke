Add-Type -TypeDefinition @"
using System.Runtime.InteropServices;

public static class InvokeClipboardNative
{
    [DllImport("user32.dll")]
    public static extern uint GetClipboardSequenceNumber();
}
"@

$historyPath = Join-Path $PSScriptRoot "history.json"
$maxItems = 250
$lastSequence = 0

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

function Save-History($items) {
    $directory = Split-Path -Path $historyPath -Parent
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    $items |
        Select-Object -First $maxItems |
        ConvertTo-Json -Depth 6 |
        Set-Content -Path $historyPath -Encoding UTF8
}

function New-Preview([string]$text) {
    $singleLine = ($text -replace "\r?\n", " ").Trim()
    if ($singleLine.Length -le 72) {
        return $singleLine
    }

    return $singleLine.Substring(0, 69) + "..."
}

while ($true) {
    try {
        $sequence = [InvokeClipboardNative]::GetClipboardSequenceNumber()
        if ($sequence -ne 0 -and $sequence -ne $lastSequence) {
            $lastSequence = $sequence
            $text = Get-Clipboard -Raw -Format Text -ErrorAction Stop
            if (-not [string]::IsNullOrWhiteSpace($text)) {
                $history = @(
                    Read-History |
                        Where-Object { $_.text -ne $text }
                )

                $existing = Read-History | Where-Object { $_.text -eq $text } | Select-Object -First 1
                $copies = if ($existing -and $existing.copies) { [int]$existing.copies + 1 } else { 1 }
                $id = if ($existing -and $existing.id) { $existing.id } else { [guid]::NewGuid().ToString("N") }

                $entry = [pscustomobject]@{
                    id = $id
                    text = $text
                    preview = New-Preview $text
                    copies = $copies
                    copiedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
                }

                Save-History @($entry) + $history
            }
        }
    }
    catch {
    }

    Start-Sleep -Milliseconds 400
}
