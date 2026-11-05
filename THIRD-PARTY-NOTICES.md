# Componenti di terzi

Il codice di Liquidino è MIT (vedi [LICENSE](LICENSE)). Qui sotto c'è quello che viene da altri.

## Dentro l'installer

| Componente | Versione | Licenza | Per cosa |
| --- | --- | --- | --- |
| .NET (runtime, WPF, Windows Forms) | 8 | MIT, © .NET Foundation e contributori | Il programma è autonomo: il runtime viaggia con lui. |
| NAudio | 2.2.1 | MIT, © Mark Heath e contributori | Microfono e lettura della voce. |
| Velopack | 1.2.161 | MIT, © Velopack Ltd. | Installer e aggiornamenti. |
| System.Speech | 8.0.0 | MIT, © .NET Foundation e contributori | Le voci italiane di Windows, quando Omni non c'è. |
| Orbitron | — | SIL Open Font License 1.1, © 2018 The Orbitron Project Authors | Carattere dei titoli. `licenze/OFL-orbitron.txt` |
| Rajdhani | — | SIL Open Font License 1.1, © 2014 Indian Type Foundry | Carattere del testo. `licenze/OFL-rajdhani.txt` |
| DSEG7 Classic | — | SIL Open Font License 1.1, © 2020 keshikan, nome riservato «DSEG» | Carattere a segmenti. `licenze/OFL-dseg.txt` |
| Immagini dell'avatar | — | MIT, © 2026 DaProdProduzioni ([DaP Convertitore](https://github.com/cammo22/DaP-Convertitore)) | Gli otto oggetti già approvati. Vedi `PROVENIENZA.md`. |

## Fuori dall'installer (stanno già sul PC, o servono solo alla CI)

| Componente | Licenza | Nota |
| --- | --- | --- |
| llama.cpp (`llama-server`) | MIT, © The ggml authors | Non è incluso: si indica la cartella dove c'è già. |
| Modelli LFM3 di Liquid AI (GGUF) | LFM Open License v1.0 | Non sono inclusi. Uso personale. |
| Voci di Windows (SAPI) | Parte di Windows | Si usano quelle installate, non se ne scarica nessuna. |
| `stories15M-q4_0.gguf` (da `ggml-org/models`, nato da `karpathy/llama2.c`) | MIT, come il repository llama2.c | Solo nella prova automatica della CI, mai nel programma. |
| xUnit, Microsoft.NET.Test.Sdk | Apache-2.0, MIT | Solo per le prove. |

Previsti, non ancora presenti: Silero VAD (MIT) e il codice di openWakeWord (Apache-2.0) per la parola «Ehi Liquidino». I modelli già pronti di openWakeWord non si usano: sono in inglese e non commerciali.
