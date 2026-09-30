param(
    [string]$RawPath = (Join-Path $PSScriptRoot '..\Video\PV\LIMINAL_raw.mp4'),
    [string]$MetadataPath,
    [ValidateRange(1, 32)]
    [int]$Jobs = 4
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not [IO.Path]::IsPathRooted($RawPath)) { $RawPath = Join-Path $projectRoot $RawPath }
$RawPath = [IO.Path]::GetFullPath($RawPath)
if ([string]::IsNullOrWhiteSpace($MetadataPath)) { $MetadataPath = [IO.Path]::ChangeExtension($RawPath, '.json') }
elseif (-not [IO.Path]::IsPathRooted($MetadataPath)) { $MetadataPath = Join-Path $projectRoot $MetadataPath }
$MetadataPath = [IO.Path]::GetFullPath($MetadataPath)
$outputDirectory = Join-Path $projectRoot 'Video\PV'
$highPath = Join-Path $outputDirectory 'Liminal_PV_High.mp4'
$gitPath = Join-Path $outputDirectory 'Liminal_PV_Git.mp4'
$contactPath = Join-Path $outputDirectory 'contact-sheet.jpg'
$maxBytes = 95 * 1024 * 1024

$ffmpegCommand = Get-Command ffmpeg -ErrorAction Stop
$ffprobeCommand = Get-Command ffprobe -ErrorAction Stop
if (-not (Test-Path -LiteralPath $RawPath -PathType Leaf)) { throw "Raw recording not found: $RawPath" }
if (-not (Test-Path -LiteralPath $MetadataPath -PathType Leaf)) { throw "Capture metadata not found: $MetadataPath" }

$metadata = Get-Content -LiteralPath $MetadataPath -Raw | ConvertFrom-Json
$timelinePath = Join-Path $projectRoot 'Assets\Liminal\Resources\TidalMemoryTimeline.json'
$timeline = Get-Content -LiteralPath $timelinePath -Raw | ConvertFrom-Json
if ($timeline.sampleRate -ne 44100 -or $timeline.beats.Count -le 160) { throw 'Authored beat sample map is missing or invalid.' }
if ([Math]::Abs([double]$metadata.audioOffsetSeconds) -gt 2.0) { throw 'Recorded audio offset is implausible; refusing to produce misaligned cuts.' }

$probeRaw = & $ffprobeCommand.Source -v error -show_streams -show_format -of json $RawPath 2>$null
if ($LASTEXITCODE -ne 0) { throw 'ffprobe could not inspect the raw recording.' }
$rawInfo = ($probeRaw -join "`n") | ConvertFrom-Json
$rawVideo = $rawInfo.streams | Where-Object codec_type -eq 'video' | Select-Object -First 1
$rawAudio = $rawInfo.streams | Where-Object codec_type -eq 'audio' | Select-Object -First 1
if ($null -eq $rawVideo -or $null -eq $rawAudio) { throw 'Raw MP4 must contain both video and built-in mixed audio.' }
if ($rawVideo.width -ne 1920 -or $rawVideo.height -ne 1080) { throw 'Raw recording is not 1920x1080.' }
if ($metadata.frameRate -ne 30 -or -not $metadata.proofActive -or -not $metadata.cavernMode) {
    throw 'Capture metadata does not confirm the required 30 fps ProofActive cavern take.'
}

