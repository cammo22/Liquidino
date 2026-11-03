# Crea la release vX.Y.Z se non c'è già. Prima il Setup, poi i file degli aggiornamenti.
# Le note sono quelle del CHANGELOG (Novità, Correzioni, Da provare).
param(
  [Parameter(Mandatory = $true)][string]$Versione,
  [Parameter(Mandatory = $true)][string]$Note,
  [Parameter(Mandatory = $true)][string]$Cartella
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false
$tag = "v$Versione"
gh release view $tag *> $null
if ($LASTEXITCODE -eq 0) {
  Write-Host "La $tag c'è già."
  exit 0
}

$setup = Join-Path $Cartella "Liquidino-Setup.exe"
if (-not (Test-Path $setup)) { throw "Manca $setup" }
gh release create $tag $setup --title "Liquidino $Versione" --notes-file $Note
if ($LASTEXITCODE -ne 0) { throw "creazione release" }

$poi = Get-ChildItem $Cartella -File | Where-Object {
  $_.Name -ne "Liquidino-Setup.exe" -and $_.Extension -ne ".zip" -and $_.Name -notlike "*-Setup.exe"
} | Sort-Object Name
foreach ($file in $poi) {
  gh release upload $tag $file.FullName --clobber
  if ($LASTEXITCODE -ne 0) { throw "upload $($file.Name)" }
}
Write-Host "Pubblicata $tag"
