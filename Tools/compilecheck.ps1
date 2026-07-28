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

# String literals inside a DLL are UTF-16, and Editor.log is written by Unity as UTF-8; read it
# without -Encoding so PowerShell picks the right one, then grep for the compiler's own marker.
$errs = Get-Content $log -Tail 400 | Select-String -Pattern "error CS" |
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
