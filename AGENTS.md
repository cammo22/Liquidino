# Istruzioni per chi lavora su Liquidino (persone e agenti)

Liquidino è un compagno da desktop per Windows, in italiano, con modelli LFM3 in locale. Uso personale. Rete solo per il controllo aggiornamenti e per i modelli che scarichi tu dalla pagina Modelli.

## Leggi prima di cambiare qualcosa

- [docs/README.md](docs/README.md): indice della documentazione.
- [docs/architettura.md](docs/architettura.md): progetti, cartelle dati, flusso.
- [docs/contribuire.md](docs/contribuire.md): convenzioni, build, prove, regole di prodotto.
- [docs/sicurezza-e-privacy.md](docs/sicurezza-e-privacy.md): rete, permessi, dati.
- [docs/idee-da-capri.md](docs/idee-da-capri.md): confronto con il progetto Capri Desktop e prossime idee.
- [ROADMAP.md](ROADMAP.md): cosa manca, con due colonne: nel programma e verificato sul PC.

## Comandi

```bash
dotnet test Liquidino.sln -c Release --nologo
```

L'app WPF (`net8.0-windows`) si avvia solo su Windows. Su Linux si compila con l'SDK di Microsoft e `-p:EnableWindowsTargeting=true`. L'installer si fa con `./scripts/pacchetto.ps1`, la prova di avvio con `./scripts/prova-avvio.ps1` (tutte e due su Windows, la CI le esegue).

## Regole che non si rompono

1. Nessuna nuova chiamata di rete né tracciamento senza chiederlo, e va scritta in `docs/sicurezza-e-privacy.md`.
2. Nessun modello scaricato senza un tuo clic e il tuo sì (pagina Modelli), nessun peso nel repository.
3. Liquidino non dice di aver fatto ciò che non ha fatto («Questo fallo tu:» + motivo).
4. Azioni che inviano, pubblicano, comprano, cancellano o installano chiedono conferma. Le mani sul PC fanno solo i gesti di `InterpretePc`, col Permesso acceso.
5. La logica sta in `src/Liquidino.Nucleo` e ha una prova in `test/Liquidino.Prove`.
6. Avatar e personaggio non si rifanno senza l'ok del proprietario.
7. Non copiare codice, grafica o testi da Capri (licenza PolyForm Noncommercial): solo idee.

## Lingua e stile

Testi per l'utente, commit e documenti in italiano, dai del tu, frasi corte. Termini tecnici e nomi di codice restano come sono.

## Dopo ogni modifica

Aggiorna `CHANGELOG.md` se cambia il comportamento visibile, e i file in `docs/` toccati dalla modifica.
