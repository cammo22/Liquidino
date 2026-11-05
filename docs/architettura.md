# Architettura di Liquidino

Stato: versione 0.4.0. Scritta leggendo il codice.

## Stack

- Windows a 64 bit, .NET 8 (`global.json`: SDK 8.0.100 con roll forward), C# 12, `Nullable` e `ImplicitUsings` attivi.
- Interfaccia WPF (`net8.0-windows`); Windows Forms solo per l'icona nel vassoio e la foto dello schermo. Audio con NAudio 2.2.1, voci di Windows con System.Speech 8.0.0.
- Installer e aggiornamenti con Velopack (pacchetto `Velopack` e strumento `vpk` 1.2.161).
- Modelli: LFM3 di Liquid AI in formato GGUF, eseguiti in locale con llama.cpp (`llama-server` in modalità router su `127.0.0.1`).
- Prove: xUnit.

## Progetti

| Progetto | Ruolo |
|---|---|
| `src/Liquidino.Nucleo` | Tutta la logica, senza finestre. Si compila e si prova anche fuori da Windows. |
| `src/Liquidino` | App WPF: finestre, vassoio, microfono, lettore, mani su Windows, voce di Windows, firewall, prova di avvio. |
| `test/Liquidino.Prove` | Prove xUnit sul nucleo, più la prova con un llama-server vero che gira solo in CI. |

### Il nucleo

| File | Compito |
|---|---|
| `Catalogo.cs` | `CatalogoAzioni`: le sette azioni (rispondi, che ore sono, crea nota, ricorda, dimentica, mostra le creazioni, dormi), soglia 0,6. `LettoreDecisione` legge `/v1/systemone` di d1 e calibra le probabilità; `DecisoreLocale` sceglie dalle parole se d1 non c'è. |
| `Dialogo.cs` | Un turno: richiesta per le mani, conferme aperte, decisione, esecuzione. Contiene le `RegoleFisse` che si aggiungono sempre al prompt. |
| `ManiPc.cs` | `InterpretePc`: capisce con regole chiuse se una frase chiede un gesto sul PC e quale. `IManiPc` è il contratto; `GestoPc` dice se serve il sì. |
| `Regista.cs` | Accende `llama-server`, carica e scarica i modelli (mai più di due), salva e ripristina gli slot. `ProvaRiempimento` fa la prova di carico. |
| `ClienteLlama.cs` | Client HTTP verso `127.0.0.1`, senza proxy: health, router dei modelli, chat, `/v1/systemone`, `/tts`, slot. Legge `usage.prompt_tokens`. |
| `CatalogoModelli.cs` | I sei modelli della pagina Modelli (ruolo, repository `LiquidAI/...-GGUF`, a cosa serve). `ScaricatoreModelli` legge revisione ed elenco da Hugging Face, sceglie il quant, scarica in `.part` e installa solo se lo SHA-256 torna. |
| `Modelli.cs` | Trova `llama-server` e i GGUF, ruoli e `mmproj`, scrive il preset senza righe di rete, `PoliticaCarico`. |
| `Contesto.cs`, `ProvaContesto.cs` | Scelta del contesto da RAM e VRAM; prova di carico a gradini; `RegolaFirewall` (gli script PowerShell). |
| `Ciclo.cs` | `CicloAutonomo`: il pensiero orario, la fase di amicizia, la notte, il rifacimento guidato, i pensieri piaciuti come esempio. |
| `Creazioni.cs` | `FiltroMondo` (cartella e ricerca), `VotiMondo` (`mondo/voti.json`), `Diario` (conti dal registro). |
| `Mondo.cs` | Il mondo su disco: note, memoria, cestino, anteprima, titoli. `Estrattore` prende titolo e fatto dalla frase. |
| `Registro.cs`, `DettagliErrore.cs` | Registro JSONL (una riga per voce) e conteggio errori; il testo di «Copia dettagli errore». |
| `Voce.cs` | `SceltaVoce`: Omni, poi Windows, poi solo testo. |
| `Quiete.cs` | Quando scaricare i modelli e quando addormentarsi; la finestra di silenzio del microfono. |
| `MigrazioneDati.cs` | Porta i dati dalla cartella della 0.2.0 a `Liquidino-dati`, senza sovrascrivere. |
| `Orecchio.cs` | Stato della parola «Ehi Liquidino» (il modello non c'è ancora). |
| `ImpostazioniUtente.cs`, `Percorsi.cs`, `Stato.cs`, `Semi.cs`, `Testo.cs`, `NoteRilascio.cs`, `ComandiLocali.cs` | Impostazioni (e copia di quelle rovinate), percorsi, stati, file di serie, frasi, note di rilascio, STOP. |

### L'app WPF

| File | Compito |
|---|---|
| `App.xaml.cs` | Avvio: Velopack, migrazione, primo avvio, misura del PC, finestre, aggiornamenti. Con `--prova-avvio` passa a `ProvaAvvio`. |
| `Ponte.cs` | Tiene insieme finestre, regista, dialogo, ciclo e mani. Orologi del ciclo e della quiete, voce, prova di carico, diario di notte. |
| `FinestraAvatar.cs` | Avatar, tasti, fumetto con Sì e No, menu, Ctrl+Spazio, trascinamento dei file. |
| `FinestraCasa.cs` | Chat e pannello del mondo (filtri, icone, anteprima, voti, rifacimento, diario). |
| `FinestraImpostazioni.cs` | Il Mainframe: impostazioni e strumenti (prova del contesto, firewall, voce, dettagli errore). |
| `FinestraModelli.cs` | La pagina Modelli: stato di ogni modello, Scarica (con domanda e avanzamento), Elimina (nel Cestino), tasti delle cartelle. |
| `Mainframe.cs` | I pezzi grafici del Mainframe: sezioni, righe, interruttori OFF/ON, selettori ◁ ▷, tasti, testata. Disegnati da noi. |
| `ManiWindows.cs` | Le mani vere: programmi dal menu Start (shell `AppsFolder` e collegamenti `.lnk`), cartelle note, tasti del volume, foto dello schermo. |
| `VoceWindows.cs`, `Firewall.cs` | Voci italiane di SAPI; regola del firewall con UAC. |
| `ProvaAvvio.cs` | La prova di avvio della CI. |
| `Vassoio.cs`, `Aggiornamenti.cs`, `Microfono.cs`, `MisuraPc.cs`, `FoglioAvatar.cs`, `Stile.cs`, `FinestraSetup.cs`, `FinestraNovita.cs` | Icona nel vassoio, Velopack, microfono e lettore, RAM e VRAM, fotogrammi, colori e caratteri, primo avvio, novità. |

## Cartelle dati

Radice: `%LOCALAPPDATA%\Liquidino-dati` (`Percorsi.RadiceDati()`). Il programma sta in `%LOCALAPPDATA%\Liquidino` e non tiene dati.

| Percorso | Contenuto |
|---|---|
| `mondo/note`, `storie`, `idee`, `per-cammo` | Le creazioni. I pensieri orari vanno in `idee`. |
| `mondo/diario/AAAA-MM-GG.md` | Il diario della giornata. |
| `mondo/memoria/cosa-so-di-te.md` | Memoria su di te, modificabile a mano. |
| `mondo/cestino` | Quello che hai buttato o che è stato rifatto. |
| `mondo/registro/AAAA-MM-GG.jsonl`, `errori.json` | Registro delle azioni e conteggio degli errori. Senza le tue frasi. |
| `mondo/voti.json` | 👍 e 👎. |
| `impostazioni.json` | Impostazioni. Se si rovina diventa `impostazioni.json.rovinato-…`. |
| `prompt/sistema.md`, `catalogo/azioni.json` | Copie modificabili (poi riavvia). Quelle di serie stanno nel programma. |
| `motore/modelli.ini`, `motore/server.log`, `motore/cache-vuota` | Preset del router, log di llama-server, cache vuota per l'offline. |
| `slot/` | Slot della cache salvati prima di scaricare un modello. |
| `orecchio/` | Posto per `silero_vad.onnx` ed `ehi-liquidino.onnx`. |
| `diario-tecnico.log` | Le eccezioni del programma, per il supporto. |

I modelli stanno **fuori** dall'installer (si scaricano dalla pagina Modelli, `FinestraModelli`, solo col tuo clic), di norma in `C:\Users\cammo\LiquidinoProva` (cambiabile da Impostazioni). `Percorsi.StaDentro` controlla che un percorso resti dentro il mondo prima di leggerlo, spostarlo o aprirlo.

## Flusso di una frase

1. Scrivi, parli (Parla o `Ctrl+Spazio`, Omni trascrive) o trascini un'immagine.
2. Se c'è una domanda aperta (sì o no), si chiude quella.
3. `InterpretePc` guarda se è una richiesta per il PC. Se sì: serve il Permesso, poi il gesto (col sì per i programmi), se no «Questo fallo tu:».
4. Altrimenti d1 sceglie l'azione dal catalogo (o il decisore locale, se d1 non c'è). Sotto 0,6, per le azioni che scrivono, chiede conferma.
5. Il regista carica solo i modelli che servono, al massimo due insieme. Le mani (LFM3-1.7B) compilano i campi, Omni (o le mani) risponde.
6. L'azione resta nel mondo e scrive nel registro. La risposta va in chat e nel fumetto; la voce è Omni (`/tts`) o, se manca, Windows.
7. Dopo quattro minuti senza gesti i modelli si scaricano. Di notte dorme e scrive il diario.

