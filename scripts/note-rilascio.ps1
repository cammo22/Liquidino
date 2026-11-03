# Stampa le note della versione: dal CHANGELOG, dalla voce ## X.Y.Z fino alla voce dopo.
param(
  [Parameter(Mandatory = $true)][string]$Changelog,
  [Parameter(Mandatory = $true)][string]$Versione
)
$dentro = $false
$righe = foreach ($riga in ($Changelog -split "`r?`n")) {
  if ($riga -match '^## ') {
    if ($dentro) { break }
    $dentro = $riga.Contains($Versione)
    continue
  }
  if ($dentro) { $riga }
}
$testo = ($righe -join "`n").Trim()
if (-not $testo) { throw "Nel CHANGELOG non c'è la versione $Versione" }
foreach ($voce in @('### Novità', '### Correzioni', '### Da provare')) {
  if ($testo -notlike "*$voce*") { throw "Manca '$voce' nelle note di $Versione" }
}
$testo
