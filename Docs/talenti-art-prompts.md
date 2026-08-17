# Talenti: prompt per la generazione degli asset

Prompt pronti da incollare in un generatore di immagini (Midjourney, DALL·E, Imagen,
Flux, Stable Diffusion). Accompagnano `Docs/talenti-design.md`.

I prompt sono **in inglese**: tutti i modelli di generazione sono addestrati in
prevalenza su didascalie inglesi e rendono nettamente meglio. Le note attorno restano in
italiano.

## Dove vanno i file

**Tutto in `Assets/Resources/UI/ProfileTalents/`**, con i nomi esatti della tabella qui
sotto. I nomi non sono liberi: `LoadSpriteResource` li cerca uno per uno, e un file giusto
col nome sbagliato per la schermata non esiste.

### Gia' in progetto e in uso (9)

`branch_purse`, `branch_initiative`, `branch_mastery`, `branch_occasions`,
`propolis_currency`, `talent_hive_lattice`, `talent_node_locked`, `talent_node_available`,
`talent_node_maximum`.

I prompt §1, §3 e §4 servono solo se vuoi rifarli meglio: in quel caso **tieni i nomi che
hanno gia'**, non quelli scritti nei titoli dei paragrafi.

### Da generare (20)

| File | Prompt |
| --- | --- |
| `talents_background` | §2 |
| `talent_icon_purse_travel_fund` | §5 |
| `talent_icon_purse_generous_forge` | §5 |
| `talent_icon_purse_kind_merchant` | §5 |
| `talent_icon_purse_smith_temper` | §5 |
| `talent_icon_purse_first_deal` | §5 |
| `talent_icon_initiative_vanguard` | §6 |
| `talent_icon_initiative_flanker` | §6 |
| `talent_icon_initiative_rearguard` | §6 |
| `talent_icon_initiative_first_strike` | §6 |
| `talent_icon_mastery_apprentice` | §7 |
| `talent_icon_mastery_momentum` | §7 |
| `talent_icon_mastery_veteran` | §7 |
| `talent_icon_mastery_summit` | §7 |
| `talent_icon_occasion_recovery` | §8 |
| `talent_icon_occasion_challenger` | §8 |
| `talent_icon_occasion_seeker` | §8 |
| `talent_icon_occasion_second_wind` | §8 |
| `talents_title_plaque` | §9 |
| `talents_points_badge` | §9 |

Il nome dell'icona e' l'id del talento nel `TalentCatalog` con i trattini trasformati in
underscore: `purse-travel-fund` → `talent_icon_purse_travel_fund`. Cosi' la schermata potra'
comporre il percorso dall'id invece di tenere una seconda tabella allineata a mano.

Finche' le icone non ci sono, le celle del favo mostrano nome, rango e prezzo su cornice:
la schermata funziona, e' solo piu' spoglia.

## Come usarli

1. **Genera prima la cornice esagonale** (§3). E' l'asset che detta lo stile a tutti gli
   altri: una volta che ti piace, usala come immagine di riferimento (`--sref` su
   Midjourney, image prompt altrove) per tutte le icone dei nodi, altrimenti i 17 nodi
   escono da 17 stili diversi.
2. **Fissa il seed** dopo la prima icona riuscita e cambia solo il soggetto. E' l'unico
   modo pratico per avere un set coerente.
3. **Niente testo nell'immagine.** Ogni prompt lo esclude: i modelli scrivono lettere
   storpiate e vanno ridisegnate a mano. Nomi e numeri li mette la UI.
4. **Alpha.** Quasi nessun generatore produce trasparenza vera. I prompt chiedono sfondo
   nero piatto, che si scontorna facilmente; per le icone su cella esagonale spesso non
   serve nemmeno, perche' la cornice fa da bordo.
5. **Leggibilita' a 64px.** Le icone dei nodi in un favo si vedono piccole. Dopo la
   generazione rimpiccioliscile e guardale: se non capisci cos'e', il soggetto e' troppo
   affollato, non serve un altro giro di prompt ma un soggetto piu' semplice.

