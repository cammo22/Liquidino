# Documentazione di Liquidino

Questa cartella raccoglie le informazioni di contesto del progetto. È pensata per chi ci lavora, persone o agenti, e per chi vuole capire come ragiona Liquidino senza leggere tutto il codice.

Il materiale nasce guardando due progetti simili:

- [capri-desktop](https://github.com/lachydotmcg/capri-desktop): compagno da desktop per Windows, open source.
- La guida di aiuto di Capri (`site/help/knowledge.md` dello stesso repository, pubblicata su capriai.app/help).

Capri è un progetto diverso, con un'altra licenza e un altro codice. Qui lo usiamo solo come fonte di idee e confronto. **Non si copia codice, grafica, voce o testi da Capri**: la sua licenza (PolyForm Noncommercial 1.0.0) lo vieta senza attribuzione, e il carattere di Liquidino è un altro.

## Indice

| File | Cosa contiene |
|---|---|
| [architettura.md](architettura.md) | Come è fatto Liquidino oggi: progetti, cartelle dati, flusso di una richiesta. |
| [aiuto.md](aiuto.md) | Guida per l'utente e risoluzione dei problemi, nello stile di una pagina di aiuto. |
| [sicurezza-e-privacy.md](sicurezza-e-privacy.md) | Cosa resta sul PC, cosa va in rete, regole di sicurezza e permessi. |
| [contribuire.md](contribuire.md) | Convenzioni di codice, build, prove, commit, rilascio. |
| [progetto-di-riferimento-capri.md](progetto-di-riferimento-capri.md) | Scheda di Capri: funzioni, stack, struttura, licenza, roadmap. |
| [idee-da-capri.md](idee-da-capri.md) | Confronto Liquidino/Capri: cosa c'è già, cosa è in roadmap, cosa potremmo prendere. |

I file nella radice (`README.md`, `ROADMAP.md`, `CHANGELOG.md`, `NOTE-RELEASE.md`) restano la fonte per l'utente finale. Se un fatto compare qui e lì, vale la radice.

## Come tenerla aggiornata

- Cambia il comportamento visibile di Liquidino: aggiorna `aiuto.md` e `CHANGELOG.md`.
- Cambia un percorso, un file di dati o un progetto: aggiorna `architettura.md`.
- Compare una nuova chiamata di rete o un nuovo permesso: aggiorna `sicurezza-e-privacy.md` nello stesso commit.
- Una voce di `ROADMAP.md` viene realizzata: aggiorna lo stato in `idee-da-capri.md`.
- Dove un'informazione non è verificata sul codice c'è scritto **da verificare**. Chi la verifica toglie la scritta.
