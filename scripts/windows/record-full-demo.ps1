<#
.SYNOPSIS
  Records the COMPLETE SecureShift video walkthrough (every role, every menu option, typed inputs visible) from zero.

.DESCRIPTION
  One command does everything (Windows PowerShell 5.1, no admin rights):
    prepare   builds the offline package into C:\Demo\PenDrive (+ the PostgreSQL zip as a hard link), copies the
              live-change SQL files to C:\Demo\live, and moves YOUR C:\SecureShiftTools database aside
              (to C:\SecureShiftTools\before-demo) so the walkthrough really starts from zero
    dryrun    runs every clip WITHOUT recording: the same keys, in the same order, on the same kind of fresh
              database; checks every expected text; writes docs\demo\full\transcripts + layout.json
    tapes     writes docs\demo\full\tapes\NN-role-task.tape (VHS) from docs\demo\full\clips.js
    record    VHS records each clip in order on ONE database (clip 00 creates it from the offline package);
              a database snapshot after every clip, so  -Steps record,... -From N  re-records from clip N
    assemble  2.5 s title cards (ffmpeg drawtext), H.264 veryfast crf 26 1920x1080, the FULL video + 7 per-role
              videos, CHAPTERS.md, chapters-youtube.txt, the .srt, start times in COVERAGE.md
    check     a frame every 3 s of every clip, read with the Windows OCR: no stack trace / exception text,
              no empty screen, the typed logins readable; ffprobe: streams + durations
    restore   stops the demo PostgreSQL and puts your own C:\SecureShiftTools database back
    cleanup   deletes C:\Demo (raw clips, frames, snapshots, the unzipped package) - run it after publishing
  Needs: VHS, ttyd, ffmpeg (+ffprobe) and the PostgreSQL 16 zip in C:\SecureShiftTools, Microsoft Edge, Node.js, .NET 8 SDK.
  RAM: one heavy job at a time (VHS + Edge + PostgreSQL ~1.5 GB); the peak is written to C:\Demo\work\ram-peak.txt.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\windows\record-full-demo.ps1
  (everything except cleanup; the videos are in C:\Demo\output)

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\windows\record-full-demo.ps1 -Steps tapes,record,assemble,check,restore -From 12
  (re-record clips 12 and later after changing clips.js; the database starts as it was after clip 11)
#>
param(
    [string]$Steps = "prepare,dryrun,tapes,record,assemble,check,restore",
    [int]$From = 0,
    [int]$To = 99,
    [string]$ToolsDir = "C:\SecureShiftTools",
    [string]$DemoDir = "C:\Demo",
    [double]$Card = 2.5
)

$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$full = Join-Path $repo "docs\demo\full"
$work = Join-Path $DemoDir "work"
$outDir = Join-Path $DemoDir "output"
$aside = Join-Path $ToolsDir "before-demo"
$stepList = @($Steps -split "," | ForEach-Object { $_.Trim().ToLower() } | Where-Object { $_ })

function Step($t) { Write-Host ""; Write-Host "== $t" -ForegroundColor Cyan }
function Ok($t) { Write-Host "[OK] $t" -ForegroundColor Green }
function Warn($t) { Write-Host "[WARNING] $t" -ForegroundColor Yellow }
function Fail($t) { Write-Host "[ERROR] $t" -ForegroundColor Red; Stop-RamMonitor; exit 1 }
function Tool($dir, $exe) { Get-ChildItem (Join-Path $ToolsDir $dir) -Recurse -Filter $exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName }

New-Item -ItemType Directory -Force $DemoDir, $work, $outDir | Out-Null
$vhs = Tool "vhs" "vhs.exe"; $ttyd = Tool "ttyd" "ttyd.exe"; $ffmpeg = Tool "ffmpeg" "ffmpeg.exe"; $ffprobe = Tool "ffmpeg" "ffprobe.exe"
$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
foreach ($p in @($vhs, $ttyd, $ffmpeg, $ffprobe)) { if (-not $p) { Fail "VHS, ttyd or ffmpeg/ffprobe is missing in $ToolsDir." } }
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { Fail "Node.js is needed (node docs\demo\full\clips.js)." }
$env:PATH = "$(Split-Path $vhs);$(Split-Path $ttyd);$(Split-Path $ffmpeg);$env:PATH"
if (Test-Path $edge) { $env:rod = "bin=$edge" }   # Edge as the headless browser (no Chromium download)
$clips = (& node (Join-Path $full "clips.js") json | Out-String) | ConvertFrom-Json

