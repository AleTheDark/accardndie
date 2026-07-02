# PvP — come provarlo in Unity

## 1. Avvia il server

```
cd Server/AccardND.Server
dotnet run
```

Resta in ascolto su `localhost:5017`.

## 2. Prepara la scena

1. Crea una scena vuota (o usane una di prova).
2. Crea un GameObject vuoto e aggiungi il componente **PvpBootstrap**
   (`Assets/_Project/Scripts/PvpUi/PvpBootstrap.cs`).
3. Nell'Inspector imposta:
   - **Username / Password** — l'account viene registrato al primo avvio.
   - **Room Code To Join** — solo per chi entra in una stanza esistente.
   - **Card Database** (opzionale) — trascina l'asset CardDatabase per usare
     il loadout "scala 2-10"; senza, si gioca coi goblin da 2.

Canvas, EventSystem e tutta la UI vengono creati da codice al Play.

## 3. Gioca

- **Crea stanza** → appare il codice a 6 lettere da condividere.
- Il secondo giocatore (altra istanza dell'editor, build, o un secondo
  progetto) imposta il codice nell'Inspector e preme **Entra con codice**.
- In alternativa entrambi premono **Cerca avversario (coda)**.

Nel match: tocca una carta della mano per schierarla; nel tuo turno tocca un
nemico per attaccare, oppure usa **Abilità** (poi tocca il bersaglio),
**Attach** (carte 2-4, tocca l'alleato) o **Passa**. Al round decisivo scegli
3 carte tra le 9 e conferma. Il timer server (60s) gioca al posto tuo se non
agisci; al terzo timeout consecutivo perdi a tavolino.

## Note

- Due istanze sulla stessa macchina: la via più rapida è una build standalone
  (File → Build) affiancata all'editor, con due username diversi.
- La UI attuale è funzionale ma volutamente essenziale (tile colorate, niente
  artwork): serve a giocare e testare le regole. Grafica e loadout builder
  arrivano dopo.
- Il replay del client (`PvpClientMatchState`) è testato contro il motore:
  se la UI mostra uno stato sbagliato, il bug è lì, non nel server.
