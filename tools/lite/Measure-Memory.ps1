# Samples memory of the Files processes and prints min/avg/max in MB.
# Usage: .\Measure-Memory.ps1 [-Label "baseline idle"] [-Samples 10] [-IntervalSeconds 3]
param(
    [string]$Label = '',
    [string[]]$ProcessName = @('Files', 'Files.App.Server'),
    [int]$Samples = 10,
    [int]$IntervalSeconds = 3
)

$rows = for ($i = 0; $i -lt $Samples; $i++) {
    foreach ($p in Get-Process -Name $ProcessName -ErrorAction SilentlyContinue) {
        [pscustomobject]@{
            Name      = $p.ProcessName
            WorkingMB = $p.WorkingSet64 / 1MB
            PrivateMB = $p.PrivateMemorySize64 / 1MB
        }
    }
    if ($i -lt $Samples - 1) { Start-Sleep -Seconds $IntervalSeconds }
}

if (-not $rows) {
    Write-Error "No running process found: $($ProcessName -join ', ')"
    exit 1
}

"[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm'), $Label
$rows | Group-Object Name | ForEach-Object {
    $ws = $_.Group | Measure-Object WorkingMB -Minimum -Average -Maximum
    $pv = $_.Group | Measure-Object PrivateMB -Minimum -Average -Maximum
    [pscustomobject]@{
        Process       = $_.Name
        'WS min/avg/max MB'      = '{0:N0} / {1:N0} / {2:N0}' -f $ws.Minimum, $ws.Average, $ws.Maximum
        'Private min/avg/max MB' = '{0:N0} / {1:N0} / {2:N0}' -f $pv.Minimum, $pv.Average, $pv.Maximum
    }
} | Format-Table -AutoSize
