# Costruisce Liquidino e fa lo zip da estrarre e avviare.
# Si lancia da Windows, nella cartella del repository:
#   powershell -ExecutionPolicy Bypass -File scripts\costruisci.ps1

$Errore = 'Stop'
Set-StrictMode -Version Latest
$radice = Split-Path -Parent $PSScriptRoot
Set-Location $radice

dotnet test "$radice\Liquidino.sln" -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$uscita = Join-Path $radice 'pubblicato'
if (Test-Path $uscita) { Remove-Item $uscita -Recurse -Force }
dotnet publish "$radice\src\Liquidino\Liquidino.csproj" -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o $uscita
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$zip = Join-Path $radice 'Liquidino-win-x64.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $uscita '*') -DestinationPath $zip
Write-Host "Fatto: $zip"
Write-Host "Dentro c'è Liquidino.exe. Doppio clic, al primo avvio nome carattere e voce."
