# Acquisti in-app (Google Play)

## Obiettivo

Vendere quattro cose a valuta reale senza che il client possa concedersele da solo. La
progressione del gioco e' autoritativa lato server: le classi e le supreme sono righe in
`single_player_unlocks`, quindi un acquisto che vive solo sul telefono non sbloccherebbe
niente. La ricevuta di Google e' l'unica cosa che il client puo' portare; il server la
verifica e decide.

Principio guida: **si conferma a Google solo dopo che il server ha concesso**. Prima di
quel momento l'ordine resta pendente. Se il gioco muore nel mezzo, Google lo ripresenta al
prossimo avvio e il giro ricomincia: il giocatore non perde l'acquisto, e noi non
consegniamo niente che non sia stato pagato.

## Il catalogo

| Prodotto | Id su Play Console | Prezzo di riferimento | Concede |
|---|---|---|---|
| Niente pubblicita' | `no_ads` | 2,99 € | Spegne gli annunci e condona i cancelli |
| Classi | `all_classes` | 9,99 € | Ogni voce `class` del Santuario |
| Classi + supreme | `all_classes_supreme` | 14,99 € | Ogni `class` e ogni `secondAbility` |
| Solo supreme | `supreme_upgrade` | 4,99 € | Ogni `secondAbility` |

Tutti **non consumabili** (una volta sola, ripristinabili).

`supreme_upgrade` esiste perche' Google non ha un percorso di upgrade tra prodotti una
tantum: senza, chi ha gia' speso 9,99 per le classi dovrebbe ricomprare il pacchetto intero
per avere le supreme. La tile compare **solo** a chi possiede le classi ma non le supreme;
a tutti gli altri il negozio mostra i due pacchetti interi.

Gli id vivono in `Assets/_Project/Scripts/NetProtocol/IapCatalog.cs`, letto sia dal client
sia dal server. I prezzi mostrati arrivano da Google gia' convertiti nella valuta del
giocatore: quelli in tabella sono solo il segnaposto usato finche' lo store non risponde
(e su web, dove non risponde mai).

## Architettura

### Client — `Assets/_Project/Scripts/Iap/` (assembly `AccardND.Iap`)

Stessa forma di `AccardND.Ads`: una facciata statica sopra, un provider per piattaforma
sotto.

- `IapProduct` / `IapProducts` — i quattro prodotti e i loro id.
- `IIapProvider` — il contratto di uno store. Nessun metodo lancia: se lo store tace, il
  gioco resta giocabile e semplicemente non si compra.
- `GooglePlayIapProvider` — Unity IAP 5 (`StoreController`, `PendingOrder`,
  `order.Info.Receipt`). Tiene gli ordini pendenti e li conferma su richiesta.
- `UnavailableIapProvider` — web, PWA, editor. Le tile restano visibili con "solo nell'app
  Android": la vetrina serve, il pulsante no.
- `IapService` — la facciata. **Non decide cosa possiede il giocatore**: tiene solo
  l'ultima risposta del server (`ApplyEntitlements`) per disegnare la UI e spegnere la
  pubblicita'.

### Protocollo — `NetProtocol/IapMessages.cs`

- `iap.get` → `iap.data` (`IapEntitlementsData`): cosa possiede l'account.
- `iap.redeem` → `iap.redeem.result`: manda una ricevuta, torna l'esito e gli entitlement.

`iap.redeem` e' nella lista dei messaggi deduplicati e viaggia sull'outbox persistente: il
giocatore ha gia' pagato, quindi la richiesta va rinviata finche' non passa.

### Server — `Server/AccardND.Server/Progression/`

- `GooglePlayReceiptVerifier` — apre la ricevuta unificata di Unity IAP
  (`Store` / `Payload` → `json` + `signature`) e verifica la firma RSA con la chiave
  pubblica dell'app. Controlla anche package name, prodotto conosciuto e `purchaseState == 0`.
- `IapPurchaseService` — registra l'acquisto e applica gli sblocchi.

Due scelte reggono il resto:

1. **Il token dell'acquisto e' la chiave primaria** di `player_purchases`. La stessa
   ricevuta rinviata dieci volte concede una volta sola, e la stessa ricevuta presentata da
   un secondo account viene rifiutata invece di sbloccare due giocatori con un acquisto.
