# Costruisce l'installer. Si lancia da Windows, nella cartella del repository:
#   powershell -ExecutionPolicy Bypass -File scripts\costruisci.ps1

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$radice = Split-Path -Parent $PSScriptRoot
Set-Location $radice

dotnet test "$radice\Liquidino.sln" -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& (Join-Path $PSScriptRoot 'pacchetto.ps1')
Write-Host "Fatto: Releases\Liquidino-Setup.exe"
