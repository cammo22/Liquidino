# Progetto di riferimento: Capri Desktop

Fonti: repository [lachydotmcg/capri-desktop](https://github.com/lachydotmcg/capri-desktop) (README, `CONTRIBUTING.md`, `SECURITY.md`, `site/help/knowledge.md`) consultati il 4 novembre 2026. Il sito capriai.app non era raggiungibile dalla sessione di lavoro: tutto ciò che segue viene dal repository. Le pagine lette erano lunghe; la parte finale della guida di aiuto non è stata letta per intero.

## Che cos'è

Compagno da desktop **non ufficiale** (fan project) per Windows. Si siede sulle finestre, chiacchiera per testo o voce, commenta i giochi ed esegue compiti sul PC. Open source, licenza PolyForm Noncommercial 1.0.0. Zero telemetria dichiarata. Mac e Linux sono in anteprima, con meno funzioni.

## Funzioni principali

- **Conversazione**: testo o voce, parola di attivazione «Hey Capri» (solo Windows e inglese), risposte in 80 lingue.
- **Cervello**, tre livelli: `NORMAL` locale e gratuito (Ollama o cervello automatico), `MEDIUM INTELLIGENCE` con chiavi gratuite (NVIDIA, Gemini, OpenRouter), `MAX INTELLIGENCE` con chiavi a pagamento (OpenAI, Claude, DeepSeek e altre).
- **Voce**: spenta, locale predefinita (Kokoro), locale personalizzata, voce HD locale opzionale (Qwen3-TTS), cloud (Fish Audio, ElevenLabs, OpenAI). Controlli di tono, velocità, effetti robot e glitch. Riconoscimento vocale locale con Parakeet tramite sherpa-onnx.
- **Compiti sul PC**: apre app e siti, musica e volume, gestione finestre, scorciatoie (Discord, Windows), mouse e tastiera con un cursore proprio, lettura di file, promemoria, routine, mini-giochi.
- **Giochi**: guarda lo schermo, commenta (`Off`, `Events only`, `Chatty`), usa un «Gaming Mode», può registrare clip.
- **Memoria**: chat recenti, trascrizioni giornaliere, note in `me.md`, ricordi riassunti in `memories.md`. Tutto sul PC.
- **Personalità**: file `personality.md` modificabile; le regole di sicurezza restano nel codice e non si cambiano da lì.
- **Presenza sul desktop**: si siede su finestre e bordi, balla con la musica, va in letargo, mini player, modalità silenziosa con `Ctrl+Alt+P`, dimensioni da Tiny a Giant.
- **Watch my screen**: guarda lo schermo a intervalli (da 10 secondi a 10 minuti, predefinito 2 minuti), salta schermate invariate o vuote.

## Il Mainframe (finestra impostazioni)

Sezioni: `BRAIN`, `VOICE`, `CAPRI`, `MOVES`, `WATCH`, `HABITS`, `KEYS`, `GAMES`. Ci sono una vista Semplice e una Avanzata e i tasti sono riassegnabili.

## Sicurezza dei permessi

Quattro livelli su uno slider (`Lockdown`, `Guarded`, `Trusted`, `Blackwall`) più `Custom`, con undici interruttori su Windows (cinque su Mac e Linux).

- `Lockdown`: tutto spento.
- `Trusted` (predefinito): chiede prima di pagare, acquistare, inviare, pubblicare, eliminare o installare ciò che non hai nominato.
- `Blackwall`: non chiede mai.
- Sempre: non esegue istruzioni lette sullo schermo; `Esc` interrompe subito un compito; password e numeri di carta non vengono digitati; con un interruttore spento chiede e agisce solo dopo un sì.
- Le regole di sicurezza stanno nel codice (`Safety` in `app/Brain.cs`), dopo la personalità, e non sono modificabili dall'utente.

## Stack e struttura

- Windows: C# 5, .NET Framework 4.8, WinForms (`app/`), compilato con il `csc.exe` incluso in Windows, quindi senza `$"..."`, `?.`, `nameof`, membri con `=>`.
- Mac e Linux: .NET 10 con Avalonia (`cross/`), condivide `Brain.cs`, `Engine.cs`, `Hands.cs`, `Voice.cs`.
- Cartelle: `.github/`, `app/`, `assets/` (sprite, strisce di animazione), `ci/` (script di build), `cross/`, `mods/`, `quest/`, `site/` (sito e guida), `worker/` (worker Cloudflare per contatori e chat di aiuto).
- Documenti nella radice: `CONTRIBUTING.md`, `LICENSE.md`, `README.md`, `SECURITY.md`, `THIRD-PARTY-NOTICES.md`.
- Dati utente in `%LOCALAPPDATA%\Capri` (Windows) o `~/.config/Capri` (Mac, Linux).
- Distribuzione: `Capri-Setup.exe` senza diritti di amministratore, avvio con Windows, aggiornamenti automatici ogni 3 ore con verifica di firma e checksum, `SHA256SUMS.txt` per ogni release.

## Build e prove (come le fanno loro)

- Windows: `./ci/build-windows.ps1 -SmokeTest`, produce `Capri.exe` e `Capri-Setup.exe` ed esegue uno «smoke test» in cui ogni riga deve dire `PASS`.
- Mac e Linux: `dotnet build cross/Capri.Cross.csproj -c Release -o out/cross` e poi `out/cross/Capri --smoke out/cross-smoke`.
- La CI esegue i tre controlli su ogni pull request.
- Ogni nuova funzione deve aggiungere un controllo allo smoke test.

## Licenza e attribuzione

PolyForm Noncommercial 1.0.0: uso, modifica e condivisione per scopi non commerciali, vietato venderlo. Attribuzione obbligatoria con link al progetto. Vietato clonare la voce di una persona vivente senza consenso. Componenti di terzi: Kokoro, sherpa-onnx e Qwen3-TTS (Apache-2.0), qwentts.cpp (MIT), Parakeet (CC-BY-4.0), VB-CABLE (donationware), font Share Tech Mono (OFL).

## Roadmap dichiarata da Capri

Non ancora fatto: pianificazione dei compiti prima dell'esecuzione, compiti di lunga durata (per esempio rispondere ai DM di Discord mentre si dorme), Mac e Linux alla pari di Windows con notarizzazione Apple, firma del codice Windows, gestione dei giochi migliore.

## Limiti noti dichiarati

Mac e Linux senza Mainframe, chat vocale, scorciatoie, mini player, aggiornamenti automatici. I modelli locali piccoli vanno bene per menu e clic, non per giocare con affidabilità. Il documento di aiuto si contraddice su un punto (strumento per i file mancante, ma file trascinabili): da verificare.