## Palette di riferimento

Dai valori veri di `MmoUiTheme.cs`, gia' convertiti in esadecimale:

| Ruolo | Hex |
| --- | --- |
| Inchiostro (fondo) | `#05070A` |
| Pannello | `#070E16` |
| Pannello chiaro | `#11202B` |
| Oro (accento primario) | `#FFC25C` |
| Rame (accento caldo) | `#BD612E` |
| Arcano (ciano freddo) | `#26D1F2` |
| Viola | `#8F57EB` |
| Verde | `#6BE673` |
| Rosso | `#F2473D` |
| Testo tenue | `#B3D4E6` |

## Preambolo di stile

Da anteporre a **ogni** prompt di icona. Chiamato `[STILE]` piu' sotto.

```
dark fantasy MMO game UI icon, hand-painted semi-realistic illustration, single centered
subject, clean readable silhouette, dramatic chiaroscuro lighting, warm gold key light
(#FFC25C), cool cyan arcane rim light (#26D1F2), copper accents (#BD612E), deep navy-black
background (#05070A), subtle gold filigree detailing, honeycomb and beeswax motifs where
natural, ornate but not cluttered, crisp edges, no text, no letters, no numbers, no
signature, no watermark, no border frame, square 1:1 composition
```

Negativi (dove il generatore li accetta separatamente):

```
text, letters, numbers, words, watermark, signature, UI frame, drop shadow on background,
photorealistic, 3d render, cartoon, chibi, cluttered composition, multiple subjects,
low contrast, pastel colors
```

---

## 1. Valuta: propoli

**File:** `Assets/Resources/UI/Talents/propolis_icon.png` — 512×512

```
[STILE] a single glowing drop of amber bee propolis resin, suspended and faceted like a
gemstone, warm orange-amber core with golden inner light, tiny hexagonal honeycomb pattern
visible inside the resin, thin gold rim, faint cyan arcane sparks orbiting it, floating
above pure black
```

Nota: deve leggersi **diverso dal miele** a colpo d'occhio, perche' sono due valute che
convivono nella stessa barra in alto. Il miele e' liquido e dorato: la propoli qui e'
solida, ambrata-rossastra e sfaccettata. Se in test si confondono, spingi la propoli verso
il rosso-rame e togli ogni colatura.

## 2. Sfondo del pannello albero

**File:** `Assets/Resources/UI/Talents/talents_background.png` — 2048×1152 (landscape)

```
dark fantasy MMO interface background, vast honeycomb wall of an ancient stone beehive
temple, hexagonal wax cells receding into darkness, dim golden light pooling from above,
drifting motes of pollen and dust, deep navy-black tones (#05070A, #070E16), warm gold
accents (#FFC25C), subtle cyan arcane glow in the depths (#26D1F2), heavy vignette, empty
uncluttered center for UI overlay, atmospheric, painterly, no text, no characters, no
watermark, ultra wide composition
```

Nota: il centro deve restare **vuoto e scuro**. Se il generatore ci mette il soggetto
principale, aggiungi "composition with empty dark center, all detail pushed to the edges".

## 3. Cornice esagonale del nodo — tre stati

**File:** `talent_node_frame_locked.png`, `_available.png`, `_maxed.png` — 256×256

Genera i tre stati con lo **stesso seed** cambiando solo le ultime righe, altrimenti gli
esagoni non si sovrappongono e il favo balla.

**Bloccato**

```
[STILE] empty hexagonal frame for a game UI node, thick weathered dark bronze border with
fine engraved filigree, hollow center showing flat pure black, cold desaturated metal, no
glow, dormant and sealed, isolated on pure black, symmetrical, front view, flat orthographic
```

**Disponibile**

```
[STILE] empty hexagonal frame for a game UI node, thick polished gold border (#FFC25C) with
fine engraved filigree, hollow center showing flat pure black, warm golden outer glow, a
few cyan arcane sparks along the rim (#26D1F2), inviting and active, isolated on pure black,
symmetrical, front view, flat orthographic
```

