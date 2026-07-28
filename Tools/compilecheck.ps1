# Authoritative "did Unity actually compile my change?" check.
# Unity's MCP console can come back EMPTY while compilation is failing, which silently leaves the
# previous DLL in place, so tests then pass against stale code. Editor.log is the source of truth.
param([int]$WaitSeconds = 14)

$proj = "C:\Users\moti\RPG Arena"
$log  = "$env:LOCALAPPDATA\Unity\Editor\Editor.log"
$dlls = @(
  "$proj\Library\ScriptAssemblies\RPGArena.Gameplay.dll",
  "$proj\Library\ScriptAssemblies\RPGArena.UI.dll",
  "$proj\Library\ScriptAssemblies\RPGArena.Core.dll"
)

Start-Sleep -Seconds $WaitSeconds

$errs = Get-Content $log -Tail 400 | Select-String -Pattern "error CS" |
        ForEach-Object { $_.Line.Trim() } | Select-Object -Unique | Select-Object -Last 10

if ($errs) {
  "=== COMPILE FAILED - DLLs on disk are STALE, any test result is meaningless ==="
  $errs
} else {
  "=== compile clean ==="
}

$newestSrc = Get-ChildItem "$proj\Assets\_Project\Scripts" -Recurse -Filter *.cs |
             Sort-Object LastWriteTime -Descending | Select-Object -First 1
foreach ($d in $dlls) {
  if (Test-Path $d) {
    $stale = $newestSrc.LastWriteTime -gt (Get-Item $d).LastWriteTime
    $mark = "ok"
    if ($stale) { $mark = "<-- STALE" }
    "{0,-28} {1}  {2}" -f (Split-Path $d -Leaf), (Get-Item $d).LastWriteTime.ToString("HH:mm:ss"), $mark
  }
}
"newest source: " + $newestSrc.Name + " " + $newestSrc.LastWriteTime.ToString("HH:mm:ss")
