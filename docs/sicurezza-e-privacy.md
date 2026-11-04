# Sicurezza e privacy

Principio di Liquidino: sta sul PC, pensa in locale e non va in rete per ragionare. Questa pagina elenca cosa è vero oggi e quali regole valgono per chi modifica il codice. Per confronto, a destra c'è come lo fa Capri (vedi [progetto-di-riferimento-capri.md](progetto-di-riferimento-capri.md)).

## Cosa resta sul PC

| Dato | Dove | Capri, per confronto |
|---|---|---|
| Note, idee, memoria, cestino, registro | `%LOCALAPPDATA%\Liquidino\mondo` | `%LOCALAPPDATA%\Capri` |
| Impostazioni | `impostazioni.json` | `settings.json` |
| Prompt e catalogo azioni | `prompt/sistema.md`, `catalogo/azioni.json` | `personality.md` |
| Modelli | Cartella dei modelli scelta dall'utente | Cervello locale su disco |

Nessuna telemetria, nessun analytics, nessuna segnalazione di crash. Se qualcuno la vuole aggiungere deve chiederlo prima.

## Cosa va in rete

Oggi **una sola cosa**: il controllo degli aggiornamenti (Velopack). Il risultato compare nella barra in alto della chat («Aggiornamento X disponibile»). I modelli non si scaricano: se manca un file, Liquidino lo dice e basta.

Il server dei modelli (`llama-server`) parte solo su `127.0.0.1`. Una regola di firewall che lo blocchi verso l'esterno è in [ROADMAP.md](../ROADMAP.md).

Ogni nuova chiamata di rete va documentata qui, in una riga: cosa, verso dove, quando, che dati escono. Esempio nel formato che usa Capri:

| Cosa | Destinazione | Quando |
|---|---|---|
| Controllo aggiornamenti | Server delle release (GitHub, via Velopack) | Quando l'app si avvia o su richiesta (**da verificare** la frequenza) |

## Permessi

- Il tasto **Permesso** oggi viene solo segnato. Liquidino non ha ancora le mani sul PC: se glielo chiedi, dice «Questo fallo tu:» e spiega perché.
- **STOP** scarica i modelli subito e non blocca la finestra.
- Soglia 0,6 sulle azioni: sotto, chiede conferma.
- Il prompt di sistema vieta di dire di aver fatto ciò che il programma non ha fatto.

### Regole da tenere quando arriveranno le «mani»

Ispirate al modello a livelli di Capri (`Lockdown`, `Guarded`, `Trusted`, `Blackwall`). Sono proposte, non ancora implementate:

1. Azioni che inviano, pubblicano, comprano, cancellano o installano passano sempre da una richiesta di conferma, salvo che l'utente abbia chiesto proprio quell'azione.
2. Liquidino non esegue istruzioni che legge sullo schermo, in un file o in una pagina: valgono solo quelle di chi gli parla.
3. Password e numeri di carta non vengono mai digitati.
4. Un tasto di emergenza (oggi STOP, in futuro anche `Esc`) ferma qualsiasi compito in corso.
5. Interruttori per singola azione, con un preset prudente come predefinito.
6. Le regole di sicurezza stanno nel codice, non nei file che l'utente modifica (`sistema.md`, `azioni.json`).
7. Ogni azione scrive nel registro.

## Avvisi di Windows e antivirus

**Da verificare** per Liquidino: l'installer è costruito con Velopack e, se non è firmato, Windows SmartScreen può mostrare «Windows ha protetto il PC». In quel caso: **Ulteriori informazioni**, poi **Esegui comunque**. La firma del codice è un punto aperto (Capri ha lo stesso problema). Scaricare solo dalla [pagina delle release](https://github.com/cammo22/Liquidino/releases/latest).

Idea ripresa da Capri: pubblicare con ogni release un `SHA256SUMS.txt`, così si può controllare il file con

```powershell
Get-FileHash -LiteralPath .\Liquidino-Setup.exe -Algorithm SHA256
```

## Segnalare un problema di sicurezza

Non aprire una issue pubblica con i dettagli di una vulnerabilità. Scrivi al proprietario del repository (cammo22) in privato. Un `SECURITY.md` nella radice con questo testo è ancora da scrivere.

## Licenze di terzi

Codice MIT. Pesi dei modelli: LFM Open License v1.0, uso personale, non nello zip. Font: SIL OFL 1.1 (testi in `risorse/font`). Voce, riconoscimento vocale e parola di attivazione: vedi `ROADMAP.md` per le licenze dei componenti previsti (Silero VAD MIT, openWakeWord codice Apache-2.0).
