# Scarica, solo in CI, un llama-server CPU e un GGUF minuscolo, poi fa passare il regista
# per caricamento, una risposta e scaricamento. Il programma installato non scarica modelli.
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false
$radice = Resolve-Path (Join-Path $PSScriptRoot "..")
$lavoro = Join-Path $env:RUNNER_TEMP "liquidino-e2e"
if (-not $env:RUNNER_TEMP) { $lavoro = Join-Path $env:TEMP "liquidino-e2e" }
New-Item -ItemType Directory -Force -Path $lavoro, (Join-Path $lavoro "llama"), (Join-Path $lavoro "modelli") | Out-Null

# b12078 è la stessa generazione del PC di prova: ha il router e /models/load.
# curl, non gh: gh release download è rimasto appeso per decine di minuti senza un tetto.
$tag = "b12078"
$nome = "llama-$tag-bin-win-cpu-x64.zip"
$zipScaricato = Join-Path $lavoro $nome
$urlLlama = "https://github.com/ggml-org/llama.cpp/releases/download/$tag/$nome"
Write-Host "$(Get-Date -Format o) Scarico llama.cpp $urlLlama"
curl.exe -fL --retry 2 --retry-all-errors --connect-timeout 20 --max-time 180 -o $zipScaricato $urlLlama
if ($LASTEXITCODE -ne 0) { throw "download llama.cpp" }
Write-Host "$(Get-Date -Format o) zip $((Get-Item $zipScaricato).Length) byte, lo apro"
Expand-Archive -Path $zipScaricato -DestinationPath (Join-Path $lavoro "llama") -Force
Get-ChildItem (Join-Path $lavoro "llama") -Recurse -File | Unblock-File -ErrorAction SilentlyContinue
$exe = Get-ChildItem (Join-Path $lavoro "llama") -Recurse -Filter "llama-server.exe" | Select-Object -First 1
if (-not $exe) { $exe = Get-ChildItem (Join-Path $lavoro "llama") -Recurse -Filter "llama.exe" | Select-Object -First 1 }
if (-not $exe) { throw "nello zip non c'è llama-server.exe né llama.exe" }
# il regista cerca llama-server.exe entro tre livelli dalla cartella del motore
if ($exe.DirectoryName -ne (Join-Path $lavoro "llama")) {
  Copy-Item (Join-Path $exe.DirectoryName "*") (Join-Path $lavoro "llama") -Force
}

$gguf = Join-Path $lavoro "modelli\prova-1.7.gguf"
Write-Host "$(Get-Date -Format o) Scarico un GGUF piccolo solo per questa prova"
curl.exe -fL --retry 2 --retry-all-errors --connect-timeout 20 --max-time 180 -A "Liquidino-CI" -o $gguf "https://huggingface.co/ggml-org/models/resolve/main/tinyllamas/stories15M-q4_0.gguf"
if ($LASTEXITCODE -ne 0) { throw "download GGUF" }
if ((Get-Item $gguf).Length -lt 100000) { throw "il GGUF è troppo piccolo, il download non è riuscito" }
Unblock-File $gguf -ErrorAction SilentlyContinue
Write-Host "$(Get-Date -Format o) GGUF $((Get-Item $gguf).Length) byte, parte la prova"

$env:LIQUIDINO_E2E = "1"
$env:LIQUIDINO_E2E_ROOT = $lavoro
$progetto = Join-Path $radice "test\Liquidino.Prove\Liquidino.Prove.csproj"
$avvio = New-Object System.Diagnostics.ProcessStartInfo
$avvio.FileName = "dotnet"
$avvio.Arguments = "test `"$progetto`" -c Release --nologo --filter Il_regista_carica_risponde_e_scarica"
$avvio.UseShellExecute = $false
$avvio.RedirectStandardOutput = $true
$avvio.RedirectStandardError = $true
$proc = [System.Diagnostics.Process]::Start($avvio)
if (-not $proc) { throw "non parte dotnet test" }
$stdout = $proc.StandardOutput.ReadToEndAsync()
$stderr = $proc.StandardError.ReadToEndAsync()
$scadenza = [DateTime]::UtcNow.AddMinutes(5)
$serverLog = Join-Path $lavoro "dati\motore\server.log"
while (-not $proc.HasExited -and [DateTime]::UtcNow -lt $scadenza) {
  Start-Sleep -Seconds 8
  if (Test-Path $serverLog) {
    Write-Host "---- $(Get-Date -Format o) server.log ----"
    Get-Content $serverLog -Tail 30 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
  } else {
    Write-Host "$(Get-Date -Format o) in corso, server.log non c'è ancora"
  }
}
if (-not $proc.HasExited) {
  Write-Host "La prova supera i 5 minuti: chiudo l'albero dei processi."
  & taskkill.exe /F /T /PID $proc.Id
  Start-Sleep -Seconds 2
}
if (-not $proc.WaitForExit(8000)) { try { $proc.Kill($true) } catch { } }
Write-Host "---- stdout ----"
try { Write-Host $stdout.Result } catch { Write-Host $_.Exception.Message }
Write-Host "---- stderr ----"
try { Write-Host $stderr.Result } catch { Write-Host $_.Exception.Message }
$esitoRiempimento = Join-Path $lavoro "esito-riempimento.txt"
if (Test-Path $esitoRiempimento) {
  Write-Host "---- prova di carico ----"
  Get-Content $esitoRiempimento | ForEach-Object { Write-Host $_ }
}
if (Test-Path $serverLog) {
  Write-Host "---- server.log intero ----"
  Get-Content $serverLog -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
}
if (-not $proc.HasExited -or $proc.ExitCode -ne 0) { throw "la prova del motore è fallita" }
Write-Host "Motore di prova: carico, risposta, scarico."
