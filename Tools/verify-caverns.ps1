$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$output = Join-Path $root 'Verification\Caverns'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$arguments = @('--verify-caverns', '--output', ('"' + $output + '"'), '-screen-fullscreen', '0',
    '-screen-width', '1600', '-screen-height', '900', '-logFile', ('"' + (Join-Path $output 'player.log') + '"'))
$player = Start-Process -FilePath (Join-Path $root 'Builds\Windows\Liminal.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $player.WaitForExit(150000)) {
    Stop-Process -Id $player.Id
    throw 'Cavern proof exceeded its 150-second deadline.'
}
$report = Join-Path $output 'report.json'
if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report -Encoding UTF8 }
else { Get-Content -LiteralPath (Join-Path $output 'player.log') -Tail 40 }
if ($player.ExitCode -ne 0) { throw "Cavern proof failed: exit code $($player.ExitCode)" }
