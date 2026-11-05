# Prova vera delle orecchie, in CI su Windows: Parakeet scaricato dallo scaricatore di Liquidino
# (con le impronte di Hugging Face) e un WAV vero da trascrivere.
# Il WAV viene dal repository del modello (test_wavs), se c'è; se no lo legge una voce di Windows.
param(
  [string]$Modelli = (Join-Path $env:RUNNER_TEMP "liquidino-modelli"),
  [string]$Repo = "csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8"
)
$ErrorActionPreference = "Stop"
$wav = Join-Path $env:RUNNER_TEMP "liquidino-prova.wav"
$esito = Join-Path $env:RUNNER_TEMP "liquidino-parakeet.txt"
if (Test-Path $esito) { Remove-Item $esito }

$scelto = $null
try {
  $info = Invoke-RestMethod "https://huggingface.co/api/models/$Repo"
  $albero = Invoke-RestMethod "https://huggingface.co/api/models/$Repo/tree/$($info.sha)?recursive=true"
  $wavs = @($albero | Where-Object { $_.type -eq "file" -and $_.path -like "test_wavs/*.wav" } | ForEach-Object { $_.path })
  Write-Host "WAV di prova nel repository: $($wavs -join ', ')"
  $scelto = ($wavs | Where-Object { $_ -match "(^|/)(it|ita|italian)[^/]*\.wav$" } | Select-Object -First 1)
  if (-not $scelto) { $scelto = ($wavs | Where-Object { $_ -match "(^|/)(en|eng|english)[^/]*\.wav$" } | Select-Object -First 1) }
  if (-not $scelto) { $scelto = $wavs | Select-Object -First 1 }
  if ($scelto) { Invoke-WebRequest "https://huggingface.co/$Repo/resolve/$($info.sha)/$scelto" -OutFile $wav }
} catch { Write-Host "Non trovo i WAV del repository: $($_.Exception.Message)" }

if (-not $scelto) {
  Write-Host "Leggo una frase con una voce di Windows."
  $codice = @"
Add-Type -AssemblyName System.Speech
`$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
`$s.SetOutputToWaveFile('$wav')
`$s.Speak('Hello, this is a test of the speech recognizer. Babbasone, open the music.')
`$s.Dispose()
"@
  powershell.exe -NoProfile -Command $codice
}
if (-not (Test-Path $wav)) { throw "Nessun WAV per la prova" }

$env:LIQUIDINO_PARAKEET = $Modelli
$env:LIQUIDINO_PARAKEET_WAV = $wav
$env:LIQUIDINO_PARAKEET_ESITO = $esito
dotnet test test/Liquidino.Prove/Liquidino.Prove.csproj -c Release --nologo --no-build --filter "FullyQualifiedName~ProvaParakeetVera"
$codiceUscita = $LASTEXITCODE
if (Test-Path $esito) { Get-Content $esito }
exit $codiceUscita
