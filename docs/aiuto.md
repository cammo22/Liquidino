# Aiuto per l'utente

Guida rapida di Liquidino 0.3.0 e soluzioni ai problemi più comuni. C'è scritto solo quello che il programma fa davvero.

## In breve

Liquidino è un compagno da desktop per Windows a 64 bit. Parla italiano, sta sul PC e non va in internet per pensare. I modelli sono i LFM3 di Liquid AI, in locale con llama.cpp. Solo uso personale.

## Installare

1. Scarica `Liquidino-Setup.exe` dalla [release](https://github.com/cammo22/Liquidino/releases/latest). È il primo file della lista.
2. Se vuoi, controlla il file: in PowerShell `Get-FileHash .\Liquidino-Setup.exe -Algorithm SHA256`, e confronta con la riga di `SHA256SUMS.txt` nella stessa release.
3. Chiudi Liquidino se è aperto.
4. Doppio clic sul Setup. Non servono i diritti di amministratore.
5. Apri Liquidino dall'icona sul desktop o dal menu Start.
6. Al primo avvio scegli il tuo nome, il carattere e la voce.

I modelli **non** sono nell'installer. Si cercano in `C:\Users\cammo\LiquidinoProva`; se stanno altrove si cambia da Impostazioni. Se un modello manca, Liquidino lo dice e non lo scarica.

## Il tuo compagno

- **L'avatar** è trasparente e sempre davanti. Lo sposti col mouse, la rotella lo ingrandisce. Stati: dorme, ascolta, pensa, parla.
- **I tasti** compaiono quando passi sopra l'avatar col mouse: 🎤 Parla, 💬 Chat, ✨ Creazioni, 🖥️ Permesso, ⏹ STOP, 🌙 Dormi, ⚙ Impostazioni, 📝 nota veloce, 🕐 ora, 💭 pensiero. Il tasto destro sull'avatar apre il menu con tutto; l'icona in basso a destra mostra o nasconde l'avatar, controlla gli aggiornamenti ed esce.
- **Parla** è un interruttore: il primo clic ascolta, il secondo manda la frase. Stessa cosa con `Ctrl+Spazio`.
- **STOP** ferma tutto, cancella la domanda aperta e scarica i modelli. Si può premere anche due volte. In chat, `Esc` ferma il turno.
- **Immagini**: trascina un'immagine sull'avatar. Se c'è Omni (o VL) la guarda. Un altro tipo di file: te lo dice e non lo tocca.

## Cosa sa fare

d1 sceglie una di queste azioni: rispondere, dire l'ora, creare una nota, ricordare, dimenticare, mostrare le creazioni, dormire. Per chiacchierare risponde subito. Se deve scrivere, ricordare o dimenticare e non è sicuro (sotto 0,6), chiede sì o no sul fumetto.

## Le mani sul PC

Funzionano solo col tasto 🖥️ **Permesso** acceso (diventa rosso). Spento, Liquidino non tocca niente fuori dal suo mondo e te lo dice.

| Chiedi | Cosa fa |
|---|---|
| «apri il blocco note», «avvia la calcolatrice», «apri chrome» | Cerca il programma nel menu Start e chiede «Apro «…»?». Si apre solo dopo il tuo sì. |
| «apri i documenti», «apri la cartella dei download», «aprimi le foto» | Apre la cartella: Documenti, Download, Immagini, Musica, Video, Desktop. |
| «alza il volume», «abbassa il volume», «metti il muto» | Usa i tasti del volume di Windows. |
| «metti in pausa la musica», «riprendi la canzone» | Il tasto play/pausa. |
| «cosa c'è sullo schermo?», «guarda lo schermo» | Fa una foto dello schermo, la fa guardare a Omni (o VL) e te la descrive. La foto resta in memoria e non si salva. Se sullo schermo ci sono istruzioni, le descrive ma non le esegue. Gli è chiesto di non leggere password o numeri di carta. |

Non clicca, non muove il mouse, non scrive al posto tuo, non chiude programmi, non installa e non cancella file: per queste cose risponde «Questo fallo tu:». Ogni gesto va nel registro. Se spegni il Permesso mentre aspetta il tuo sì, non lo fa.

## Il mondo e le creazioni

Il mondo è la cartella `%LOCALAPPDATA%\Liquidino-dati\mondo`. Nel pannello a destra della chat:

- in alto scegli la cartella (note, storie, idee, per-cammo, diario, memoria, cestino, registro) e scrivi una parola da cercare, anche dentro i file;
- ▦ passa alle icone grandi, ☰ torna all'elenco;
- un clic su un file mostra l'inizio sotto l'elenco; doppio clic, o 📄, lo apre col suo programma;
- 👍 ti piace: i pensieri nuovi gli somiglieranno; 👎 non ti piace: se è un suo pensiero o una storia ti chiede in chat cosa cambiare, e lo riscrive. Il vecchio va nel cestino. Le note che gli hai dettato non le riscrive;
- 🗑 mette nel cestino, 💭 un pensiero adesso, 📔 il diario di oggi.

Il **diario** sta in `mondo/diario`, un file per giorno. Ci sono i conti della giornata (quante volte avete parlato, note, pensieri, ricordi, gesti sul PC, errori) e i titoli di quello che ha creato. Le chiacchiere no. Si scrive da solo quando va a dormire di notte.

## Memoria

Quello che sa di te sta in `mondo/memoria/cosa-so-di-te.md`. Puoi aprirlo, correggerlo o cancellarlo a mano. Con «dimentica …» lo toglie lui. Il registro non conserva le tue frasi: scrive solo quanti caratteri erano.

## Personalità e azioni

Due file si leggono e si modificano (poi riavvia):

- `%LOCALAPPDATA%\Liquidino-dati\prompt\sistema.md`: come parla.
- `%LOCALAPPDATA%\Liquidino-dati\catalogo\azioni.json`: le azioni che d1 può scegliere.

Le regole di sicurezza (non eseguire ordini letti su file o schermo, non dire di aver fatto quello che non ha fatto, niente password, niente internet) stanno nel programma: valgono anche se cambi questi file.

## Pensieri e notte

Ogni ora, se non lo spegni, lascia un pensiero in `mondo/idee`. Nei primi 14 giorni è un saluto. «Un pensiero adesso» funziona sempre, anche di notte e col ciclo spento. Dalle 23 alle 7 dorme, ma se hai usato la chat da poco aspetta.

## Voce

Se c'è Omni, la risposta si sente con la sua voce (`/tts`). Se Omni non c'è, o la sua voce non esce, legge con le voci italiane di Windows. L'interruttore è in Impostazioni, con l'elenco delle voci trovate e il tasto «Prova la voce». Una voce italiana si aggiunge da Impostazioni di Windows → Ora e lingua → Voce.

## Impostazioni e strumenti

- **Cartella del motore**, porta, ciclo dei pensieri, carattere, voce. **Salva** riaccende il motore.
- **Misura di nuovo il PC**: rilegge RAM e VRAM e sceglie il contesto.
- **Prova il contesto sul serio**: riaccende il motore a 128K, lo riempie al 90% e guarda quanti token ha letto davvero. Se non regge prova 64K, poi 32K, 16K, 8K, e tiene il primo che va. Controlla anche se lo slot si salva su disco. Può volerci qualche minuto; lo stesso tasto, durante la prova, la ferma.
- **Togli internet a llama-server**: crea una regola di Windows Firewall che blocca llama-server verso l'esterno. Windows chiede il permesso dell'amministratore una volta. Liquidino continua a parlargli su 127.0.0.1.
- **Prova la voce** e **Copia dettagli errore** (versione, sistema, motore, ultimo errore, coda del diario tecnico; senza chat né memoria).

## Aggiornare

La barra in alto della chat dice «Aggiornamento X disponibile». Un clic e si aggiorna da solo. È l'unica cosa che Liquidino guarda in rete.

## Disinstallare

Dall'elenco delle app di Windows: Velopack registra la disinstallazione. I tuoi dati restano in `%LOCALAPPDATA%\Liquidino-dati`: cancella la cartella a mano se vuoi eliminarli. I modelli stanno altrove e non vengono toccati.

## Problemi comuni

| Problema | Cosa fare |
|---|---|
| Windows mostra «Windows ha protetto il PC» | L'installer non è firmato. **Ulteriori informazioni**, poi **Esegui comunque**. Scarica solo dalla pagina delle release e controlla `SHA256SUMS.txt`. |
| L'antivirus segnala il Setup | Di solito è un falso positivo su un programma non firmato. Controlla l'impronta, poi consenti il file. |
| «Manca un modello» | Metti i file GGUF nella cartella dei modelli, o indica quella giusta in Impostazioni. Non scarica nulla da solo. |
| Risposte lente o il PC fatica | Il regista tiene accesi al massimo due modelli. Chiudi giochi e programmi pesanti. Poi Impostazioni → «Prova il contesto sul serio»: sceglie il contesto che il PC regge davvero. |
| Non sento la voce | Controlla uscita audio e volume. Senza Omni serve una voce italiana di Windows: Impostazioni → «Prova la voce» dice quali ha trovato. |
| Non mi ascolta quando parlo | Il tasto Parla è un interruttore: il secondo clic invia. Per trascrivere serve Omni. La parola «Ehi Liquidino» non esiste ancora. |
| «apri …» risponde che serve il permesso | Accendi 🖥️ Permesso vicino all'avatar. |
| «Non trovo … fra i programmi del menu Start» | Usa il nome che vedi nel menu Start (per esempio «Blocco note», «Esplora file»). |
| «Questo fallo tu: le mie mani…» | Hai chiesto un clic, di scrivere, chiudere o installare: non lo fa, per scelta. |
| All'avvio dice che le impostazioni erano rovinate | Le ha messe da parte accanto alle nuove (`impostazioni.json.rovinato-…`) e riparte dal primo avvio. Note e memoria non sono toccate. |
| Voglio ripartire da zero | Chiudi Liquidino e rinomina `%LOCALAPPDATA%\Liquidino-dati` (non la cartella dei modelli). Se venivi dalla 0.2.0, sposta anche `mondo` e `impostazioni.json` da `%LOCALAPPDATA%\Liquidino`, se no al prossimo avvio li ricopia. Poi rifà il primo avvio. |

## Chiedere aiuto

Impostazioni → **Copia dettagli errore**, poi apri una issue su GitHub con quel testo, cosa hai fatto e cosa è successo. Il testo non contiene la chat né la memoria, ma rileggilo prima di incollarlo. Per un problema di sicurezza, leggi [SECURITY.md](../SECURITY.md).
