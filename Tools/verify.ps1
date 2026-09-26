param([switch]$Preview)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $root 'Builds\Windows\Liminal.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the Windows player first.' }
$output = Join-Path $root $(if ($Preview) { 'Verification\Preview' } else { 'Verification\Full' })
New-Item -ItemType Directory -Force -Path $output | Out-Null
$mode = if ($Preview) { '--preview' } else { '--verify' }
$arguments = @($mode, '--output', ('"' + $output + '"'), '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900', '-logFile', ('"' + (Join-Path $output 'player.log') + '"'))
$player = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $player.WaitForExit(260000)) {
    Stop-Process -Id $player.Id
    throw 'Runtime proof exceeded its bounded deadline.'
}
$name = if ($Preview) { 'preview.json' } else { 'report.json' }
$reportPath = Join-Path $output $name
if (Test-Path -LiteralPath $reportPath) { Get-Content -LiteralPath $reportPath -Encoding UTF8 }
else { Get-Content -LiteralPath (Join-Path $output 'player.log') -Tail 50 }
if ($player.ExitCode -ne 0) { throw "Runtime proof failed: exit code $($player.ExitCode)" }
