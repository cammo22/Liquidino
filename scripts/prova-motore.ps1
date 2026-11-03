# Scarica, solo in CI, un llama-server CPU e un GGUF minuscolo, poi fa passare il regista
# per caricamento, una risposta e scaricamento. Il programma installato non scarica modelli.
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false
$radice = Resolve-Path (Join-Path $PSScriptRoot "..")
$lavoro = Join-Path $env:RUNNER_TEMP "liquidino-e2e"
if (-not $env:RUNNER_TEMP) { $lavoro = Join-Path $env:TEMP "liquidino-e2e" }
New-Item -ItemType Directory -Force -Path $lavoro, (Join-Path $lavoro "llama"), (Join-Path $lavoro "modelli") | Out-Null

$zip = Join-Path $lavoro "llama-cpu.zip"
$tag = gh api repos/ggml-org/llama.cpp/releases/latest --jq .tag_name
if ($LASTEXITCODE -ne 0 -or -not $tag) { throw "non leggo l'ultima release di llama.cpp" }
$assets = gh api "repos/ggml-org/llama.cpp/releases/tags/$tag" --jq ".assets[].name"
$nome = $assets | Where-Object { $_ -like "*win-cpu-x64.zip" } | Select-Object -First 1
if (-not $nome) { throw "nella release $tag non c'è un pacchetto win-cpu-x64" }
Write-Host "Scarico llama.cpp $tag / $nome"
gh release download $tag --repo ggml-org/llama.cpp --pattern $nome --dir $lavoro --clobber
if ($LASTEXITCODE -ne 0) { throw "download llama.cpp" }
$zipScaricato = Join-Path $lavoro $nome
Expand-Archive -Path $zipScaricato -DestinationPath (Join-Path $lavoro "llama") -Force
$exe = Get-ChildItem (Join-Path $lavoro "llama") -Recurse -Filter "llama-server.exe" | Select-Object -First 1
if (-not $exe) { throw "llama-server.exe non c'è nello zip" }
# il regista cerca llama-server.exe entro tre livelli dalla cartella del motore
if ($exe.DirectoryName -ne (Join-Path $lavoro "llama")) {
  Copy-Item (Join-Path $exe.DirectoryName "*") (Join-Path $lavoro "llama") -Force
}

$gguf = Join-Path $lavoro "modelli\prova-1.7.gguf"
Write-Host "Scarico un GGUF piccolo solo per questa prova"
curl.exe -fL --retry 3 -A "Liquidino-CI" -o $gguf "https://huggingface.co/ggml-org/models/resolve/main/tinyllamas/stories15M-q4_0.gguf"
if ($LASTEXITCODE -ne 0) { throw "download GGUF" }
if ((Get-Item $gguf).Length -lt 100000) { throw "il GGUF è troppo piccolo, il download non è riuscito" }

$env:LIQUIDINO_E2E = "1"
$env:LIQUIDINO_E2E_ROOT = $lavoro
dotnet test (Join-Path $radice "test\Liquidino.Prove\Liquidino.Prove.csproj") -c Release --nologo --filter "Il_regista_carica_risponde_e_scarica"
if ($LASTEXITCODE -ne 0) { throw "la prova del motore è fallita" }
Write-Host "Motore di prova: carico, risposta, scarico."
