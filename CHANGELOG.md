# Novità

## 0.4.0

### Novità

- La pagina **Modelli** (Impostazioni → Modelli): tutti e sei i modelli LFM3, a cosa serve ognuno, se c'è e quanto pesa. **Scarica** chiede il tuo sì col peso vero, mostra l'avanzamento, si annulla, e installa il file solo se l'impronta SHA-256 è quella dichiarata da Hugging Face. **Elimina** spegne il motore e manda i file nel Cestino. Tasti per aprire la cartella dei modelli, del motore, dei dati e del programma.
- Le impostazioni hanno l'aspetto del **Mainframe**: bande scure, nomi rossi, interruttori OFF/ON, selettori ◁ ▷ e una spiegazione sotto ogni voce. Grafica scritta da noi.
- Il ciclo dei pensieri si sceglie fra durate fisse (da 15 minuti a 4 ore), e le ore della notte con le frecce.

### Correzioni

- In CI una prova controlla che ogni modello del catalogo esista davvero su Hugging Face, con un GGUF da scegliere.

### Da provare

- Impostazioni → **Modelli**. Ogni riga dice installato o non installato come ti aspetti per la cartella `C:\Users\cammo\LiquidinoProva`.
- Premi **Cartella dei modelli**: si apre Esplora file lì. Prova anche gli altri tre tasti.
- Su un modello che non hai (per esempio VL), premi **Scarica**: la domanda deve dire nome del file, peso e cartella. Rispondi sì, guarda la percentuale, poi premi **Annulla**: nella cartella non deve restare un `.part`.
- Riscarica fino in fondo. La riga diventa «installato» e la chat funziona. Poi **Elimina**: il file finisce nel Cestino e la riga torna «non installato».
- Nel Mainframe cambia carattere, voce e minuti del ciclo, premi Salva, riapri: devono essere rimasti.

## 0.3.0

### Novità

- Le mani sul PC, col tasto Permesso acceso: apre un programma del menu Start dopo il tuo «sì», apre Documenti, Download, Immagini, Musica, Video e il Desktop, alza e abbassa il volume, mette in pausa la musica. Clic, tastiera, chiusure e installazioni non le fa: te lo dice.
- «Cosa c'è sullo schermo?»: Omni (o VL) guarda una foto dello schermo. La foto resta in memoria e non si salva. Le istruzioni che ci legge non le esegue.
- Il mondo si sfoglia: scegli la cartella, cerca nel nome e dentro i file, icone grandi, anteprima del file, apri il file.
- 👍 e 👎 sulle creazioni. I pensieri che ti piacciono fanno da esempio ai nuovi. Con 👎 un pensiero o una storia si rifà: in chat gli dici cosa cambiare.
- Il diario della giornata, in `mondo/diario`: i conti del giorno e i titoli di quello che ha creato, niente chiacchiere. Si scrive da solo quando va a dormire di notte, o col tasto 📔.
- Voce di riserva: se Omni non c'è, o la sua voce non esce, legge con le voci italiane di Windows. Niente download.
- In Impostazioni: «Prova il contesto sul serio» riempie davvero il motore partendo da 128K, scende finché regge e controlla se lo slot si salva su disco. «Togli internet a llama-server» crea la regola del firewall. Poi «Prova la voce» e «Copia dettagli errore».
- Accanto al Setup c'è `SHA256SUMS.txt`, per controllare il file scaricato.

### Correzioni

- Il registro non tiene più le tue frasi, solo quanti caratteri erano. Prima un ricordo che gli avevi fatto dimenticare restava scritto lì.
- «dimentica Rossi» toglie davvero Rossi: prima cercava «dimentica Rossi» e non trovava niente.
- Le regole di sicurezza stanno nel programma: valgono anche se cambi `sistema.md`.
- Se la voce non usciva, l'avatar restava fermo su «parla» e i modelli non si scaricavano più.
- Due note con lo stesso titolo nello stesso secondo non si sovrascrivono.
- «Un pensiero adesso» funziona anche col ciclo spento e di notte. Uno STOP mentre pensa non apre più la finestra d'errore.
- Se `impostazioni.json` si rovina, lo mette da parte e te lo dice, invece di perderlo.
- Lo STOP cancella anche la domanda ancora aperta.
- Lo slot della cache si salva davvero su disco prima di scaricare un modello, e si ripristina quando torna: prima la richiesta non diceva al router per quale modello era. L'ha trovato la prova di carico, con un llama-server vero.

### Da provare

