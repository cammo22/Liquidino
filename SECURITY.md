# Sicurezza

## Versioni seguite

Solo l'ultima release (`Liquidino-Setup.exe` nella [pagina delle release](https://github.com/cammo22/Liquidino/releases/latest)). Le prove vecchie (`-prova`) non si correggono più: si aggiorna.

## Segnalare un problema

Non aprire una issue pubblica con i dettagli. Usa la segnalazione privata di GitHub (scheda **Security**, poi **Report a vulnerability**) oppure scrivi in privato al proprietario del repository, `cammo22`.

Scrivi: la versione, che cosa hai fatto, che cosa è successo, e se serve il testo di **Impostazioni → Copia dettagli errore** (non contiene la chat né la memoria, ma rileggilo prima di mandarlo).

## Cosa conta come problema di sicurezza

- Liquidino va in rete per qualcosa che non sia il controllo degli aggiornamenti.
- `llama-server` ascolta fuori da `127.0.0.1`.
- Le mani fanno un gesto senza il permesso acceso, o aprono un programma senza il tuo «sì».
- Un file fuori da `%LOCALAPPDATA%\Liquidino-dati\mondo` viene scritto, spostato o cancellato dal programma.
- Un'istruzione letta in un file, in un'immagine o sullo schermo viene eseguita.
- Il testo di un ricordo dimenticato resta nel registro.

Le regole complete sono in [docs/sicurezza-e-privacy.md](docs/sicurezza-e-privacy.md).
