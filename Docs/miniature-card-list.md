# Accard N' Die — lista miniature carte schierabili

Questa lista contiene le carte personaggio/mostro attualmente presenti in `Assets/_Project/Data/Cards/Monster`, divise per classe.

Convenzione consigliata per le miniature:

`{valore}_{nome}_{classe}.png`

Esempi:

- `2_goblin_paladin.png`
- `5_darkelf_paladin.png`
- `champion_paladin.png` per il campione/valore 10, se vogliamo un nome speciale.

## Paladin / Paladino

- Valore 2 — Goblin — asset: `2-goblin-paladin`
- Valore 3 — Skeleton — asset: `3-skeleton-tank`
- Valore 4 — Animal — asset: `4-animal-paladin`
- Valore 4 — Animal — asset: `4-animal-tank`
- Valore 5 — Dark Elf — asset: `5-darkelf-tank`
- Valore 6 — Chimera — asset: `6-chimera-tank`
- Valore 7 — White Alien — asset: `7-whitealien-tank`
- Valore 8 — Spirit — asset: `8-spirit-tank`
- Valore 9 — Faceless — asset: `9-faceless-tank`
- Valore 10 — Champion — asset: `10-champion-paladin`

## Assassin / Assassino

- Valore 2 — Goblin — asset: `2-goblin-assassin`
- Valore 3 — Skeleton — asset: `3-skeleton-assassin`
- Valore 4 — Animal — asset: `4-animal-assassin`
- Valore 6 — Chimera — asset: `6-chimera-assassin`
- Valore 9 — Faceless — asset: `9-faceless-assassin`
- Valore 10 — Champion — asset: `10-champion-assassin`

## Warrior / Guerriero

- Valore 2 — Goblin — asset: `2-goblin-warrior`
- Valore 3 — Skeleton — asset: `3-skeleton-warrior`
- Valore 4 — Animal — asset: `4-animal-warrior`
- Valore 5 — Dark Elf — asset: `5-darkelf-warrior`
- Valore 6 — Chimera — asset: `6-chimera-warrior`
- Valore 8 — Spirit — asset: `8-spirit-warrior`
- Valore 9 — Faceless — asset: `9-faceless-warrior`
- Valore 10 — Champion — asset: `10-champion-warrior`

## Mage / Mago

- Valore 2 — Goblin — asset: `2-goblin-mage`
- Valore 3 — Skeleton — asset: `3-skeleton-mage`
- Valore 4 — Animal — asset: `4-animal-mage`
- Valore 6 — Chimera — asset: `6-chimera-mage`
- Valore 9 — Faceless — asset: `9-faceless-mage`
- Valore 10 — Champion — asset: `10-champion-mage`

## Rogue / Ladro

- Valore 2 — Goblin — asset: `2-goblin-rogue`
- Valore 3 — Skeleton — asset: `3-skeleton-rogue`
- Valore 4 — Animal — asset: `4-animal-rogue`
- Valore 6 — Chimera — asset: `6-chimera-rogue`
- Valore 7 — White Alien — asset: `7-whitealien-assassin`
- Valore 10 — Champion — asset: `10-champion-rogue`

## Hunter / Cacciatore

- Valore 2 — Goblin — asset: `2-goblin-hunter`
- Valore 3 — Skeleton — asset: `3-skeleton-hunter`
- Valore 4 — Animal — asset: `4-animal-hunter`
- Valore 5 — Dark Elf — asset: `5-darkelf-assassin`
- Valore 6 — Chimera — asset: `6-chimera-hunter`
- Valore 8 — Spirit — asset: `8-spirit-assassin`
- Valore 10 — Champion — asset: `10-champion-hunter`

## Barbarian / Barbaro

- Valore 2 — Goblin — asset: `2-goblin-barbarian`
- Valore 3 — Skeleton — asset: `3-skeleton-barbarian`
- Valore 4 — Animal — asset: `4-animal-barbarian`
- Valore 6 — Chimera — asset: `6-chimera-barbarian`
- Valore 7 — White Alien — asset: `7-whitealien-warrior`
- Valore 10 — Champion — asset: `10-champion-barbarian`

## Necromancer / Negromante

- Valore 2 — Goblin — asset: `2-goblin-necromancer`
- Valore 3 — Skeleton — asset: `3-skeleton-necromancer`
- Valore 4 — Animal — asset: `4-animal-necromancer`
- Valore 6 — Chimera — asset: `6-chimera-necromancer`
- Valore 7 — White Alien — asset: `7-whitealien-mage`
- Valore 10 — Champion — asset: `10-champion-necromancer`

## Priest / Sacerdote

- Valore 2 — Goblin — asset: `2-goblin-priest`
- Valore 3 — Skeleton — asset: `3-skeleton-priest`
- Valore 4 — Animal — asset: `4-animal-priest`
- Valore 5 — Dark Elf — asset: `5-darkelf-mage`
- Valore 6 — Chimera — asset: `6-chimera-priest`
- Valore 8 — Spirit — asset: `8-spirit-mage`
- Valore 10 — Champion — asset: `10-champion-priest`

## Note di produzione

- Le carte Boss esistono nel progetto, ma al momento non hanno classe (`hasHeroClass: 0`), quindi non sono incluse in questa lista.
- Le carte Item/Oggetto esistono nel progetto, ma non vanno schierate come miniature personaggio.
- Il catalogo attuale è sbilanciato: Paladin ha 12 asset, le altre classi ne hanno 3 ciascuna.
- Se vogliamo un set completo e ordinato per tutte le classi, la struttura ideale sarebbe 9 classi x 9 valori = 81 miniature, oppure 9 classi x 10 valori = 90 miniature includendo il campione.
