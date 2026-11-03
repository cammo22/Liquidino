# Roadmap

Questa è la prova minima: si avvia, si parla, si scrive, si ricorda. Il resto è scritto qui, non è nel programma.

## Rimandato

- **Parola «Ehi Liquidino».** La struttura c'è (`orecchio/`, Silero VAD licenza MIT, codice openWakeWord licenza Apache-2.0). Manca il modello addestrato da noi. I modelli già pronti di openWakeWord sono in inglese e non commerciali: non li mettiamo. Per ora: clic, tasto «Parla», Ctrl+Spazio.
- **Uso del PC.** C'è il tasto Permesso, lo STOP e il registro. Non c'è il cursore suo, né l'elenco delle azioni sul desktop, né il mouse. Se glielo chiedi, dice di farlo tu.
- **Evolversi.** Conta gli errori e, dopo dodici, lo dice una volta al giorno. Non parte nessun addestramento e non sostituisce il modello. La versione precedente si conserverà quando questa parte esisterà.
- **Voce diretta.** La prova sulla RTX 4060 ha mostrato che la voce intrecciata di Omni in italiano non è affidabile. Si usa il testo più `/tts`. Si riprova quando i modelli migliorano.
- **d1-Omni, VL, Audio.** Il regista li riconosce e li può caricare se i file GGUF ci sono (mai più di due insieme). Nel flusso di tutti i giorni usiamo d1, Omni e, per compilare le azioni, LFM3-1.7B. VL serve se ci trascini un'immagine e Omni non c'è.
- **Contesto 128K misurato sul serio.** All'avvio si leggono RAM e VRAM e si sceglie (sul PC di prova: 128K per le mani). Non si fa ancora una prova di carico che riempie davvero la cache. Cache KV q4 e flash-attention sono nel preset. Il salvataggio dello slot su disco viene chiamato; va verificato su quel PC.
- **Creazioni.** C'è l'elenco dei file, il cestino e la cartella. Mancano le icone grandi, i filtri, l'anteprima e il diario della giornata.
- **Avatar che muove la bocca.** Gli sprite approvati restano. Un viso 3D con la bocca sincronizzata (TalkingHead) è un'idea successiva, e servirebbe un modello con le forme della bocca.
- **Piano B della voce su CPU** (Kokoro) se Omni non c'è.
- **Regola del firewall** che blocca llama-server verso l'esterno. Il programma lo avvia già solo su 127.0.0.1.
- **Fase di amicizia più ricca.** Per i primi 14 giorni il pensiero orario è un saluto, non una creazione scelta da lui. Dopo, d1 può scegliere il silenzio o una nota. Manca il voto tuo sulle creazioni e il rifacimento guidato, oltre al cestino quando il file esce vuoto.

## Già in questa prova

Avatar con i quattro stati, chat, push-to-talk, regista con al massimo due modelli, catalogo di sei azioni con soglia 0,6, memoria in markdown, ciclo orario spegnibile, STOP, registro, setup del primo avvio.