- Accendi il Permesso (🖥️). Scrivi «apri il blocco note»: deve chiedere «Apro «Blocco note»?». Rispondi sì e si apre.
- Scrivi «alza il volume», «apri i download», «cosa c'è sullo schermo?».
- Spegni il Permesso e riscrivi «apri il blocco note»: deve dirti che serve il permesso, senza aprire niente.
- Impostazioni → «Prova il contesto sul serio». Aspetta l'esito: sulla RTX 4060 deve dire quanti K reggono le mani e se lo slot si salva.
- Impostazioni → «Togli internet a llama-server». Windows chiede il permesso. Dopo, la riga sotto i tasti dice che la regola c'è, e la chat funziona come prima.
- Nelle creazioni: 👎 su un pensiero, scrivi «più allegro» e premi Invio. Il pensiero nuovo compare, il vecchio è nel cestino.
- Sposta per un momento il file di Omni fuori dalla cartella dei modelli, premi Salva in Impostazioni (così rilegge i modelli) e scrivi «come stai?»: se Windows ha una voce italiana, la risposta si sente lo stesso. Poi rimetti il file al suo posto e premi di nuovo Salva.

## 0.2.1

### Novità

- Icona nell'area di notifica, in basso a destra. Tasto destro: mostra o nascondi l'avatar, aggiornamenti, esci.
- Lo stesso menu sta anche sull'avatar, con in più Parla, Chat, Creazioni, Permesso, STOP, Dormi e Impostazioni.
- Gli aggiornamenti, dal menu, dicono a che punto sono: controllo, sei aggiornato, scarico, riavvia.

### Correzioni

- Prima di rispondere, Liquidino carica davvero il modello e aspetta che sia pronto. Se non parte, lo scrive in chat e nel fumetto, e nel registro.
- Un turno annullato dice perché (STOP o Esc).
- Esci chiude anche llama-server. Se non risponde, dopo qualche secondo viene chiuso comunque.
- Per chiacchierare non chiede più conferma. La chiede solo se deve scrivere, ricordare o dimenticare, e la domanda sta sul fumetto con Sì e No.
- I dati (mondo, impostazioni, memoria) stanno in `%LOCALAPPDATA%\Liquidino-dati`, fuori dalla cartella che l'aggiornamento svuota. Se erano ancora nella cartella vecchia, li porta via al primo avvio.
- Mentre parla non si ascolta. Il registro è una riga per voce. d1 e la voce restano accesi finché si chiacchiera.
- Di notte non si addormenta se la chat è stata usata da poco.
- Sull'avatar i tasti sono simboli, con la spiegazione al passaggio del mouse, e compaiono solo quando il cursore è lì. Le risposte si leggono nel fumetto.

### Da provare

- Scrivi «ciao» in chat. Nel registro di oggi devono comparire «ricevuto», «chiedo il caricamento» e «caricato».
- Premi STOP. Deve comparire «scaricato» o «STOP premuto», e la finestra resta viva.
- Tasto destro sull'icona in basso a destra: nascondi l'avatar, poi mostralo di nuovo. Esci chiude tutto.

## 0.2.0

### Novità

- Si installa da solo: un file, `Liquidino-Setup.exe`. Icona sul desktop e nel menu Start. Non servono i diritti di amministratore.
- Se esce una versione nuova, la barra in alto della chat dice «Aggiornamento X disponibile». Un clic e si aggiorna da solo.
- I modelli non stanno nell'installer. Liquidino li cerca nella cartella di sempre e, se non li trova, lo dice.

### Correzioni

- Lo STOP non blocca più la finestra, anche premuto due volte.
- I tasti sotto l'avatar si usano con un clic: Parla è un interruttore, non va tenuto premuto.
- Nessun modello viene scaricato da internet. L'unica rete è il controllo degli aggiornamenti.

### Da provare

- Chiudi la prova vecchia. Doppio clic su `Liquidino-Setup.exe`. Poi apri Liquidino dal desktop.
- Premi STOP due volte: la finestra deve restare viva.
- Sposta la cartella dei modelli, o segnala in Impostazioni una cartella vuota: deve dirtelo, senza scaricare niente.

## 0.1.1 — lo STOP non blocca più

I tasti stanno tutti sotto l'avatar e si usano con un clic: Parla, Chat, Creazioni, Permesso, STOP, Dormi, Impostazioni. Parla è un interruttore, non va tenuto premuto. Lo STOP torna subito, anche se lo premi due volte. Nessun modello viene scaricato da internet.

## 0.1.0 — la prima prova

Liquidino sta sul desktop. Lo puoi spostare, ci clicchi e si apre la chat con le sue creazioni.

Al primo avvio scegli il nome, il carattere e la voce. Il programma misura il PC e decide il contesto, senza andare in rete.

La voce, se i modelli ci sono, esce dall'italiano di Omni: lui capisce, scrive, e `/tts` legge. d1 sceglie l'azione e, se non è sicuro, chiede.

Di notte dorme. Ogni ora, se lo lasci acceso, lascia un pensiero nella sua cartella.
