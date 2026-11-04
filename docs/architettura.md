# Architettura di Liquidino

Stato: versione 0.2.0. Fonti: la struttura del repository, `Percorsi.cs`, `README.md`, `ROADMAP.md`. Dove non ho letto il codice per intero è scritto **da verificare**.

## Stack

- Windows a 64 bit, .NET 8 (`global.json`: SDK 8.0.100 con roll forward), C# 12, `Nullable` e `ImplicitUsings` attivi.
- Interfaccia WPF (`net8.0-windows`), audio con NAudio 2.2.1.
- Installer e aggiornamenti con Velopack (pacchetto `Velopack` e strumento `vpk` 1.2.161).
- Modelli: LFM3 di Liquid AI in formato GGUF, eseguiti in locale con llama.cpp (`llama-server` su `127.0.0.1`).
- Prove: xUnit.

## Progetti

| Progetto | Ruolo |
|---|---|
| `src/Liquidino` | App WPF: finestre (avatar, casa/chat, impostazioni, setup, novità), foglio di sprite, microfono, misura del PC, aggiornamenti, stile. |
| `src/Liquidino.Nucleo` | Logica senza interfaccia, testabile. |
| `test/Liquidino.Prove` | Prove xUnit sul nucleo (`ProveNucleo.cs`). |

### Cosa c'è nel nucleo

| File | Compito (dal nome e dai tipi, **da verificare** nei dettagli) |
|---|---|
| `Catalogo.cs` | `CatalogoAzioni`: le sei azioni (rispondi, crea nota, ricorda, dimentica, mostra le creazioni, dormi), soglia di sicurezza 0,6. `LettoreDecisione` e `DecisoreLocale` leggono la scelta del modello d1. |
| `Regista.cs` | `Regista`: tiene accesi al massimo due modelli e li scarica quando non servono. |
| `Modelli.cs` | Trova i modelli GGUF nella cartella dei modelli, ruoli, preset del motore, politica di carico. |
| `Contesto.cs` | Legge RAM e VRAM e sceglie la dimensione del contesto. |
| `ClienteLlama.cs` | Client verso il `llama-server` locale. |
| `Dialogo.cs`, `ComandiLocali.cs` | Chat e comandi che non passano dal modello. |
| `Ciclo.cs` | `CicloAutonomo`: il pensiero orario, la fase di amicizia, la notte. |
| `Mondo.cs`, `Registro.cs` | Il «mondo» su disco (note, idee, memoria, cestino) e il registro delle azioni. |
| `Orecchio.cs` | Stato dell'ascolto (il rilevamento della parola di attivazione non c'è ancora). |
| `ImpostazioniUtente.cs`, `Percorsi.cs`, `Stato.cs`, `Semi.cs`, `Testo.cs`, `NoteRilascio.cs` | Impostazioni, percorsi, stati dell'avatar, dati iniziali, utilità, note di rilascio. |

## Cartelle dati

Radice: `%LOCALAPPDATA%\Liquidino` (`Percorsi.RadiceDati()`).

| Percorso | Contenuto |
|---|---|
| `mondo/` | Il mondo di Liquidino: note, idee, memoria, cestino. |
| `mondo/memoria/cosa-so-di-te.md` | Memoria su di te, modificabile a mano. |
| `mondo/idee/` | Pensieri orari. |
| `mondo/registro/` | Registro delle azioni. Non conserva il testo cancellato. |
| `impostazioni.json` | Impostazioni dell'utente. |
| `prompt/sistema.md` | Prompt di sistema, modificabile (poi riavvia). |
| `catalogo/azioni.json` | Catalogo delle azioni, modificabile (poi riavvia). |
| `motore/modelli.ini` | Preset del motore. |
| `slot/` | Salvataggio degli slot della cache (da verificare sul PC di prova). |
| `orecchio/` | Struttura per la parola di attivazione. |
| `diario-tecnico.log` | Diario per il supporto. |

I modelli stanno **fuori** dall'installer, di norma in `C:\Users\cammo\LiquidinoProva` (cambiabile da Impostazioni). `Percorsi.StaDentro` serve a verificare che un percorso resti dentro la radice.

## Flusso di una richiesta (semplificato)

1. L'utente scrive, parla (clic su Parla o `Ctrl+Spazio`) o trascina un'immagine sull'avatar.
2. Il regista carica solo i modelli che servono, al massimo due insieme.
3. d1 sceglie una delle sei azioni del catalogo con una probabilità. Sotto 0,6 Liquidino chiede «sì» o «no».
4. L'azione viene eseguita nel mondo (non sul resto del PC) e annotata nel registro.
5. La risposta esce come testo; la voce è il testo letto da `/tts` in italiano.

## Modelli e ruoli

- **d1**: sceglie l'azione.
- **Omni**: chat e visione, se c'è. Altrimenti **LFM3-1.7B**.
- **VL**: visione, se Omni non c'è e arriva un'immagine.
- **Audio**: riconosciuto dal regista, non usato nel flusso quotidiano.
- Il ragionamento degli strumenti è spento. Di notte (23-7) dorme.

## Build e distribuzione

- `scripts/costruisci.ps1`: compilazione. `scripts/pacchetto.ps1`: `dotnet publish` self-contained `win-x64` e `vpk pack`. `scripts/note-rilascio.ps1` e `scripts/pubblica-release.ps1`: note e pubblicazione.
- CI: `.github/workflows/costruisci.yml` su Windows, con `dotnet test`, installer, artefatto e release sul ramo `main`.
- Il `.gitignore` esclude `bin/`, `obj/`, `Releases/`, `pubblicato/`, `dist/`, `TestResults/`.

## Provenienza dei pezzi

Avatar, fogli di sprite e font: vedi `risorse/PROVENIENZA.md`. Gli oggetti dell'avatar (cassetta, VHS, floppy, cartuccia, bobina, MiniDisc, CD, vinile) vengono dal DaP Convertitore e non vanno rifatti.
