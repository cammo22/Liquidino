# Contribuire a Liquidino

Per chi lavora sul codice, persone o agenti. Ispirato a `CONTRIBUTING.md` di Capri, adattato alla struttura di Liquidino. Le regole di Capri sul C# 5 e su WinForms **non** valgono qui: Liquidino usa .NET 8 e C# 12.

## Prima di iniziare

- Correzione piccola: apri direttamente una pull request.
- Funzione nuova o cambiamento grosso: apri prima una issue, così non si costruisce qualcosa che poi non entra.
- Cambiare il carattere, l'avatar o la voce di Liquidino richiede l'ok del proprietario.
- Le immagini dell'avatar sono quelle approvate del DaP Convertitore: non si rifanno (vedi `risorse/PROVENIENZA.md`).

## Ambiente

- Windows 10 o 11 a 64 bit per provare l'app WPF.
- .NET SDK 8.0.100 o successivo della serie 8 (`global.json`).
- Il nucleo (`Liquidino.Nucleo`) e le prove girano anche su Linux. L'app (`net8.0-windows`) su Linux si **compila** con l'SDK di Microsoft (non quello delle distribuzioni, che non ha WPF) e `-p:EnableWindowsTargeting=true`, ma si **avvia** solo su Windows.

## Compilare e provare

```bash
dotnet test Liquidino.sln -c Release --nologo
```

Per l'installer e la prova di avvio, su Windows:

```powershell
./scripts/pacchetto.ps1
./scripts/prova-avvio.ps1
```

La CI (`.github/workflows/costruisci.yml`) esegue, a ogni push e a ogni pull request: le prove, la prova con un llama-server vero (`scripts/prova-motore.ps1`), l'installer, la prova di avvio del programma pubblicato e l'artefatto. Sul ramo `main` crea anche la release.

`Liquidino.exe --prova-avvio <cartella>` apre tutte le finestre su dati finti in quella cartella, prova i passi principali senza toccare il PC ed esce con 0 se va tutto bene. L'esito sta in `<cartella>\esito-avvio.txt`. Se aggiungi una finestra o un gesto, aggiungi un passo in `src/Liquidino/ProvaAvvio.cs`.

## Convenzioni di codice

- Segui lo stile dei file vicini: nomi in italiano, semplici, `record` sealed per i dati, classi `static` per le utilità pure.
- La logica va in `Liquidino.Nucleo`, senza interfaccia. In `Liquidino` (WPF) restano finestre e collegamenti. Così si prova senza aprire finestre.
- Ogni funzione nuova ha una prova in `test/Liquidino.Prove`. I file sono per argomento (`ProveNucleo.cs`, `ProveMani.cs`, `ProveCreazioni.cs`, `ProveSicurezza.cs`, `ProveVoce.cs`, `ProveContesto.cs`); `Finti.cs` ha il regista finto e gli aiuti. Lo stile: nome che racconta il comportamento, `Assert` chiari.
- Niente commenti che ripetono il codice. I commenti `///` spiegano il perché di una classe (vedi `Percorsi.cs`).
- Nullable attivo: niente `!` per zittire gli avvisi senza motivo.
- I testi che vede l'utente sono in italiano, dai del tu, frasi corte.

## Regole di prodotto (da non rompere)

1. **Niente rete oltre al controllo aggiornamenti.** Nuove chiamate di rete o tracciamenti si chiedono prima e si scrivono in [sicurezza-e-privacy.md](sicurezza-e-privacy.md).
2. **Nessun modello scaricato in automatico.** Se manca, lo dice.
3. **Mai dire di aver fatto una cosa che il programma non ha fatto.** Se non può: «Questo fallo tu:» e il perché.
4. **Azioni che inviano, pubblicano, comprano, cancellano o installano** passano da una conferma, salvo richiesta esplicita di quell'azione.
5. **Il mondo non esce dalla sua cartella.** Usa `Percorsi.StaDentro` prima di toccare un file scelto da un percorso esterno.
6. **Le mani fanno solo i gesti di `InterpretePc`**, col Permesso acceso; il bersaglio viene da un elenco letto dal PC, mai dal testo del modello. Vedi [sicurezza-e-privacy.md](sicurezza-e-privacy.md).
7. **Il registro e il diario non tengono le frasi dell'utente.**
8. **STOP risponde sempre**, anche premuto due volte, e non blocca la finestra.
9. **Al massimo due modelli accesi insieme.**
10. **Solo uso personale.** Non inserire modelli o pesi nel repository.

## Commit e pull request

- Il messaggio dice cosa fa Liquidino adesso, in parole semplici, in italiano. Esempio dal repository: «Lo STOP non blocca più la finestra e non parte nessun download.»
- Una pull request per un cambiamento. Descrizione breve: cosa cambia per l'utente, come l'hai provato.
- Se cambia il comportamento visibile, aggiorna `CHANGELOG.md`, `NOTE-RELEASE.md` se serve, e [aiuto.md](aiuto.md).
- Se tocchi la rete, i permessi o i dati su disco, aggiorna [sicurezza-e-privacy.md](sicurezza-e-privacy.md) e [architettura.md](architettura.md).

## Rilasciare una versione

1. Alza `<Version>` in `Directory.Build.props`.
2. Aggiungi la voce in `CHANGELOG.md` (sezioni Novità, Correzioni, Da provare).
3. Aggiorna `NOTE-RELEASE.md`.
4. Unisci su `main`: la CI costruisce `Liquidino-Setup.exe` e pubblica la release, con `SHA256SUMS.txt`. Gli script `scripts/note-rilascio.ps1` e `scripts/pubblica-release.ps1` fanno le note e la pubblicazione. Le copie installate si aggiornano da sole.
5. Aggiorna le due colonne di [ROADMAP.md](../ROADMAP.md): «Nel programma» quando il codice c'è, «Verificato» quando i punti «Da provare» passano sul PC.

## Per gli agenti

Leggi `AGENTS.md` nella radice. Lavora sul ramo che ti è stato assegnato, non toccare `main`, non fare push su altri rami.
