# Authoritative "did Unity actually compile my change?" check.
# Unity's MCP console can come back EMPTY while compilation is failing, which silently leaves the
# previous DLL in place, so tests then pass against stale code. Editor.log is the source of truth.
#
# Staleness is checked PER ASSEMBLY: each .asmdef owns the .cs files under its own directory, so a
# UI-only edit must not flag Core.dll as stale (it did, and the false alarm is worse than useless -
# it trains you to ignore the one signal that catches real breakage).
param([int]$WaitSeconds = 14)

$proj = "C:\Users\moti\RPG Arena"
$log  = "$env:LOCALAPPDATA\Unity\Editor\Editor.log"

Start-Sleep -Seconds $WaitSeconds

# Only errors from the MOST RECENT compile count. Grepping a fixed tail reports errors from a
# compile you already FIXED, which is exactly as misleading as missing a real one - either way you
# stop trusting the one tool that catches silent breakage.
#
# Unity logs "[ScriptCompilation] Requested script compilation because: ..." when a new pass is
# queued, and the resulting errors land after it. So anything before the LAST such marker belongs to
# a superseded compile and must be ignored.
$lines = Get-Content $log -Tail 4000
$start = 0
for ($i = $lines.Count - 1; $i -ge 0; $i--) {
  if ($lines[$i] -match '\[ScriptCompilation\] Requested script compilation') { $start = $i; break }
}
$errs = $lines[$start..($lines.Count - 1)] | Select-String -Pattern "error CS" |
        ForEach-Object { $_.Line.Trim() } | Select-Object -Unique | Select-Object -Last 10

if ($errs) {
  "=== COMPILE FAILED - DLLs on disk are STALE, any test result is meaningless ==="
  $errs
} else {
  "=== compile clean ==="
}

$anyStale = $false
foreach ($asmdef in Get-ChildItem "$proj\Assets\_Project\Scripts" -Recurse -Filter *.asmdef) {
  $name = [System.IO.Path]::GetFileNameWithoutExtension($asmdef.Name)
  $dll  = "$proj\Library\ScriptAssemblies\$name.dll"
  if (-not (Test-Path $dll)) { "{0,-28} MISSING DLL" -f $name; $anyStale = $true; continue }

  $src = Get-ChildItem $asmdef.DirectoryName -Recurse -Filter *.cs -ErrorAction SilentlyContinue |
         Sort-Object LastWriteTime -Descending | Select-Object -First 1
  $dllTime = (Get-Item $dll).LastWriteTime
  $mark = "ok"
  if ($src -and $src.LastWriteTime -gt $dllTime) {
    $mark = "<-- STALE (newer: " + $src.Name + " " + $src.LastWriteTime.ToString("HH:mm:ss") + ")"
    $anyStale = $true
  }
  "{0,-28} {1}  {2}" -f "$name.dll", $dllTime.ToString("HH:mm:ss"), $mark
}

if ($anyStale) { "!!! at least one assembly is older than its own sources - refresh Unity and re-run" }