function Duration($file) {
    $d = & $ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 $file
    return [double]::Parse(($d | Select-Object -First 1), [Globalization.CultureInfo]::InvariantCulture)
}
function Pg($exe) { Join-Path $ToolsDir "pgsql\bin\$exe" }
function Conn { $t = (Get-Content (Join-Path $ToolsDir "connection-string.txt") -Raw).Trim(); $env:PGPASSWORD = ($t -replace '.*Password=', '').Trim(); return $t }
function Stop-Pg {
    if (Test-Path (Pg "pg_ctl.exe")) { & (Pg "pg_ctl.exe") -D (Join-Path $ToolsDir "pgdata") stop -m fast *> $null }
}
function Start-Pg {
    $p = Start-Process -FilePath (Pg "pg_ctl.exe") -ArgumentList @("start", "-D", (Join-Path $ToolsDir "pgdata"), "-l", (Join-Path $ToolsDir "postgres.log"), "-w") -WindowStyle Hidden -PassThru
    $p.WaitForExit(120000) | Out-Null
}
function Snapshot($mode, $file) {
    $null = Conn
    if ($mode -eq "save") { & (Pg "pg_dump.exe") -h 127.0.0.1 -p 5434 -U postgres -d secureshift -Fc -n dev -f $file }
    else { & (Pg "pg_restore.exe") -h 127.0.0.1 -p 5434 -U postgres -d secureshift --clean --if-exists --no-owner $file }
    if ($LASTEXITCODE -ne 0) { Fail "$mode snapshot $file failed." }
}

# ---------------------------------------------------------------- RAM monitor: only THIS script's processes
# VHS (+ ttyd, PowerShell, the app, the headless Edge), the demo PostgreSQL, ffmpeg/ffprobe, the pg tools and
# node only when it runs clips.js. Other node.exe processes (an editor's TypeScript server, another agent) are not
# ours and are not counted. Each line: time, total MB, then MB per group.
$ramFile = Join-Path $work "ram-samples.txt"
$script:ramJob = $null
function Start-RamMonitor {
    if ($script:ramJob) { return }
    $script:ramJob = Start-Job -ArgumentList $ramFile, $ToolsDir, $DemoDir -ScriptBlock {
        param($file, $tools, $demo)
        while ($true) {
            $all = Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, Name, WorkingSetSize, ExecutablePath, CommandLine
            $kids = @{}; foreach ($p in $all) { $kids[[int]$p.ParentProcessId] += @($p) }
            $set = @{}; $group = @{}
            $roots = $all | Where-Object {
                ($_.Name -in @("vhs.exe", "ffmpeg.exe", "ffprobe.exe", "pg_dump.exe", "pg_restore.exe", "psql.exe")) -or
                ($_.Name -eq "postgres.exe" -and $_.ExecutablePath -and ($_.ExecutablePath.StartsWith($tools, "OrdinalIgnoreCase") -or $_.ExecutablePath.StartsWith($demo, "OrdinalIgnoreCase"))) -or
                ($_.Name -eq "node.exe" -and $_.CommandLine -match "clips\.js") }
            foreach ($r in $roots) {
                $stack = New-Object System.Collections.Stack; $stack.Push($r)
                while ($stack.Count) {
                    $p = $stack.Pop(); if ($set.ContainsKey([int]$p.ProcessId)) { continue }
                    $set[[int]$p.ProcessId] = $p; $group[$r.Name] += [double]$p.WorkingSetSize
                    foreach ($c in @($kids[[int]$p.ProcessId])) { if ($c) { $stack.Push($c) } }
                }
            }
            $mb = [math]::Round((($set.Values | Measure-Object WorkingSetSize -Sum).Sum) / 1MB)
            $parts = ($group.Keys | Sort-Object | ForEach-Object { "{0}={1}" -f ($_ -replace '\.exe$', ''), [math]::Round($group[$_] / 1MB) }) -join " "
            Add-Content -Path $file -Value ("{0:HH:mm:ss} {1} {2}" -f (Get-Date), $mb, $parts)
            Start-Sleep 3
        }
    }
}
function Stop-RamMonitor {
    if ($script:ramJob) { Stop-Job $script:ramJob; Remove-Job $script:ramJob -Force; $script:ramJob = $null }
    if (Test-Path $ramFile) {
        $peak = (Get-Content $ramFile | ForEach-Object { [int](($_ -split " ")[1]) } | Measure-Object -Maximum).Maximum
        Set-Content -Path (Join-Path $work "ram-peak.txt") -Value "$peak MB" -Encoding ascii
        Write-Host "Peak RAM of the recording processes: $peak MB"
    }
}

