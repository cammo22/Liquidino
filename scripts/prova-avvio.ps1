# Avvia il Liquidino pubblicato con --prova-avvio: apre le finestre su dati finti ed esce.
# Si lancia dopo pacchetto.ps1 (serve la cartella pubblicato).
$ErrorActionPreference = "Stop"
$radice = Resolve-Path (Join-Path $PSScriptRoot "..")
$exe = Join-Path $radice "pubblicato\Liquidino.exe"
if (-not (Test-Path $exe)) { throw "Manca ${exe}, prima serve pacchetto.ps1" }
$base = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { $env:TEMP }
$cartella = Join-Path $base "liquidino-prova-avvio"
if (Test-Path $cartella) { Remove-Item -Recurse -Force $cartella }

$avvio = New-Object System.Diagnostics.ProcessStartInfo
$avvio.FileName = $exe
$avvio.Arguments = "--prova-avvio `"$cartella`""
$avvio.UseShellExecute = $false
$proc = [System.Diagnostics.Process]::Start($avvio)
if (-not $proc) { throw "Liquidino non parte" }
if (-not $proc.WaitForExit(200000)) {
  try { $proc.Kill($true) } catch { }
  Write-Host "La prova di avvio non finisce in tempo."
}
$esito = Join-Path $cartella "esito-avvio.txt"
if (Test-Path $esito) { Get-Content $esito | ForEach-Object { Write-Host $_ } } else { Write-Host "esito-avvio.txt non c'è" }
$diario = Join-Path $cartella "dati\diario-tecnico.log"
if (Test-Path $diario) { Write-Host "---- diario tecnico ----"; Get-Content $diario | ForEach-Object { Write-Host $_ } }
if (-not $proc.HasExited -or $proc.ExitCode -ne 0) { throw "la prova di avvio è fallita" }
if (-not (Select-String -Path $esito -Pattern "ESITO: OK" -Quiet)) { throw "la prova di avvio non ha scritto ESITO: OK" }
Write-Host "Prova di avvio: tutte le finestre si aprono e si chiudono."
