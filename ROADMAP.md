# Roadmap

Avanzamento verso la versione definitiva: **41%**.

Il 100% è Liquidino che sta sul desktop da solo: ti sente quando lo chiami, fa le cose sul PC solo se glielo permetti, ricorda, crea, e si può far evolvere senza perdere la versione di prima. Quello che c'è oggi è il compagno che parla, scrive e si aggiorna. Il resto è elencato qui sotto, con il peso che ha sul totale.

| Pezzo | Peso | Fatto | Manca per il 100% |
| --- | ---: | ---: | --- |
| Parola «Ehi Liquidino» | 10 | 0 | Manca il modello addestrato da noi. La cartella `orecchio/` c'è. Silero VAD è MIT, il codice di openWakeWord è Apache-2.0. I modelli già pronti di openWakeWord non si usano. Per ora: clic, «Parla», Ctrl+Spazio. |
| Uso del PC | 15 | 2 | C'è il permesso, lo STOP e il registro. Manca il suo cursore, l'elenco delle azioni sul desktop e il mouse. Se glielo chiedi, dice di farlo tu. |
| Voce | 10 | 6 | Capisce e legge con Omni (`/tts`). Non si ascolta mentre parla. Manca la voce intrecciata in italiano, che sulla RTX 4060 non era affidabile, e un piano B su CPU (Kokoro) se Omni non c'è. |
| Modelli e dati | 10 | 8 | Carica il modello prima di rispondere, ne tiene due accesi mentre si chiacchiera, e i dati stanno fuori dalla cartella di Velopack. Manca la prova di carico che riempie davvero la cache da 128K e la verifica dello slot su quel PC. La regola del firewall verso l'esterno non c'è: il server ascolta già solo su 127.0.0.1. |
| Interfaccia | 8 | 7 | Fumetto sull'avatar, scelte cliccabili, tasti con simboli che compaiono al passaggio del mouse, colori dei servizi accesi. Manca la bocca sincronizzata (TalkingHead): gli sprite approvati restano. |
| Memoria e creazioni | 10 | 5 | Note, memoria in markdown, cestino, elenco file. Mancano le icone grandi, i filtri, l'anteprima e il diario della giornata. |
| Evolversi | 10 | 1 | Conta gli errori e, dopo dodici, lo dice una volta al giorno. Non parte nessun addestramento e non sostituisce il modello. La versione precedente si conserverà quando questa parte esisterà. |
| Notte e autonomia | 7 | 6 | Di notte dorme, ma aspetta se stai usando la chat. Il pensiero orario si può spegnere. Manca il voto sulle creazioni e il rifacimento guidato. Nelle prime due settimane il pensiero è un saluto. |
| Vista e altri modelli | 10 | 2 | Il regista riconosce d1-Omni, VL e Audio e li carica se i file ci sono, mai più di due insieme. Nel flusso di tutti i giorni usiamo d1, Omni e, per compilare, LFM3-1.7B. VL serve se ci trascini un'immagine e Omni non c'è. Non sono ancora nel giro quotidiano. |
| Contesto misurato sul serio | 10 | 4 | All'avvio si leggono RAM e VRAM e si sceglie (sul PC di prova: 128K per le mani). Cache KV q4 e flash-attention sono nel preset. Non c'è ancora una prova che riempie la cache. |

41 su 100. Si sale quando un pezzo della colonna «Manca» entra nel programma e resta verificato sul PC.