# ---------------------------------------------------------------- reset: no PostgreSQL, no unzipped package
function Reset-Demo {
    Stop-Pg
    if (-not (Test-Path $aside)) { Fail "$aside is missing - run the 'prepare' step first (it keeps your own database safe)." }
    foreach ($x in "pgsql", "pgdata", "postgres-password.txt", "connection-string.txt", "postgres.log") { Remove-Item -Recurse -Force (Join-Path $ToolsDir $x) -ErrorAction SilentlyContinue }
    Remove-Item -Recurse -Force (Join-Path $DemoDir "SecureShift") -ErrorAction SilentlyContinue
}

# ================================================================ prepare
if ($stepList -contains "prepare") {
    Step "prepare: offline package, PostgreSQL zip, live-change SQL, your database moved aside"
    $pen = Join-Path $DemoDir "PenDrive"
    New-Item -ItemType Directory -Force $pen, (Join-Path $DemoDir "live") | Out-Null
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "make-offline-package.ps1") -Output (Join-Path $pen "SecureShift-offline.zip") | Out-Host
    if ($LASTEXITCODE -ne 0) { Fail "make-offline-package.ps1 failed." }
    $zip = Get-ChildItem $ToolsDir -Filter "postgresql-16*-windows-x64-binaries.zip" -File | Select-Object -First 1
    if (-not $zip) { Fail "postgresql-16*-windows-x64-binaries.zip is not in $ToolsDir." }
    $link = Join-Path $pen $zip.Name
    if (-not (Test-Path $link)) {
        try { New-Item -ItemType HardLink -Path $link -Target $zip.FullName -ErrorAction Stop | Out-Null } catch { Copy-Item $zip.FullName $link }
    }
    Copy-Item (Join-Path $full "sql\*.sql") (Join-Path $DemoDir "live") -Force
    Stop-Pg
    if (Test-Path $aside) {
        Warn "$aside already exists (an earlier run): your own database is already safe there."
    } else {
        New-Item -ItemType Directory -Force $aside | Out-Null
        foreach ($x in "pgsql", "pgdata", "postgres-password.txt", "connection-string.txt", "postgres.log") {
            $p = Join-Path $ToolsDir $x
            if (Test-Path $p) { Move-Item $p $aside }
        }
        Ok "Your PostgreSQL + data moved to $aside (the 'restore' step puts them back)."
    }
    Ok "C:\Demo\PenDrive has the offline package and the PostgreSQL zip."
}

# ================================================================ dry run
if ($stepList -contains "dryrun") {
    Step "dryrun: every clip without recording"
    if ($From -eq 0) { Reset-Demo }
    Start-RamMonitor
    & node (Join-Path $full "clips.js") dryrun --from $From --to $To
    $code = $LASTEXITCODE
    Stop-RamMonitor
    if ($code -ne 0) { Fail "The dry run found problems (see above). Fix docs\demo\full\clips.js and run again with -Steps dryrun -From N." }
    Ok "Dry run passed."
}

# ================================================================ tapes
if ($stepList -contains "tapes") {
    Step "tapes"
    & node (Join-Path $full "clips.js") tapes
    if ($LASTEXITCODE -ne 0) { Fail "Writing the tapes failed." }
}