2. **Gli sblocchi si riapplicano a ogni lettura degli entitlement**, non solo al momento
   dell'acquisto. Chi ha comprato "classi + supreme" ha comprato anche le supreme che non
   esistevano ancora: quando ne arriva una nuova a catalogo, il primo accesso successivo
   gliela mette in mano senza bisogno di una migrazione.

## Il giro completo di un acquisto

1. Il giocatore tocca la tile → `IapService.PurchaseAsync`.
2. Google incassa → `OnPurchasePending` con la ricevuta.
3. Il client manda `iap.redeem` al nostro server.
4. Il server verifica la firma, scrive la riga, concede gli sblocchi, risponde.
5. Solo adesso il client chiama `ConfirmPurchase` (`IapService.Confirm`).
6. Il client ricarica progressione e Santuario: le classi sono gia' li'.

Se il passo 3 o 4 falliscono (offline, server giu'), **non si conferma**: l'ordine resta
pendente e riparte da solo. Il ripristino (`SyncEntitlementsAsync`) gira a ogni aggancio al
server e ripresenta le ricevute che lo store conosce e il server no — e' anche il cambio di
dispositivo.

## No-ads

`AdService.AdsRemoved` entra in `RewardsWaivedWithoutAds`, la stessa strada gia' battuta sul
web quando un annuncio non arriva:

- gli interstitial non partono;
- i cancelli (miele delle quest, premio di giornata, EXP tripla) **concedono lo stesso**,
  con un id impressione marcato `waived-`;
- `Warm` non chiede piu' inventario ad AdMob.

Chi paga compra il tempo che avrebbe speso a guardare la pubblicita', non una progressione
piu' lenta.

## Cosa manca / limiti noti

- **Rimborsi e revoche non si vedono.** La verifica offline dice "questa ricevuta l'ha
  emessa Google per questa app" e nient'altro. Per accorgersi di un rimborso serve la Play
  Developer API (`purchases.products.get`), che chiede un service account Google Cloud. Fino
  ad allora un acquisto rimborsato resta sblockato.
- **Nessun `SetObfuscatedAccountId`.** Legare l'acquisto all'account gia' al momento del
  pagamento aiuterebbe l'antifrode; oggi il legame lo fa la riga sul nostro database.
- **Niente pannello admin per gli entitlement.** Per un tester si puo' inserire a mano una
  riga in `player_purchases` con un `purchase_token` finto.

## Configurazione

### Server

```
ACCARDND_PLAY_LICENSE_KEY=<chiave pubblica RSA in base64, da Play Console>
```

Play Console → **Monetizza con Play → Configurazione monetizzazione → Licenza**: e' la
stringa base64 lunga. Senza chiave il server rifiuta ogni riscatto (`store_off`): meglio un
negozio che non concede niente che uno che regala classi a chiunque mandi un JSON.

Il package name atteso e' in `serverconfig.json` (`GooglePlay.PackageName`), default
`com.apesolution.accardndie`.

### Unity

- Pacchetto `com.unity.purchasing` (5.4.2).
- Store Android = Google Play (default).
- Il package name della build deve coincidere con quello di Play Console.

### Play Console

1. App creata, profilo pagamenti completato.
2. Almeno una `.aab` caricata su un canale di test.
3. I quattro prodotti creati come **prodotti una tantum**, attivi, con gli id esatti della
   tabella qui sopra.
4. Tester: **Impostazioni → Test delle licenze** (per non essere addebitati) **e** iscritti
   al canale di test. Chi sta nel canale ma non tra i license tester paga davvero.

## Prove da fare sul dispositivo

- acquisto riuscito → la classe compare al Santuario;
- acquisto annullato → nessun messaggio d'errore, il negozio resta com'era;
- gioco chiuso tra il pagamento e la conferma → al riavvio lo sblocco arriva da solo;
- disinstalla e reinstalla → il ripristino ridA' tutto senza pagare;
- stesso acquisto su un secondo account → rifiutato;
- con `no_ads` attivo: nessun interstitial, e le quest della taverna pagano lo stesso.