$segments = @(
    @{ Start = 0; End = 16 },
    @{ Start = 48; End = 64 },
    @{ Start = 64; End = 96 },
    @{ Start = 96; End = 104 },
    @{ Start = 128; End = 152 }
)
$audioOffset = [double]$metadata.audioOffsetSeconds
$filterParts = [Collections.Generic.List[string]]::new()
$concatInputs = [Collections.Generic.List[string]]::new()
$finalDuration = 0.0
for ($index = 0; $index -lt $segments.Count; $index++) {
    $startBeat = [int]$segments[$index].Start
    $endBeat = [int]$segments[$index].End
    $startSample = [double]$timeline.beats[$startBeat]
    $endSample = [double]$timeline.beats[$endBeat]
    $start = $audioOffset + $startSample / [double]$timeline.sampleRate
    $duration = ($endSample - $startSample) / [double]$timeline.sampleRate
    if ($start -lt -0.04) { throw "Capture begins after authored beat 0 (audio offset $audioOffset); aligned trims are impossible." }
    $start = [Math]::Max(0, $start)
    if ($start + $duration -gt [double]$rawInfo.format.duration + 0.04) {
        throw "Raw MP4 does not cover authored beats $startBeat-$endBeat after applying the audio offset."
    }

    $startText = $start.ToString('0.########', [Globalization.CultureInfo]::InvariantCulture)
    $durationText = $duration.ToString('0.########', [Globalization.CultureInfo]::InvariantCulture)
    $fadeOutStart = [Math]::Max(0, $duration - 0.03).ToString('0.########', [Globalization.CultureInfo]::InvariantCulture)
    $videoLabel = "v$index"
    $audioLabel = "a$index"
    $filterParts.Add("[0:v:0]trim=start=$startText`:duration=$durationText,setpts=PTS-STARTPTS[$videoLabel]")
    $filterParts.Add("[0:a:0]atrim=start=$startText`:duration=$durationText,asetpts=PTS-STARTPTS,afade=t=in:st=0:d=0.03,afade=t=out:st=$fadeOutStart`:d=0.03[$audioLabel]")
    $concatInputs.Add("[$videoLabel][$audioLabel]")
    $finalDuration += $duration
}
$filterParts.Add(($concatInputs -join '') + "concat=n=$($segments.Count):v=1:a=1[vcat][acat]")
$titleStart = [Math]::Max(0, $finalDuration - 1.5).ToString('0.########', [Globalization.CultureInfo]::InvariantCulture)
$filterTail = 'fps=30,setsar=1,format=yuv420p'
$fontPath = Join-Path $env:WINDIR 'Fonts\arial.ttf'
$drawtextAvailable = (& $ffmpegCommand.Source -hide_banner -filters 2>$null) -match '\bdrawtext\b'
if ($drawtextAvailable -and (Test-Path -LiteralPath $fontPath)) {
    $fontForFilter = $fontPath.Replace('\', '/').Replace(':', '\:')
    $filterTail += ",drawtext=fontfile='$fontForFilter':text='LIMINAL':fontcolor=white:fontsize=44:borderw=2:bordercolor=black@0.5:x=(w-text_w)/2:y=h-110:enable='lt(t,1.5)+gte(t,$titleStart)'"
}
$filterParts.Add("[vcat]$filterTail,scale=1920:1080:flags=lanczos[vhigh]")
$filterParts.Add('[acat]aresample=48000,asetpts=PTS-STARTPTS[ahigh]')
$filterGraph = $filterParts -join ';'

function Invoke-PvEncode([string]$OutputPath, [string]$VideoLabel, [string]$AudioLabel,
    [int]$Width, [int]$Height, [int]$Crf, [string]$MaxRate, [string]$BufferSize, [string]$AudioRate) {
    if ($Width -eq 1920) { $outputFilter = $filterGraph }
    else {
        $outputFilter = $filterGraph.Replace('scale=1920:1080:flags=lanczos[vhigh]', "scale=${Width}:${Height}:flags=lanczos[vgit]")
        $outputFilter = $outputFilter.Replace('[ahigh]', '[agit]')
    }
    $videoMap = if ($Width -eq 1920) { '[vhigh]' } else { '[vgit]' }
    $audioMap = if ($Width -eq 1920) { '[ahigh]' } else { '[agit]' }
    $arguments = @(
        '-hide_banner', '-loglevel', 'error', '-y', '-i', $RawPath,
        '-filter_complex', $outputFilter, '-map', $videoMap, '-map', $audioMap,
        '-c:v', 'libx264', '-preset', 'medium', '-crf', "$Crf", '-maxrate', $MaxRate,
        '-bufsize', $BufferSize, '-r', '30', '-pix_fmt', 'yuv420p', '-threads', "$Jobs",
        '-c:a', 'aac', '-b:a', $AudioRate, '-ar', '48000', '-ac', '2',
        '-movflags', '+faststart', $OutputPath
    )
    & $ffmpegCommand.Source @arguments
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed while creating $([IO.Path]::GetFileName($OutputPath))." }
}

Invoke-PvEncode $highPath '[vhigh]' '[ahigh]' 1920 1080 18 '16M' '32M' '256k'
Invoke-PvEncode $gitPath '[vgit]' '[agit]' 1280 720 21 '5M' '10M' '192k'

function Get-ValidatedProbe([string]$Path, [int]$Width, [int]$Height, [string]$Label) {
    $item = Get-Item -LiteralPath $Path
    if ($item.Length -gt $maxBytes) { throw "$Label exceeds the 95 MiB file-size cap ($($item.Length) bytes)." }
    $json = & $ffprobeCommand.Source -v error -show_streams -show_format -of json $Path 2>$null
    if ($LASTEXITCODE -ne 0) { throw "ffprobe failed for $Label." }
    $info = ($json -join "`n") | ConvertFrom-Json
    $video = $info.streams | Where-Object codec_type -eq 'video' | Select-Object -First 1
    $audio = $info.streams | Where-Object codec_type -eq 'audio' | Select-Object -First 1
    $duration = [double]$info.format.duration
    if ($null -eq $video -or $null -eq $audio -or $video.width -ne $Width -or $video.height -ne $Height) {
        throw "$Label has unexpected streams or dimensions."
    }
    if ($video.avg_frame_rate -ne '30/1' -or $duration -lt 45 -or $duration -gt 50) {
        throw "$Label is not a 30 fps, 45-50 second deliverable."
    }
    if ($audio.codec_name -ne 'aac' -or $audio.sample_rate -ne '48000' -or $audio.channels -ne 2) {
        throw "$Label does not contain stereo 48 kHz AAC audio."
    }
    [pscustomobject]@{
        file = [IO.Path]::GetFileName($Path)
        width = $video.width
        height = $video.height
        fps = $video.avg_frame_rate
        durationSeconds = [Math]::Round($duration, 4)
        bytes = $item.Length
        mebibytes = [Math]::Round($item.Length / 1MB, 2)
        video = $video.codec_name
        audio = $audio.codec_name
        audioRate = $audio.bit_rate
        sampleRate = $audio.sample_rate
        channels = $audio.channels
    }
}

$highSummary = Get-ValidatedProbe $highPath 1920 1080 'High PV'
$gitSummary = Get-ValidatedProbe $gitPath 1280 720 'Git PV'
$contactFps = (8.0 / [double]$highSummary.durationSeconds).ToString('0.########', [Globalization.CultureInfo]::InvariantCulture)
$contactArgs = @('-hide_banner', '-loglevel', 'error', '-y', '-i', $highPath,
    '-vf', "fps=$contactFps,scale=480:-1,tile=4x2:padding=10:margin=10", '-frames:v', '1', $contactPath)
& $ffmpegCommand.Source @contactArgs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $contactPath -PathType Leaf)) {
    throw 'ffmpeg could not produce the eight-frame contact sheet.'
}

Write-Output 'PV deliverables:'
Write-Output (ConvertTo-Json -InputObject @($highSummary, $gitSummary) -Compress -Depth 3)
Write-Output ("contactSheet={0}" -f $contactPath)
