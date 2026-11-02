param(
    [Parameter(Mandatory=$true)][string]$CaptureDirectory,
    [string]$Ffmpeg='C:/Users/Hyeon/AppData/Local/Wand/app-12.38.0/resources/app.asar.unpacked/static/unpacked/capture/release/bin/64bit/ffmpeg.exe',
    [string]$Ffprobe='C:/Users/Hyeon/AppData/Local/Wand/app-12.38.0/resources/app.asar.unpacked/static/unpacked/capture/release/bin/64bit/ffprobe.exe',
    [string]$OutputDirectoryName='vfr-review-v4',
    [string[]]$Clips=@(),
    [string]$CopyExistingDirectory=''
)
$ErrorActionPreference='Stop'
$capture=(Resolve-Path -LiteralPath $CaptureDirectory).Path
$destination=Join-Path $capture $OutputDirectoryName
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$records=@()
if($Clips.Count -eq 0){$Clips=@(Get-ChildItem -LiteralPath $capture -Directory | Where-Object {Test-Path -LiteralPath (Join-Path $_.FullName 'actual-time.ffconcat')} | Select-Object -ExpandProperty Name)}
if($Clips.Count -eq 0){throw 'No timestamped motion clips found'}
foreach($clip in $Clips){
    $clipPath=Join-Path $capture $clip
    $timestamps=@(Import-Csv -LiteralPath (Join-Path $clipPath 'timestamps.csv') -Encoding UTF8)
    $frames=@(Get-ChildItem -LiteralPath $clipPath -Filter 'frame-*.png')
    if($timestamps.Count -ne $frames.Count -or $frames.Count -lt 2){throw "Missing captured frames for $clip"}
    $concat=Join-Path $clipPath 'actual-time.ffconcat'
    $output=Join-Path $destination ($clip+'-actual-time.mp4')
    # Exact output PTS/frame count are verified. No -r, scale, fps filter,
    # interpolation, duplicated-frame fill or speed change.
    # The final repeated ffconcat image solely holds the last captured image for
    # its last observed interval; it is not counted as an additional capture.
    # Codec VUI uses an ordinary nominal60Hz hint with fixed_frame_rate_flag=0.
    # Container PTS remain the exact microsecond VFR capture times. This rewrites
    # only timing metadata; it does not convert the video to60fps or add frames.
    if($CopyExistingDirectory){
        $existing=Join-Path $CopyExistingDirectory ($clip+'-actual-time.mp4')
        & $Ffmpeg -hide_banner -loglevel warning -n -i $existing -c:v copy -bsf:v 'h264_metadata=tick_rate=120:fixed_frame_rate_flag=0' -video_track_timescale 1000000 -movflags +faststart $output
    }else{
        & $Ffmpeg -hide_banner -loglevel warning -n -f concat -safe 0 -i $concat -fps_mode vfr -c:v libx264 -threads 2 -preset medium -crf 16 -pix_fmt yuv420p -level:v 4.2 -enc_time_base 1:1000000 -bsf:v 'h264_metadata=tick_rate=120:fixed_frame_rate_flag=0' -video_track_timescale 1000000 -movflags +faststart $output
    }
    if($LASTEXITCODE -ne 0){throw "VFR encoder failed for $clip"}
    $probe=(& $Ffprobe -v error -select_streams v:0 -show_entries stream=width,height,nb_frames,avg_frame_rate,duration,time_base -of json $output | ConvertFrom-Json).streams[0]
    $packetTimes=@((& $Ffprobe -v error -select_streams v:0 -show_entries frame=best_effort_timestamp_time -of csv=p=0 $output) | Where-Object {$_ -match '^\d'} | ForEach-Object {[double]($_.Split(',')[0])})
    if($LASTEXITCODE -ne 0 -or $probe.width -ne 1280 -or $probe.height -ne 720){throw "Native output geometry differs for $clip"}
    if([int]$probe.nb_frames -ne $frames.Count+1){throw "Unexpected frame duplication/drop for $clip"}
    $maxError=0.0
    for($i=0;$i -lt $timestamps.Count;$i++){
        $expected=[double]$timestamps[$i].realtime-[double]$timestamps[0].realtime
        $error=[Math]::Abs($packetTimes[$i]-$expected)
        $maxError=[Math]::Max($maxError,$error)
    }
    if($maxError -gt .0001){throw "Actual capture timestamp drift exceeds100microseconds: $clip $maxError"}
    for($i=1;$i -lt $packetTimes.Count;$i++){if($packetTimes[$i] -le $packetTimes[$i-1]){throw "Non-monotonic presentation timestamp: $clip/$i"}}
    # Use the exact VFR output clock for decode validation. The null muxer's
    # default CFR timebase otherwise quantizes close timestamps and emits false
    # duplicate-DTS warnings unrelated to video decoding or source frame loss.
    $decode=@(& $Ffmpeg -hide_banner -v error -threads 1 -i $output -fps_mode passthrough -enc_time_base 1:1000000 -f null - 2>&1)
    if($LASTEXITCODE -ne 0 -or $decode.Count -gt 0){throw "Decode validation failed for ${clip}: $($decode -join ' ')"}
    $records += [PSCustomObject]@{clip=$clip;sourceFrames=$frames.Count;encodedFrames=[int]$probe.nb_frames;terminalHoldFrames=1;width=$probe.width;height=$probe.height;duration=$probe.duration;averageFrameRate=$probe.avg_frame_rate;timeBase=$probe.time_base;maximumTimestampErrorSeconds=$maxError;strictlyIncreasingPresentationTimes=$true;nativeDecode='PASS, no errors';file=$output;sha256=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$report=[PSCustomObject]@{method='Native1280x720 VFR H264; exact actual-time.ffconcat/CSV intervals; no scale/fps conversion or synthetic intermediate frames; final image hold explicitly counted; source PNGs/CSV unchanged; nominal codec VUI60Hz with variable-rate flag does not change actual presentation times';copiedExistingEncodedPixels=[bool]$CopyExistingDirectory;quality='See original recording-quality.csv. Low capture-rate/p95 results remain LOW_RATE_NO_SMOOTHNESS_PASS. Encoding and decoding do not confer visual acceptance.';records=$records}
[IO.File]::WriteAllText((Join-Path $destination 'encoding-verification.json'),($report|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
$records|Select-Object clip,sourceFrames,encodedFrames,duration,maximumTimestampErrorSeconds
