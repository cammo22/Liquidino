# Impacchetta Liquidino con Velopack (come il DaP Convertitore).
# Esce Releases\Liquidino-Setup.exe, più i file per gli aggiornamenti (niente zip).
# Le note si prendono dal CHANGELOG, la versione da Directory.Build.props.
param([switch]$SenzaCompilare)

$ErrorActionPreference = "Stop"
$radice = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $radice
try {
  [xml]$props = Get-Content (Join-Path $radice "Directory.Build.props") -Encoding UTF8
  $versione = $props.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
  if (-not $versione) { throw "Versione non trovata in Directory.Build.props" }
  Write-Host "Liquidino $versione"

  if (-not $SenzaCompilare) {
    dotnet publish (Join-Path $radice "src\Liquidino\Liquidino.csproj") -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o (Join-Path $radice "pubblicato")
    if ($LASTEXITCODE -ne 0) { throw "publish" }
  }

  $changelog = Get-Content (Join-Path $radice "CHANGELOG.md") -Encoding UTF8 -Raw
  $note = & (Join-Path $PSScriptRoot "note-rilascio.ps1") -Changelog $changelog -Versione "$versione"
  $fileNote = Join-Path ([IO.Path]::GetTempPath()) "liquidino-note.md"
  [IO.File]::WriteAllText($fileNote, $note.Trim() + "`n", (New-Object Text.UTF8Encoding $false))

  dotnet tool restore | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "tool restore" }

  $uscite = Join-Path $radice "Releases"
  New-Item -ItemType Directory -Force -Path $uscite | Out-Null
  dotnet vpk pack -u Liquidino -v $versione -p (Join-Path $radice "pubblicato") -e Liquidino.exe `
    --packTitle "Liquidino" --packAuthors "DaProd" `
    --icon (Join-Path $radice "risorse\icona.ico") `
    --shortcuts Desktop,StartMenu `
    --releaseNotes $fileNote -o $uscite
  if ($LASTEXITCODE -ne 0) { throw "vpk pack" }

  $setup = Get-ChildItem $uscite -Filter "*-Setup.exe" | Select-Object -First 1
  if (-not $setup) { throw "Manca il Setup di Velopack" }
  Copy-Item $setup.FullName (Join-Path $uscite "Liquidino-Setup.exe") -Force
  Get-ChildItem $uscite -Filter "*Portable.zip" -ErrorAction SilentlyContinue | Remove-Item -Force
  $portatile = Join-Path $uscite "Liquidino-portatile.zip"
  if (Test-Path $portatile) { Remove-Item $portatile -Force }
  Compress-Archive -Path (Join-Path $radice "pubblicato\*") -DestinationPath $portatile
  Get-ChildItem $uscite | Format-Table Name, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize
} finally { Pop-Location }