# ================================================================ record
if ($stepList -contains "record") {
    Step "record: VHS, one clip after the other on one database"
    $raw = Join-Path $work "raw"; $snap = Join-Path $work "snapshots"
    New-Item -ItemType Directory -Force $raw, $snap | Out-Null
    if ($From -eq 0) { Reset-Demo }
    else {
        if (-not (Test-Path (Pg "pg_ctl.exe"))) { Fail "No demo PostgreSQL: record from clip 00 first." }
        Start-Pg
        $prev = $clips | Where-Object { $_.n -lt $From } | Select-Object -Last 1
        Snapshot "restore" (Join-Path $snap "$($prev.file).dump")
        Ok "Database restored to the state after $($prev.file)."
    }
    Start-RamMonitor
    Push-Location $work
    foreach ($c in $clips | Where-Object { $_.n -ge $From -and $_.n -le $To }) {
        $tape = Join-Path $full "tapes\$($c.file).tape"
        Write-Host "-- $($c.file)" -ForegroundColor Cyan
        $start = Get-Date
        & $vhs $tape *> (Join-Path $work "vhs-$($c.file).log")
        $mp4 = Join-Path $raw "$($c.file).mp4"
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $mp4)) { Pop-Location; Fail "VHS failed for $($c.file) (see $work\vhs-$($c.file).log)." }
        Snapshot "save" (Join-Path $snap "$($c.file).dump")
        Ok ("{0}: {1:0.0} s of video, recorded in {2:0} s" -f $c.file, (Duration $mp4), ((Get-Date) - $start).TotalSeconds)
    }
    Pop-Location
    Stop-RamMonitor
}

# ================================================================ assemble
if ($stepList -contains "assemble") {
    Step "assemble: title cards, H.264, FULL + per-role videos, chapters"
    $raw = Join-Path $work "raw"; $enc = Join-Path $work "enc"; $cards = Join-Path $work "cards"
    New-Item -ItemType Directory -Force $enc, $cards | Out-Null
    Start-RamMonitor
    $font = "C\:/Windows/Fonts/segoeui.ttf"; $fontB = "C\:/Windows/Fonts/segoeuib.ttf"
    $enc8 = New-Object System.Text.UTF8Encoding($false)
    $x264 = @("-c:v", "libx264", "-preset", "veryfast", "-crf", "26", "-pix_fmt", "yuv420p", "-r", "25", "-g", "50", "-c:a", "aac", "-b:a", "64k", "-ar", "48000", "-ac", "2")
    $timings = @{ card = $Card; clips = @(); videos = @{} }
    $t = 0.0
    foreach ($c in $clips) {
        $rawFile = Join-Path $raw "$($c.file).mp4"
        if (-not (Test-Path $rawFile)) { Fail "Missing $rawFile - record it first." }
        $big = Join-Path $cards "$($c.file)-title.txt"; $small = Join-Path $cards "$($c.file)-small.txt"
        [IO.File]::WriteAllText($big, $c.title, $enc8); [IO.File]::WriteAllText($small, $c.small, $enc8)
        $bigF = ($big -replace '\\', '/') -replace ':', '\:'; $smallF = ($small -replace '\\', '/') -replace ':', '\:'
        $cardMp4 = Join-Path $cards "$($c.file).mp4"   # not $card: PowerShell names ignore case, $card IS the [double] -Card
        # expansion=none: a "%" in a title (e.g. "15/20/45/20 %") is text, not a drawtext expansion
        $vf = "drawtext=fontfile='$fontB':textfile='$bigF':expansion=none:fontsize=58:fontcolor=white:x=(w-text_w)/2:y=h*0.40," +
              "drawtext=fontfile='$font':textfile='$smallF':expansion=none:fontsize=30:fontcolor=0xB8C4D0:x=(w-text_w)/2:y=h*0.40+110," +
              "drawtext=fontfile='$font':text='SecureShift console walkthrough':fontsize=26:fontcolor=0x6E8296:x=(w-text_w)/2:y=h*0.40-80"
        & $ffmpeg -v error -y -f lavfi -i "color=c=0x0D1117:s=1920x1080:r=25:d=$Card" -f lavfi -i "anullsrc=channel_layout=stereo:sample_rate=48000" -t $Card -vf $vf @x264 $cardMp4
        if ($LASTEXITCODE -ne 0) { Fail "Title card for $($c.file) failed." }
        $encFile = Join-Path $enc "$($c.file).mp4"
        if ((Test-Path $encFile) -and (Get-Item $encFile).LastWriteTime -gt (Get-Item $rawFile).LastWriteTime) {
            Write-Host "  $($c.file): already encoded (newer than the raw clip)"
        } else {
            & $ffmpeg -v error -y -i $rawFile -f lavfi -i "anullsrc=channel_layout=stereo:sample_rate=48000" -map 0:v -map 1:a -shortest @x264 $encFile
            if ($LASTEXITCODE -ne 0) { Remove-Item $encFile -ErrorAction SilentlyContinue; Fail "Encoding $($c.file) failed." }
        }
        $cd = Duration $cardMp4; $d = Duration $encFile
        $timings.clips += @{ file = $c.file; start = [math]::Round($t, 3); card = $cd; duration = $d; video = $c.video }
        $t += $cd + $d
        Write-Host ("  {0}: {1:0.0} s" -f $c.file, $d)
    }
    $timings.total = [math]::Round($t, 3)
    function Join-Videos($list, $out) {
        $txt = Join-Path $work ("concat-" + [IO.Path]::GetFileNameWithoutExtension($out) + ".txt")
        $lines = foreach ($c in $list) { "file '" + ((Join-Path $cards "$($c.file).mp4") -replace '\\', '/') + "'"; "file '" + ((Join-Path $enc "$($c.file).mp4") -replace '\\', '/') + "'" }
        [IO.File]::WriteAllLines($txt, $lines, $enc8)
        & $ffmpeg -v error -y -f concat -safe 0 -i $txt -c copy -movflags +faststart $out
        if ($LASTEXITCODE -ne 0) { Fail "Joining $out failed." }
        return (Duration $out)
    }
    $fullMp4 = Join-Path $outDir "SecureShift-FULL-walkthrough.mp4"
    $fullDur = Join-Videos $clips $fullMp4
    Ok ("SecureShift-FULL-walkthrough.mp4: {0:0.0} s (sum of cards + clips {1:0.0} s)" -f $fullDur, $t)
    foreach ($v in ($clips | Select-Object -ExpandProperty video -Unique)) {
        $list = @($clips | Where-Object { $_.video -eq $v })
        $d = Join-Videos $list (Join-Path $outDir "SecureShift-$v.mp4")
        $timings.videos[$v] = $d
        Ok ("SecureShift-{0}.mp4: {1:0.0} s" -f $v, $d)
    }
    $timings.full = $fullDur
    $tj = Join-Path $work "timings.json"
    [IO.File]::WriteAllText($tj, ($timings | ConvertTo-Json -Depth 5), $enc8)
    & node (Join-Path $full "clips.js") docs $tj
    foreach ($f in "CHAPTERS.md", "chapters-youtube.txt", "SecureShift-FULL-walkthrough.srt") { Copy-Item (Join-Path $full $f) $outDir -Force }
    Stop-RamMonitor
}