**Al massimo**

```
[STILE] empty hexagonal frame for a game UI node, radiant gold border (#FFC25C) with
filigree filled by molten light, thin cyan arcane energy running through the engravings
(#26D1F2), small crown-like flourish at the top vertex, strong outer bloom, hollow center
showing flat pure black, isolated on pure black, symmetrical, front view, flat orthographic
```

## 4. Emblemi dei quattro rami

**File:** `talents_branch_<ramo>_emblem.png` — 512×512. Vanno al centro di ogni settore del
favo, sullo stampo dei `sanctuary_*_emblem_aaa.png` gia' in progetto.

**Borsa**

```
[STILE] heraldic emblem of a heavy leather coin purse bound in gold cord, spilling gold
coins and a single amber resin drop, crossed blacksmith tongs behind it, ornate gold
medallion composition, radial symmetry
```

**Iniziativa**

```
[STILE] heraldic emblem of three twenty-sided dice arranged in a rising diagonal line, the
leading die glowing with cyan arcane light (#26D1F2), thin gold speed lines behind them,
ornate gold medallion composition, radial symmetry
```

**Maestria**

```
[STILE] heraldic emblem of an ascending stair of six carved stone steps, a die on the top
step radiating golden light, laurel of beeswax and honeycomb around the base, ornate gold
medallion composition, radial symmetry
```

**Occasioni**

```
[STILE] heraldic emblem of an hourglass with a hexagonal honeycomb frame, sand frozen
mid-fall and turning into golden sparks, a small key crossed behind it, ornate gold
medallion composition, radial symmetry
```

---

## 5. Icone dei nodi — ramo Borsa

**File:** `talent_icon_<id>.png` — 512×512

**Fondo di viaggio** (+oro iniziale)

```
[STILE] a small open coin pouch at the start of a journey, three gold coins tumbling out
onto a worn stone road, a walking staff leaning behind, dawn light
```

**Forgia generosa** (+essenza iniziale)

```
[STILE] a blacksmith crucible overflowing with molten golden essence, thick liquid light
brimming over the rim without spilling, cyan arcane vapour rising, anvil edge below
```

**Mercante compiacente** (−costi mercante)

```
[STILE] a merchant's brass balance scale tipped generously to one side, gold coins on the
light pan, a honeycomb weight on the heavy pan, warm lantern light
```

**Tempra del fabbro** (carta +1 forza a fine forgia)

```
[STILE] a glowing hammer striking a playing card laid on an anvil, the card edge flaring
white-hot gold, sparks bursting upward, cyan arcane afterglow in the impact
```

**Primo affare** (primo upgrade gratis)

```
[STILE] a merchant's gloved hand offering a small gold-wrapped parcel, the price tag cut
and falling away, warm shop lantern glow behind, generous gesture
```

## 6. Icone dei nodi — ramo Iniziativa

**Avanguardia** (+iniziativa 1ª pedina)

```
[STILE] a single armoured chess-like game pawn charging forward at the head of a formation,
a twenty-sided die glowing gold at its feet, cyan motion streaks behind it, the other two
pawns dim in the background
```

**Fiancheggiatore** (+iniziativa 2ª pedina)

```
[STILE] the second armoured game pawn of a three-pawn formation stepping out sideways to
flank, its twenty-sided die glowing gold, the first pawn faintly lit ahead, cyan motion
streaks
```

**Retroguardia** (+iniziativa 3ª pedina)

```
[STILE] the rearmost armoured game pawn of a three-pawn formation surging forward from the
back, its twenty-sided die glowing gold, two dim pawns silhouetted ahead, cyan motion
streaks
```

**Colpo d'anticipo** (vinci i pari)

```
[STILE] two identical twenty-sided dice landing on the same face, the left one flaring with
golden light and cyan arcane energy while the right one stays dull grey stone, decisive
tie-breaking moment
```

## 7. Icone dei nodi — ramo Maestria

**Apprendista** (−soglie di livello)