## Modelli e ruoli

- **d1**: sceglie l'azione.
- **Omni**: chat, trascrizione, voce, immagini e schermo, se c'è. Altrimenti la chat la fa **LFM3-1.7B**.
- **LFM3-1.7B (mani)**: compila le note e i ricordi, scrive e rifà i pensieri. Il contesto lungo (fino a 128K) è il suo.
- **VL**: immagini e schermo, se Omni non c'è.
- **Audio**, **d1-Omni**: riconosciuti dal regista, non usati nel flusso quotidiano.
- Il ragionamento degli strumenti è spento. Cache KV q4 e flash-attention nel preset.

## Build e distribuzione

- `dotnet test Liquidino.sln -c Release`: prove del nucleo.
- `scripts/prova-motore.ps1`: solo in CI, scarica un `llama-server` CPU e un GGUF minuscolo e fa passare il regista per caricamento, risposta, prova di carico e scaricamento.
- `scripts/pacchetto.ps1`: `dotnet publish` self-contained `win-x64`, `vpk pack`, zip portatile, `SHA256SUMS.txt`.
- `scripts/prova-avvio.ps1`: avvia il programma pubblicato con `--prova-avvio` e legge `esito-avvio.txt`.
- `scripts/note-rilascio.ps1` e `scripts/pubblica-release.ps1`: note dal CHANGELOG e release su GitHub (prima il Setup, poi gli aggiornamenti e le impronte).
- CI: `.github/workflows/costruisci.yml` su `windows-latest`. Sul ramo `main` crea la release della versione in `Directory.Build.props`, se non c'è già.

## Provenienza dei pezzi

Avatar, fogli di sprite e font: vedi `risorse/PROVENIENZA.md` e [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). Gli oggetti dell'avatar (cassetta, VHS, floppy, cartuccia, bobina, MiniDisc, CD, vinile) vengono dal DaP Convertitore e non vanno rifatti.