# ================================================================ check
if ($stepList -contains "check") {
    Step "check: ffprobe + a frame every 3 s read with the Windows OCR"
    $enc = Join-Path $work "enc"; $frames = Join-Path $work "frames"
    $report = New-Object System.Collections.Generic.List[string]
    $problems = 0
    # ffprobe: streams + durations
    $tj = Get-Content (Join-Path $work "timings.json") -Raw | ConvertFrom-Json
    foreach ($f in Get-ChildItem $outDir -Filter *.mp4) {
        $s = & $ffprobe -v error -show_entries stream=codec_type,codec_name,width,height,r_frame_rate -of csv=p=0 $f.FullName
        $ok = ($s -match "^h264,video,1920,1080,25/1|^h264,1920,1080,video,25/1|h264.*1920.*1080") -and ($s -match "aac")
        $report.Add(("{0}: {1:0.0} s · {2}" -f $f.Name, (Duration $f.FullName), ($s -join " | ")))
        if (-not $ok) { $problems++; $report.Add("  PROBLEM: unexpected streams") }
    }
    $diff = [math]::Abs($tj.full - $tj.total)
    $report.Add(("FULL duration {0:0.00} s vs sum of title cards + clips {1:0.00} s: difference {2:0.00} s" -f $tj.full, $tj.total, $diff))
    if ($diff -gt 1.5) { $problems++; $report.Add("  PROBLEM: the FULL video length does not match the clips") }

    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
    $null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
    $null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics, ContentType = WindowsRuntime]
    $asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
    function Await($op, [Type]$type) { $task = $asTask.MakeGenericMethod($type).Invoke($null, @($op)); $task.Wait(-1) | Out-Null; $task.Result }
    $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
    function Read-Frame($png) {
        $file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($png)) ([Windows.Storage.StorageFile])
        $stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
        try {
            $decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
            $bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
            return (Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])).Text
        } finally { $stream.Dispose() }
    }
    $total = 0
    foreach ($c in $clips) {
        $dir = Join-Path $frames $c.file
        Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force $dir | Out-Null
        & $ffmpeg -v error -i (Join-Path $enc "$($c.file).mp4") -vf "fps=1/3" (Join-Path $dir "f%04d.png")
        $pngs = Get-ChildItem $dir -Filter *.png | Sort-Object Name
        $all = New-Object System.Text.StringBuilder
        $empty = 0
        foreach ($png in $pngs) {
            $text = Read-Frame $png.FullName
            [void]$all.AppendLine($text)
            $sec = ([int]($png.BaseName.Substring(1)) - 1) * 3
            if ($text -match "Unhandled exception|Exception:|StackTrace|NullReference|Unexpected problem|\bat SecureShift\.|\bat System\.") {
                $problems++; $report.Add(("  PROBLEM {0} at {1}s: {2}" -f $c.file, $sec, ($Matches[0])))
            }
            if ($text.Trim().Length -lt 15 -and $sec -gt 3) { $empty++ }
        }
        $total += $pngs.Count
        $text = $all.ToString()
        # the login typed at the start of the clip must be readable in the frames
        $need = switch -Regex ($c.file) { "super-admin" { "arya"; break } "cloud-admin" { "aarav"; break } "^\d+-admin-" { "sachin"; break } "architect" { "amanjeet"; break } "executive" { "shivanand"; break } default { $null } }
        $readable = if ($need) { $text -match $need } else { $true }
        $report.Add(("{0}: {1} frames, {2} nearly empty{3}" -f $c.file, $pngs.Count, $empty, $(if ($readable) { "" } else { " - PROBLEM: the typed login was not readable" })))
        if (-not $readable) { $problems++ }
        if ($empty -gt [math]::Max(2, $pngs.Count / 10)) { $problems++; $report.Add("  PROBLEM: too many empty frames in $($c.file)") }
    }
    $report.Insert(0, "Frame check: $total frames (one every 3 s), Windows OCR ($($engine.RecognizerLanguage.LanguageTag)); problems: $problems")
    [IO.File]::WriteAllLines((Join-Path $work "check-report.txt"), $report, (New-Object System.Text.UTF8Encoding($false)))
    $report | ForEach-Object { Write-Host $_ }
    if ($problems -gt 0) { Warn "$problems problem(s): see $work\check-report.txt" } else { Ok "No problems in $total frames." }
}

# ================================================================ restore
if ($stepList -contains "restore") {
    Step "restore: your own C:\SecureShiftTools database back"
    Stop-Pg
    if (Test-Path $aside) {
        foreach ($x in "pgsql", "pgdata", "postgres-password.txt", "connection-string.txt", "postgres.log") { Remove-Item -Recurse -Force (Join-Path $ToolsDir $x) -ErrorAction SilentlyContinue }
        Get-ChildItem $aside | ForEach-Object { Move-Item $_.FullName $ToolsDir }
        Remove-Item -Recurse -Force $aside
        Ok "Restored (PostgreSQL is stopped; start it with scripts\windows\run.ps1 or setup-postgres-portable.ps1)."
    } else { Warn "Nothing to restore ($aside does not exist)." }
}

# ================================================================ cleanup
if ($stepList -contains "cleanup") {
    Step "cleanup: delete $DemoDir"
    if (Test-Path $aside) { Fail "Run the 'restore' step first ($aside still exists)." }
    Remove-Item -Recurse -Force $DemoDir -ErrorAction SilentlyContinue
    if (Test-Path $DemoDir) { Warn "Some files in $DemoDir could not be deleted (in use?)." } else { Ok "$DemoDir deleted." }
}
