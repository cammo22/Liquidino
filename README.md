# Liquidino

Compagno da desktop per Windows. Sta sul PC, parla italiano, non va in internet per pensare. I modelli sono i LFM3 di Liquid AI, eseguiti in locale con llama.cpp. Solo uso personale.

## Installazione

1. Scarica `Liquidino-Setup.exe` dalla [release](https://github.com/cammo22/Liquidino/releases/latest). È il primo file.
2. Chiudi Liquidino se è ancora aperto.
3. Doppio clic sul Setup. Non chiede i diritti di amministratore.
4. Apri Liquidino dall'icona sul desktop o dal menu Start.
5. Al primo avvio: il tuo nome, il carattere e la voce. I tasti sotto l'avatar sono un clic. I modelli restano in `C:\Users\cammo\LiquidinoProva` (Impostazioni, se la cartella è un'altra): non sono dentro l'installer.

Se manca un modello, Liquidino lo dice. Non lo scarica. L'unica cosa che guarda in rete è se c'è un aggiornamento: lo scrive nella barra in alto della chat.

## Cosa fa questa prova

- Avatar trasparente, sempre davanti, lo sposti col mouse. Stati: **dorme**, **ascolta**, **pensa**, **parla**. La rotella lo ingrandisce. Ogni tanto cambia oggetto, fra quelli già approvati del Convertitore.
- Un clic apre chat e mondo. Il mondo è la cartella `%LOCALAPPDATA%\Liquidino\mondo`: note, idee, memoria, cestino, registro. Si apre anche da «Apri cartella».
- d1 sceglie fra sei azioni: rispondi, crea nota, ricorda, dimentica, mostra le creazioni, dormi. Sotto sicurezza 0,6 chiede «sì» o «no».
- La chat usa Omni, se c'è, altrimenti LFM3-1.7B. La voce è il testo letto da `/tts` in italiano. Il ragionamento degli strumenti è spento.
- Il regista tiene accesi al massimo due modelli e li scarica quando non servono. Di notte (dalle 23 alle 7) dorme.
- Ogni ora, se non lo spegni, lascia un pensiero in `mondo/idee`. Nelle prime due settimane è un saluto. «Un pensiero adesso» non ti fa aspettare l'ora.
- Tasto **Permesso** accanto all'avatar: viene solo segnato. Le mani sul PC arrivano dopo. **STOP** scarica i modelli subito.
- Memoria in `mondo/memoria/cosa-so-di-te.md`. Si cancella a mano o con «dimentica». Il registro non conserva il testo cancellato.
- Prompt e catalogo, da leggere e modificare (poi riavvia): `%LOCALAPPDATA%\Liquidino\prompt\sistema.md` e `catalogo\azioni.json`.

Trascina un'immagine sull'avatar: se c'è Omni (o VL) la guarda. Un altro tipo di file: te lo dice e non lo tocca.

## Cosa non fa ancora

La parola «Ehi Liquidino» non è addestrata. L'elenco è in [ROADMAP.md](ROADMAP.md).

## Da dove viene l'avatar

Dalle immagini già approvate del [DaP Convertitore](https://github.com/cammo22/DaP-Convertitore). Non sono state rifatte. Dettaglio in `PROVENIENZA.md`.

## Licenza

Codice MIT (vedi [LICENSE](LICENSE)). Pesi dei modelli: LFM Open License v1.0, non sono nello zip, uso personale.