```
[STILE] an open leather grimoire on a lectern, a small four-sided die resting on the page
turning into golden light, honeycomb pattern watermarked into the parchment, quiet study
lamp glow
```

**Slancio** (soglia livello 2 dimezzata)

```
[STILE] a runner's first explosive stride off a carved stone starting block, golden light
trailing from the heel, a four-sided die left spinning behind, strong forward momentum
```

**Veterano** (−soglie livelli 4 e 5)

```
[STILE] a scarred veteran's pauldron with four notched service marks carved into the metal,
worn gold trim, a ten-sided die set into the shoulder plate glowing faintly cyan
```

**Culmine** (−soglia livello 6, arriva il d20)

```
[STILE] a radiant twenty-sided die crowning the summit of a carved stone spire, brilliant
golden light bursting from its faces, cyan arcane arcs spiralling around it, clouds far
below, triumphant
```

## 8. Icone dei nodi — ramo Occasioni

**Recupero** (−costo di recupero)

```
[STILE] a cracked game pawn being mended by threads of molten gold along its fractures,
kintsugi repair aesthetic, warm restorative light in the seams
```

**Sfidante** (+vigore contro boss)

```
[STILE] a small armoured game pawn raising its blade toward an enormous horned shadow
looming over it, the pawn's twenty-sided die blazing gold, defiant scale contrast
```

**Cercatore** (+consumabili dalle stanze bottino)

```
[STILE] an opened treasure chest overflowing with glowing potion vials and sealed scroll
cases, a column of golden light rising from it, honeycomb pattern embossed on the chest lid,
drifting gold dust
```

**Secondo fiato** (primo recupero gratis)

```
[STILE] a fallen game pawn rising back upright out of a stone grave slab, golden light
lifting it, broken chain links falling away, second-chance moment
```

---

## 9. Ornamenti della schermata

**Targa del titolo** — `talents_title_plaque.png`, 1024×256

```
dark fantasy MMO UI title plaque, horizontal carved dark stone banner with heavy gold
filigree ends, two small honeycomb bosses at the corners, empty smooth center panel for
text, warm gold rim light (#FFC25C), faint cyan arcane glow (#26D1F2), deep navy-black
background, isolated on pure black, symmetrical, flat orthographic front view, no text, no
letters, no watermark
```

**Badge punti disponibili** — `talents_points_badge.png`, 256×256

```
[STILE] a small radiant amber propolis drop inside a thin gold hexagonal badge, strong
pulsing outer glow, notification pip aesthetic, extremely simple silhouette, isolated on
pure black
```

Deve funzionare a **32px** sopra l'angolo del bottone del profilo: se a quella dimensione
non e' una macchia ambrata riconoscibile, semplificalo ancora.

**Linee di collegamento tra nodi** — *nessun prompt*. Si disegnano proceduralmente:
`MmoUiTheme` genera gia' cornici e bottoni a runtime, e una linea generata da AI non si
allineerebbe mai ai centri delle celle.

---

## Riepilogo asset

| # | Asset | Dimensione | Prompt | Stato |
| --- | --- | --- | --- | --- |
| 1 | `propolis_currency` | 512² | §1 | in progetto |
| 2 | `talents_background` | 2048×1152 | §2 | da fare |
| 3-5 | cornice nodo, 3 stati | 256² | §3 | in progetto |
| 6-9 | emblemi dei 4 rami | 512² | §4 | in progetto |
| 10-14 | 5 icone ramo Borsa | 512² | §5 | da fare |
| 15-18 | 4 icone ramo Iniziativa | 512² | §6 | da fare |
| 19-22 | 4 icone ramo Maestria | 512² | §7 | da fare |
| 23-26 | 4 icone ramo Occasioni | 512² | §8 | da fare |
| 27 | `talents_title_plaque` | 1024×256 | §9 | da fare |
| 28 | `talents_points_badge` | 256² | §9 | da fare |

**28 immagini in tutto, 20 ancora da fare**, di cui 17 icone di nodo. Genera nell'ordine:
una icona di prova → fissa seed e stile sulla cornice che c'e' gia' → tutte le altre.
