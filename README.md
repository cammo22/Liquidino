# Liquidino

Compagno da desktop per Windows. Sta sul PC, parla italiano, non va in internet per pensare. I modelli sono i LFM3 di Liquid AI, eseguiti in locale con llama.cpp. Solo uso personale.

## Installazione

1. Scarica `Liquidino-Setup.exe` dalla [release](https://github.com/cammo22/Liquidino/releases/latest). È il primo file.
2. Chiudi Liquidino se è ancora aperto.
3. Doppio clic sul Setup. Non chiede i diritti di amministratore.
4. Apri Liquidino dall'icona sul desktop o dal menu Start.
5. Al primo avvio: il tuo nome, il carattere e la voce. I tasti sotto l'avatar sono un clic. I modelli restano in `C:\Users\cammo\LiquidinoProva` (Impostazioni, se la cartella è un'altra): non sono dentro l'installer.

Se manca un modello, Liquidino lo dice. Per scaricarlo c'è la pagina **Modelli** (Impostazioni → Modelli): a cosa serve ogni modello, quanto pesa, Scarica, Elimina e i tasti per aprire le cartelle. Scarica solo dopo il tuo sì, da Hugging Face, e controlla l'impronta del file. Da solo guarda in rete solo se c'è un aggiornamento: lo scrive nella barra in alto della chat. Per controllare il Setup scaricato c'è `SHA256SUMS.txt` nella stessa release.

## Cosa fa

- Avatar trasparente, sempre davanti, lo sposti col mouse. Stati: **dorme**, **ascolta**, **pensa**, **parla**. La rotella lo ingrandisce. Ogni tanto cambia oggetto, fra quelli già approvati del Convertitore. I tasti compaiono quando ci passi sopra col mouse.
- Un clic apre chat e mondo. Il mondo è la cartella `%LOCALAPPDATA%\Liquidino-dati\mondo`: note, storie, idee, diario, memoria, cestino, registro. Non sta nella cartella del programma, così un aggiornamento non la cancella.
- Il pannello del mondo si sfoglia: cartella, ricerca (anche dentro i file), icone grandi, anteprima, 👍 e 👎. Con 👎 un pensiero si rifà, dicendogli in chat cosa cambiare.
- d1 sceglie l'azione: rispondi, che ore sono, crea nota, ricorda, dimentica, mostra le creazioni, dormi. Per chiacchierare risponde subito. Chiede conferma, coi pulsanti sul fumetto, solo se deve scrivere, ricordare o dimenticare e non è sicuro.
- **Le mani sul PC**, solo col tasto **Permesso** acceso: apre un programma del menu Start dopo il tuo «sì», apre Documenti, Download, Immagini, Musica, Video e Desktop, alza e abbassa il volume, mette in pausa la musica, guarda lo schermo. Non clicca, non scrive al posto tuo, non chiude e non installa niente. **STOP** ferma tutto e scarica i modelli.
- **Le Orecchie** (👂): lo chiami «babbasone» e ti risponde, senza cliccare. Parakeet v3 trascrive sulla CPU, anche in italiano, e la parola si tara sulle tue registrazioni. Sente anche l'audio del PC (tranne la sua voce) e lo tiene in mente qualche minuto, senza salvarlo. Quello che sente dal PC non sono ordini.
- **Discord**: con le Orecchie accese lo sente a parte, da solo; chi lo chiama per nome riceve una risposta a voce, ma le mani le comandi solo tu. Con VB-CABLE la sua voce va anche nel tuo microfono, così gli altri lo sentono.
- La chat usa Omni, se c'è, altrimenti LFM3-1.7B. La voce è il testo letto da `/tts` in italiano; se Omni non c'è, le voci italiane di Windows. Il ragionamento degli strumenti è spento.
- Il regista tiene accesi al massimo due modelli e li scarica quando non servono. Le **luci** sotto l'avatar li mostrano: un clic accende, spegne o scarica. Con **⏸ Pausa** niente si carica da solo e l'avatar diventa in bianco e nero.
- **Dormire** lo decidi tu (🌙): sceglie lui quanto, al massimo due ore, e intanto sogna spesso. Alla sveglia scrive il diario; le dormite complete si contano. Su sogni, pensieri e chat ci sono 👍 e 👎: non risponde, li tiene per allenarsi.
- Ogni tanto (da 10 secondi a 2 ore, lo scegli tu) lascia un pensiero in `mondo/idee`. Nelle prime due settimane è un saluto. «Un pensiero adesso» non ti fa aspettare l'ora.
- Memoria in `mondo/memoria/cosa-so-di-te.md`. Si cancella a mano o con «dimentica». Il registro non conserva le tue frasi.
- Prompt e catalogo, da leggere e modificare (poi riavvia): `%LOCALAPPDATA%\Liquidino-dati\prompt\sistema.md` e `catalogo\azioni.json`. Le regole di sicurezza stanno nel programma e valgono anche se li cambi.
- Impostazioni a schede, con la barra DaProd: Tu, Voce, Orecchie, Pensieri e sonno, Avatar, Modelli (tabella in una schermata), Motore, Sicurezza, Strumenti; cartella del motore, carattere, voce, «Prova il contesto sul serio», «Togli internet a llama-server», «Prova la voce», «Copia dettagli errore».

Trascina un'immagine sull'avatar: se c'è Omni (o VL) la guarda. Un altro tipo di file: te lo dice e non lo tocca.

## Cosa non fa ancora

Non usa il mouse né la tastiera. Non si evolve da solo. L'elenco, con quanto manca, è in [ROADMAP.md](ROADMAP.md).

## Da dove viene l'avatar

Dalle immagini già approvate del [DaP Convertitore](https://github.com/cammo22/DaP-Convertitore). Non sono state rifatte. Dettaglio in `PROVENIENZA.md`.

## Documentazione

Guida, architettura, sicurezza e convenzioni sono in [docs/](docs/README.md). Chi lavora sul codice parte da [AGENTS.md](AGENTS.md).

## Licenza

Codice MIT (vedi [LICENSE](LICENSE)). Pesi dei modelli: LFM Open License v1.0, non sono nell'installer, uso personale. Componenti di terzi in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Problemi di sicurezza: [SECURITY.md](SECURITY.md).
