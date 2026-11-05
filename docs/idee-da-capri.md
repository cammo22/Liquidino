# Idee da Capri

Confronto tra ciò che fa [Capri](progetto-di-riferimento-capri.md) e lo stato di Liquidino. La colonna **Stato** dice cosa c'è oggi. «Idea» non significa «deciso»: serve a ordinare il lavoro, non a promettere nulla. Ricorda: niente codice, grafica o testi copiati da Capri; si prende l'idea e la si rifà a modo nostro.

Legenda: ✅ c'è già · 🟡 c'è in parte o è in roadmap · 💡 idea nuova · ⛔ non vogliamo.

## Cervello e modelli

| Capri | Stato in Liquidino |
|---|---|
| Tre livelli di intelligenza (locale, chiavi gratuite, chiavi a pagamento) | ⛔ Liquidino è solo locale, per scelta. Il livello si adatta alla macchina (RAM, VRAM, contesto). |
| Cervello automatico in base al PC | ✅ `SceltaContesto` misura RAM e VRAM. 🟡 Manca la prova di carico vera (roadmap). |
| Fallback automatico se un servizio finisce i crediti | 💡 Equivalente locale: se Omni non c'è, usa LFM3-1.7B (✅); se non c'è nessun modello, messaggio chiaro (✅). |
| Menu con elenco modelli e pulsante Refresh | 🟡 «Copia dettagli errore» elenca llama-server e i GGUF trovati; **Salva** li rilegge. Un elenco nella finestra non c'è ancora. |

## Voce e ascolto

| Capri | Stato |
|---|---|
| Voce locale gratuita (Kokoro) | ✅ Piano B: le voci italiane di Windows (SAPI), già sul PC. Kokoro resta un'idea. |
| Cursori di tono e velocità, effetti | 💡 |
| Parola di attivazione («Hey Capri») | ✅ «babbasone»: Parakeet trascrive e la parola si tara sulle tue registrazioni (lo spotter di Capri è solo inglese). |
| Riconoscimento vocale locale | ✅ Parakeet v3 int8 con sherpa-onnx, come Capri; rilevatore di voce sull'energia, scritto da noi. |
| Ears: audio del PC, call mode | ✅ 👂 sente il PC tranne la sua voce; 📞 sente Discord a parte; la sua voce nel microfono con VB-CABLE. «Listen to this» non c'è: non serviva. |
| Avviso sul microfono Bluetooth che peggiora l'audio | 💡 Nota nell'aiuto o avviso nel setup. |

## Memoria e personalità

| Capri | Stato |
|---|---|
| `me.md`, `memories.md`, trascrizioni | ✅ `cosa-so-di-te.md` e registro. 💡 Riassunto delle chat vecchie. |
| `personality.md` modificabile, ma regole di sicurezza fisse | ✅ `sistema.md` modificabile, e le regole fisse stanno nel codice (`Dialogo.RegoleFisse`). |
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
| Aprire app, musica, finestre, scorciatoie, mouse | 🟡 Col Permesso: programmi (dopo il sì), cartelle, volume, pausa, schermo. Mouse e tastiera no. |
| Livelli Lockdown, Guarded, Trusted, Blackwall e interruttori | 🟡 Un livello solo, prudente: Permesso acceso o spento, e il sì per ogni programma. Interruttori per singolo gesto in roadmap. |
| `Esc` ferma ogni compito | ✅ STOP ovunque; `Esc` in chat ferma il turno. |
| Non esegue istruzioni lette sullo schermo | ✅ Regola fissa nel codice, e nella domanda per lo schermo e per le immagini. |
| Pianificazione dei compiti prima di eseguirli | 💡 Dopo le mani. |
| Compiti lunghi in background | 💡 |

## Giochi e schermo

| Capri | Stato |
|---|---|
| Guarda lo schermo a intervalli | 🟡 Lo schermo si guarda quando lo chiedi, col Permesso. Una vista periodica non c'è. |
| Commenti nei giochi, clip, Gaming Mode | ⛔ Fuori scopo per ora. |

## Installazione, aggiornamenti, qualità

| Capri | Stato |
|---|---|
| Installer unico senza amministratore | ✅ Velopack. |
| Avvio con Windows | 💡 Da decidere. |
| Aggiornamenti con controllo di firma | ✅ Via Velopack (**da verificare** i dettagli). |
| `SHA256SUMS.txt` in ogni release | ✅ Lo scrive `scripts/pacchetto.ps1`. |
| Firma del codice Windows | 🟡 Non c'è. Evita gli avvisi di SmartScreen. |
| Smoke test con righe `PASS` e CI su tre sistemi | ✅ `--prova-avvio` in CI, col programma pubblicato, scrive righe `PASS`. Solo Windows, che è l'unico sistema di Liquidino. |
| Aiuto → «Copia dettagli errore» | ✅ In Impostazioni. |
| «Capri started fresh»: ripristino dopo due chiusure anomale | ✅ Se `impostazioni.json` non si legge, diventa `.rovinato-…` e Liquidino lo dice. |
| Pagina di aiuto con ricerca / chat di aiuto | 🟡 [aiuto.md](aiuto.md). ⛔ Niente chat di aiuto in rete. |

## Documenti di progetto

| Capri | Stato |
|---|---|
| `CONTRIBUTING.md`, `SECURITY.md`, `THIRD-PARTY-NOTICES.md` | ✅ [contribuire.md](contribuire.md), [SECURITY.md](../SECURITY.md), [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). |
| Tabella dei server contattati | ✅ in [sicurezza-e-privacy.md](sicurezza-e-privacy.md). |
| Roadmap con «fatto» e «previsto» | ✅ `ROADMAP.md`. |
| Attribuzione dei componenti di terzi con licenza | ✅ `risorse/PROVENIENZA.md` e l'elenco unico [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md), che viaggia anche col programma. |

## Prime cose da fare, in ordine

Le prime cinque della lista di prima sono fatte nella 0.3.0: la regola sulle istruzioni lette, `SHA256SUMS.txt`, «Copia dettagli errore», `SECURITY.md` e `THIRD-PARTY-NOTICES.md`, la copia delle impostazioni rovinate. Restano:

1. Interruttori per singolo gesto delle mani (programmi, cartelle, volume, schermo), prima di aggiungere mouse e tastiera.
2. Un elenco dei GGUF trovati, visibile in Impostazioni.
3. Un tasto per far tacere i pensieri orari senza aprire le Impostazioni.
4. Firma del codice dell'installer.
