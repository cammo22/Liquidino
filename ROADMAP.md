# Roadmap

Avanzamento verso la versione definitiva:

- **Nel programma (0.4.0): 66%.** È scritto, provato dalle prove automatiche e dall'avvio dell'app in CI su Windows.
- **Verificato sul PC di prova: 41%.** Quello della 0.2.1. Sale quando i punti «Da provare» del [CHANGELOG](CHANGELOG.md) passano sul PC con la RTX 4060.

Il 100% è Liquidino che sta sul desktop da solo: ti sente quando lo chiami, fa le cose sul PC solo se glielo permetti, ricorda, crea, e si può far evolvere senza perdere la versione di prima.

| Pezzo | Peso | Nel programma | Verificato | Manca per il 100% |
| --- | ---: | ---: | ---: | --- |
| Parola «Ehi Liquidino» | 10 | 0 | 0 | Il modello addestrato da noi, con registrazioni della parola in italiano. La cartella `orecchio/` c'è. Silero VAD è MIT, il codice di openWakeWord è Apache-2.0; i modelli già pronti di openWakeWord non si usano. Per ora: clic, «Parla», Ctrl+Spazio. |
| Uso del PC | 15 | 8 | 2 | Ci sono il permesso, i gesti chiusi (programma dopo il sì, cartelle, volume, pausa, schermo), lo STOP e il registro. Mancano il mouse, la tastiera e il suo cursore sullo schermo, e i livelli di permesso per singolo gesto. |
| Voce | 10 | 8 | 6 | Capisce e legge con Omni (`/tts`); se Omni non c'è, le voci italiane di Windows. Non si ascolta mentre parla. Manca la voce intrecciata di Omni in italiano, che sulla RTX 4060 non era affidabile. |
| Modelli e dati | 10 | 10 | 8 | Nel programma c'è tutto: caricamento prima di rispondere, due modelli accesi mentre si chiacchiera, dati fuori dalla cartella di Velopack, prova di carico, slot salvato su disco (in CI con un llama-server vero sì; prima non funzionava), regola del firewall. Va provato sul PC. |
| Interfaccia | 8 | 8 | 7 | C'è il Mainframe e la pagina Modelli (scarica, elimina, cartelle). Manca la bocca sincronizzata (TalkingHead): gli sprite approvati restano, e un viso nuovo vuole l'ok del proprietario. |
| Memoria e creazioni | 10 | 10 | 5 | Nel programma: note, memoria in markdown, cestino, cartelle, ricerca, icone grandi, anteprima, diario della giornata. Va provato sul PC. |
| Evolversi | 10 | 1 | 1 | Conta gli errori e, dopo dodici, lo dice una volta al giorno. Manca l'addestramento vero e la versione di prima tenuta da parte: serve la GPU e un insieme di esempi tuoi, e non parte mai da solo. |
| Notte e autonomia | 7 | 7 | 6 | Nel programma: notte, ciclo spegnibile, fase di amicizia, voto sulle creazioni, rifacimento guidato, diario. Va provato sul PC. |
| Vista e altri modelli | 10 | 4 | 2 | Omni o VL guardano le immagini trascinate e lo schermo (col permesso). Il regista riconosce d1-Omni e Audio e li carica se i file ci sono, mai più di due insieme, ma non sono ancora nel giro quotidiano. |
| Contesto misurato sul serio | 10 | 10 | 4 | La prova di carico riempie davvero la cache (Impostazioni → «Prova il contesto sul serio»), da 128K in giù. In CI gira in piccolo con un llama-server vero. Va fatta sul PC di prova. |

Si sale nella colonna «Nel programma» quando un pezzo della colonna «Manca» entra nel codice con le sue prove. Si sale in «Verificato» quando resta giusto anche sul PC.

## Cosa resta, e perché non è già qui

- **«Ehi Liquidino»**: senza un modello addestrato sulla parola detta in italiano non c'è niente da far girare. Servono registrazioni (anche sintetiche) e un addestramento.
- **Mouse e tastiera**: un clic sbagliato può cancellare o mandare qualcosa. Prima vanno decisi insieme i livelli di permesso e cosa vede Liquidino mentre lo fa.
- **Evolversi**: un addestramento vero sul PC, con la versione di prima tenuta da parte. È un lavoro a sé, e parte solo se lo chiedi tu.
- **Bocca che si muove**: serve un viso nuovo, e l'avatar non si rifà senza l'ok del proprietario.
