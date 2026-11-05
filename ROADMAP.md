# Roadmap

Avanzamento verso la versione definitiva:

- **Nel programma (0.5.0): 75%.** È scritto, provato dalle prove automatiche e dall'avvio dell'app in CI su Windows.
- **Verificato sul PC di prova: 41%.** Quello della 0.2.1. Sale quando i punti «Da provare» del [CHANGELOG](CHANGELOG.md) passano sul PC con la RTX 4060.

Il 100% è Liquidino che sta sul desktop da solo: ti sente quando lo chiami, fa le cose sul PC solo se glielo permetti, ricorda, crea, e si può far evolvere senza perdere la versione di prima.

| Pezzo | Peso | Nel programma | Verificato | Manca per il 100% |
| --- | ---: | ---: | ---: | --- |
| Parola «babbasone» e Orecchie | 10 | 8 | 0 | Nel programma: 👂, rilevatore di voce, Parakeet v3 sulla CPU, parola tarata sulle tue registrazioni con soglia sopra le parole simili, audio del PC senza la sua voce, eco delle casse, chiamata su Discord. In CI Parakeet trascrive un WAV vero. Manca la prova sul PC vero, col tuo microfono, e un modello più piccolo sempre acceso se Parakeet pesa troppo sulla CPU. |
| Uso del PC | 15 | 8 | 2 | Ci sono il permesso, i gesti chiusi (programma dopo il sì, cartelle, volume, pausa, schermo), lo STOP e il registro. Mancano il mouse, la tastiera e il suo cursore sullo schermo, e i livelli di permesso per singolo gesto. |
| Voce | 10 | 9 | 6 | Capisce e legge con Omni (`/tts`); se Omni non c'è, le voci italiane di Windows. Non si ascolta mentre parla. In chiamata la sua voce va nel tuo microfono con VB-CABLE. Manca la voce intrecciata di Omni in italiano, che sulla RTX 4060 non era affidabile. |
| Modelli e dati | 10 | 10 | 8 | Nel programma c'è tutto: caricamento prima di rispondere, due modelli accesi mentre si chiacchiera, dati fuori dalla cartella di Velopack, prova di carico, slot salvato su disco (in CI con un llama-server vero sì; prima non funzionava), regola del firewall. Va provato sul PC. |
| Interfaccia | 8 | 8 | 7 | C'è il Mainframe e la pagina Modelli (scarica, elimina, cartelle). Manca la bocca sincronizzata (TalkingHead): gli sprite approvati restano, e un viso nuovo vuole l'ok del proprietario. |
| Memoria e creazioni | 10 | 10 | 5 | Nel programma: note, memoria in markdown, cestino, cartelle, ricerca, icone grandi, anteprima, diario della giornata. Va provato sul PC. |
| Evolversi | 10 | 1 | 1 | Conta gli errori e, dopo dodici, lo dice una volta al giorno. Manca l'addestramento vero e la versione di prima tenuta da parte: serve la GPU e un insieme di esempi tuoi, e non parte mai da solo. |
| Notte e autonomia | 7 | 7 | 6 | Nel programma: notte, ciclo spegnibile, fase di amicizia, voto sulle creazioni, rifacimento guidato, diario. Va provato sul PC. |
| Vista e altri modelli | 10 | 4 | 2 | Omni o VL guardano le immagini trascinate e lo schermo (col permesso). Il regista riconosce d1-Omni e Audio e li carica se i file ci sono, mai più di due insieme, ma non sono ancora nel giro quotidiano. |
| Contesto misurato sul serio | 10 | 10 | 4 | La prova di carico riempie davvero la cache (Impostazioni → «Prova il contesto sul serio»), da 128K in giù. In CI gira in piccolo con un llama-server vero. Va fatta sul PC di prova. |

Si sale nella colonna «Nel programma» quando un pezzo della colonna «Manca» entra nel codice con le sue prove. Si sale in «Verificato» quando resta giusto anche sul PC.

## Cosa resta, e perché non è già qui

- **«Babbasone» sul PC vero**: la parola e le Orecchie sono provate con prove automatiche e con Parakeet in CI, ma il microfono, la stanza e Discord li ha solo il PC di prova.
- **Mouse e tastiera**: un clic sbagliato può cancellare o mandare qualcosa. Prima vanno decisi insieme i livelli di permesso e cosa vede Liquidino mentre lo fa.
- **Evolversi**: un addestramento vero sul PC, con la versione di prima tenuta da parte. È un lavoro a sé, e parte solo se lo chiedi tu.
- **Bocca che si muove**: serve un viso nuovo, e l'avatar non si rifà senza l'ok del proprietario.
