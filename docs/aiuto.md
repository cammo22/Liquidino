# Aiuto per l'utente

Pagina di aiuto di Liquidino, organizzata come una guida rapida più una sezione sui problemi. Ispirata alla struttura dell'aiuto di Capri (installazione, impostazioni, cervello, voce, memoria, spostamento, problemi), ma scritta solo con quello che Liquidino fa davvero. Quello che non è sicuro è segnato **da verificare**.

## In breve

Liquidino è un compagno da desktop per Windows a 64 bit. Parla italiano, sta sul PC e non va in internet per pensare. I modelli sono i LFM3 di Liquid AI, in locale con llama.cpp. Solo uso personale.

## Installare

1. Scarica `Liquidino-Setup.exe` dalla [release](https://github.com/cammo22/Liquidino/releases/latest). È il primo file della lista.
2. Chiudi Liquidino se è aperto.
3. Doppio clic sul Setup. Non servono i diritti di amministratore.
4. Apri Liquidino dall'icona sul desktop o dal menu Start.
5. Al primo avvio scegli il tuo nome, il carattere e la voce.

I modelli **non** sono nell'installer. Si cercano in `C:\Users\cammo\LiquidinoProva`; se stanno altrove si cambia da Impostazioni. Se un modello manca, Liquidino lo dice e non lo scarica.

## Il tuo compagno

- **L'avatar** è trasparente e sempre davanti. Lo sposti col mouse, la rotella lo ingrandisce. Stati: dorme, ascolta, pensa, parla.
- **I tasti sotto l'avatar** si usano con un clic: Parla, Chat, Creazioni, Permesso, STOP, Dormi / Sveglia, Impostazioni.
- **Parla** è un interruttore: il primo clic ascolta, il secondo manda la frase. Stessa cosa con `Ctrl+Spazio`.
- **Chat e Creazioni** aprono la finestra. Il «mondo» è la cartella `%LOCALAPPDATA%\Liquidino\mondo`, apribile con «Apri cartella».
- **STOP** ferma tutto e scarica i modelli. Si può premere anche due volte.
- **Immagini**: trascina un'immagine sull'avatar. Se c'è Omni (o VL) la guarda. Un altro tipo di file: te lo dice e non lo tocca.

## Cosa sa fare

Sei azioni, scelte dal modello d1: rispondere, creare una nota, ricordare, dimenticare, mostrare le creazioni, dormire. Se non è abbastanza sicuro (sotto 0,6) chiede «sì» o «no».

## Memoria

Quello che sa di te sta in `mondo/memoria/cosa-so-di-te.md`. Puoi aprirlo, correggerlo o cancellarlo a mano. Con «dimentica» lo toglie lui, e il registro non conserva il testo tolto. Tutto resta sul tuo PC.

## Personalità e azioni

Due file si leggono e si modificano (poi riavvia):

- `%LOCALAPPDATA%\Liquidino\prompt\sistema.md`: come parla e cosa non deve dire.
- `%LOCALAPPDATA%\Liquidino\catalogo\azioni.json`: le sei azioni.

Cambiarli non toglie i limiti: oggi Liquidino non ha mani sul PC.

## Pensieri e notte

Ogni ora, se non lo spegni, lascia un pensiero in `mondo/idee`. Nei primi 14 giorni è un saluto. «Un pensiero adesso» non ti fa aspettare l'ora. Dalle 23 alle 7 dorme.

## Aggiornare

La barra in alto della chat dice «Aggiornamento X disponibile». Un clic e si aggiorna da solo. È l'unica cosa che Liquidino guarda in rete.

## Disinstallare

**Da verificare.** Dall'elenco delle app di Windows (Velopack registra la disinstallazione). I tuoi dati restano in `%LOCALAPPDATA%\Liquidino`: cancella la cartella a mano se vuoi eliminarli. I modelli stanno altrove e non vengono toccati.

## Problemi comuni

| Problema | Cosa fare |
|---|---|
| Windows mostra «Windows ha protetto il PC» | **Ulteriori informazioni**, poi **Esegui comunque**. Scarica solo dalla pagina delle release. (**Da verificare** se l'installer è firmato.) |
| L'antivirus segnala il Setup | Di solito è un falso positivo su un programma non firmato. Controlla l'origine, poi consenti il file. |
| «Manca un modello» | Metti i file GGUF nella cartella dei modelli, o indica quella giusta in Impostazioni. Non scarica nulla da solo. |
| Premo STOP e la finestra non risponde | Non dovrebbe più succedere dalla 0.1.1. Se succede, segnala con i passaggi esatti. |
| Risposte lente o il PC fatica | Il regista tiene accesi al massimo due modelli. Chiudi giochi e programmi pesanti. Contesto troppo grande per la RAM: **da verificare** come ridurlo da Impostazioni. |
| Non sento la voce | Controlla uscita audio e volume. La voce di Liquidino è il testo letto da `/tts`. Il piano B su CPU (Kokoro) è in roadmap. |
| Non mi ascolta quando parlo | Il microfono giusto è scelto in Impostazioni? Il tasto Parla è un interruttore: il secondo clic invia. La parola «Ehi Liquidino» non esiste ancora. |
| «Non so farlo, fallo tu» | Previsto: non ha ancora le mani sul PC. È nella [roadmap](../ROADMAP.md). |
| Voglio ripartire da zero | Chiudi Liquidino e rinomina `%LOCALAPPDATA%\Liquidino` (non la cartella dei modelli). Al prossimo avvio dovrebbe rifare il setup (**da verificare**). |

## Chiedere aiuto

Apri una issue su GitHub con: versione (barra delle novità), cosa hai fatto, cosa è successo, e se vuoi il file `diario-tecnico.log` (si trova in `%LOCALAPPDATA%\Liquidino`; controllalo prima, potrebbe contenere frasi tue). Una voce «Copia dettagli errore» come quella di Capri è una buona idea: vedi [idee-da-capri.md](idee-da-capri.md).
