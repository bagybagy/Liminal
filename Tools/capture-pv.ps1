param(
    [string]$UnityPath = (Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe'),
    [ValidateRange(60, 900)]
    [int]$TimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputDirectory = Join-Path $projectRoot 'Video\PV'
$rawPath = Join-Path $outputDirectory 'LIMINAL_raw.mp4'
$metadataPath = Join-Path $outputDirectory 'LIMINAL_raw.json'
$logPath = Join-Path $outputDirectory 'capture-unity.log'

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity 6000.3.13f1 executable not found: $UnityPath"
}
if ((Test-Path -LiteralPath $rawPath -PathType Leaf) -or (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw 'The one-take output already exists. Move the existing raw MP4 and metadata before starting.'
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
$arguments = '-projectPath "{0}" -executeMethod Liminal.Editor.PvCaptureSession.Run -logFile "{1}"' -f $projectRoot, $logPath
$unity = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $projectRoot -PassThru -WindowStyle Hidden

if (-not $unity.WaitForExit($TimeoutSeconds * 1000)) {
    try { $unity.Kill() } catch { }
    $unity.WaitForExit()
    $tail = if (Test-Path -LiteralPath $logPath) { (Get-Content -LiteralPath $logPath -Tail 12) -join "`n" } else { '(Unity log not created)' }
    throw "PV capture exceeded the bounded $TimeoutSeconds second wait. Unity was stopped.`n$tail"
}

$unity.Refresh()
if ($unity.ExitCode -ne 0) {
    $tail = if (Test-Path -LiteralPath $logPath) { (Get-Content -LiteralPath $logPath -Tail 12) -join "`n" } else { '(Unity log not created)' }
    throw "Unity PV capture exited with code $($unity.ExitCode).`n$tail"
}
if (-not (Test-Path -LiteralPath $rawPath -PathType Leaf) -or -not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw 'Unity exited successfully but did not produce both LIMINAL_raw.mp4 and LIMINAL_raw.json.'
}

$raw = Get-Item -LiteralPath $rawPath
Write-Output ("PV capture complete: {0:N1} MiB, metadata={1}" -f ($raw.Length / 1MB), $metadataPath)
