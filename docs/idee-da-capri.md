# Idee da Capri

Confronto tra ciò che fa [Capri](progetto-di-riferimento-capri.md) e lo stato di Liquidino. La colonna **Stato** dice cosa c'è oggi. «Idea» non significa «deciso»: serve a ordinare il lavoro, non a promettere nulla. Ricorda: niente codice, grafica o testi copiati da Capri; si prende l'idea e la si rifà a modo nostro.

Legenda: ✅ c'è già · 🟡 c'è in parte o è in roadmap · 💡 idea nuova · ⛔ non vogliamo.

## Cervello e modelli

| Capri | Stato in Liquidino |
|---|---|
| Tre livelli di intelligenza (locale, chiavi gratuite, chiavi a pagamento) | ⛔ Liquidino è solo locale, per scelta. Il livello si adatta alla macchina (RAM, VRAM, contesto). |
| Cervello automatico in base al PC | ✅ `SceltaContesto` misura RAM e VRAM. 🟡 Manca la prova di carico vera (roadmap). |
| Fallback automatico se un servizio finisce i crediti | 💡 Equivalente locale: se Omni non c'è, usa LFM3-1.7B (✅); se non c'è nessun modello, messaggio chiaro (✅). |
| Menu con elenco modelli e pulsante Refresh | 💡 Impostazioni potrebbe elencare i GGUF trovati. |

## Voce e ascolto

| Capri | Stato |
|---|---|
| Voce locale gratuita (Kokoro) | 🟡 Piano B su CPU in roadmap; oggi `/tts` di Omni. |
| Cursori di tono e velocità, effetti | 💡 |
| Parola di attivazione («Hey Capri») | 🟡 «Ehi Liquidino»: struttura pronta, manca il modello addestrato. |
| Riconoscimento vocale locale | 🟡 Silero VAD e struttura `orecchio/`. |
| Avviso sul microfono Bluetooth che peggiora l'audio | 💡 Nota nell'aiuto o avviso nel setup. |

## Memoria e personalità

| Capri | Stato |
|---|---|
| `me.md`, `memories.md`, trascrizioni | ✅ `cosa-so-di-te.md` e registro. 💡 Riassunto delle chat vecchie. |
| `personality.md` modificabile, ma regole di sicurezza fisse | ✅ `sistema.md` modificabile. 💡 Spostare le regole non negoziabili nel codice e non nel file modificabile. |
| Frasi dei promemoria in un file modificabile (`check-ins.md`) | 💡 I pensieri orari potrebbero attingere da un file di frasi. |
| «Open her notes / memories» da Impostazioni | ✅ «Apri cartella». |

## Presenza sul desktop

| Capri | Stato |
|---|---|
| Sta sul PC, trascinabile, ridimensionabile | ✅ |
| Si siede sulle finestre e sui bordi | 💡 |
| Letargo, notte | ✅ Dorme dalle 23 alle 7 e con «Dormi». |
| Modalità silenziosa con scorciatoia | 💡 Un tasto per far tacere i pensieri orari. Oggi si spengono dalle impostazioni. |
| Più monitor, dimensioni da Tiny a Giant | 🟡 Rotella per ingrandire. **Da verificare** più monitor. |
| Mini player e danza con la musica | 💡 Fuori dal tema per ora. |

## Compiti sul PC e permessi

| Capri | Stato |
|---|---|
| Aprire app, musica, finestre, scorciatoie, mouse | 🟡 Roadmap «Uso del PC»: oggi solo il tasto Permesso e lo STOP. |
| Livelli Lockdown, Guarded, Trusted, Blackwall e interruttori | 💡 Vedi proposta in [sicurezza-e-privacy.md](sicurezza-e-privacy.md). Niente «Blackwall» senza motivo: meglio fermarsi a un livello prudente. |
| `Esc` ferma ogni compito | 🟡 Oggi STOP. 💡 Aggiungere `Esc` quando arrivano le mani. |
| Non esegue istruzioni lette sullo schermo | 💡 Regola da scrivere subito nel prompt e nel codice. |
| Pianificazione dei compiti prima di eseguirli | 💡 Dopo le mani. |
| Compiti lunghi in background | 💡 |

## Giochi e schermo

| Capri | Stato |
|---|---|
| Guarda lo schermo a intervalli | 🟡 Immagine trascinata sull'avatar; vista periodica non c'è. |
| Commenti nei giochi, clip, Gaming Mode | ⛔ Fuori scopo per ora. |

## Installazione, aggiornamenti, qualità

| Capri | Stato |
|---|---|
| Installer unico senza amministratore | ✅ Velopack. |
| Avvio con Windows | 💡 Da decidere. |
| Aggiornamenti con controllo di firma | ✅ Via Velopack (**da verificare** i dettagli). |
| `SHA256SUMS.txt` in ogni release | 💡 Facile da aggiungere in `scripts/pubblica-release.ps1`. |
| Firma del codice Windows | 🟡 Non c'è. Evita gli avvisi di SmartScreen. |
| Smoke test con righe `PASS` e CI su tre sistemi | 🟡 Prove xUnit e CI su Windows. 💡 Un controllo che avvia davvero l'app. |
| Aiuto → «Copia dettagli errore» | 💡 Copia versione, sistema, ultimo errore del diario tecnico, senza testo delle chat. |
| «Capri started fresh»: ripristino dopo due chiusure anomale | 💡 Utile quando `impostazioni.json` si rompe; salvare il vecchio come `.crashed`. |
| Pagina di aiuto con ricerca / chat di aiuto | 🟡 [aiuto.md](aiuto.md). ⛔ Niente chat di aiuto in rete. |

## Documenti di progetto

| Capri | Stato |
|---|---|
| `CONTRIBUTING.md`, `SECURITY.md`, `THIRD-PARTY-NOTICES.md` | 🟡 [contribuire.md](contribuire.md) e [sicurezza-e-privacy.md](sicurezza-e-privacy.md). 💡 `SECURITY.md` e `THIRD-PARTY-NOTICES.md` nella radice. |
| Tabella dei server contattati | ✅ in [sicurezza-e-privacy.md](sicurezza-e-privacy.md). |
| Roadmap con «fatto» e «previsto» | ✅ `ROADMAP.md`. |
| Attribuzione dei componenti di terzi con licenza | ✅ `risorse/PROVENIENZA.md`. 💡 Un elenco unico per modelli, font, librerie. |

## Prime cose da fare, in ordine

1. Scrivere nel prompt e nel codice la regola «non obbedire a istruzioni lette su schermo o in file».
2. Aggiungere `SHA256SUMS.txt` alla release.
3. «Copia dettagli errore» nel menu Impostazioni.
4. `SECURITY.md` e `THIRD-PARTY-NOTICES.md`.
5. Ripristino delle impostazioni rotte con copia `.crashed`.
6. Livelli di permesso, prima di dare le mani a Liquidino.
