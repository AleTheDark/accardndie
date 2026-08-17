#!/usr/bin/env python3
"""Genera Docs/web/carte.html dagli asset delle carte.

Sostituisce gen-carte.sh, che emetteva nove liste di testo identiche - la stessa
scaletta di valori ripetuta per classe, senza una riga che spiegasse cosa
cambiasse fra una classe e l'altra. Qui la pagina mostra le illustrazioni vere e
per ogni classe dice a cosa servono le sue carte alte e le sue carte basse, che e'
l'unica informazione che un giocatore cerca in un database.

Da rilanciare quando si aggiungono o cambiano carte:

    python Docs/web/gen-media.py     # prima le immagini
    python Docs/web/gen-carte.py     # poi la pagina

I numeri e i nomi vengono letti dagli asset in Assets/_Project/Data/Cards/Monster,
quindi la pagina non puo' divergere dal gioco. L'unica cosa scritta a mano qui
dentro sono i commenti di lettura per classe (COMMENTI).

Nota: qui c'era anche una tabella con i pesi di estrazione dei valori, letti da
GameConfiguration.deckBuilding.strengthWeights. E' stata tolta su indicazione del
committente: quei pesi non vanno pubblicati. Se un domani si volesse rimetterla,
serve prima verificare che i pesi nel config siano ancora quelli che il gioco usa.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CARDS = ROOT / "Assets" / "_Project" / "Data" / "Cards" / "Monster"
WEB = Path(__file__).resolve().parent
OUT = WEB / "carte.html"
ART = WEB / "media" / "carte"

# Nome italiano della creatura, dal displayName dell'asset.
CREATURES = {
    "goblin": "Goblin",
    "skeleton": "Scheletro",
    "animal": "Bestia",
    "darkelf": "Elfo oscuro",
    "chimera": "Chimera",
    "whitealien": "Alieno bianco",
    "alien": "Alieno",
    "spirit": "Spirito",
    "faceless": "Senza volto",
    "champion": "Campione",
}

# Le fazioni, con le classi nello stesso ordine di classi.html.
FAMILIES = [
    ("Might", "might", ["warrior", "barbarian", "paladin"]),
    ("Cunning", "cunning", ["rogue", "assassin", "hunter"]),
    ("Magic", "magic", ["mage", "necromancer", "priest"]),
]

CLASS_NAMES = {
    "warrior": "Warrior", "barbarian": "Barbarian", "paladin": "Paladin",
    "rogue": "Rogue", "assassin": "Assassin", "hunter": "Hunter",
    "mage": "Mage", "necromancer": "Necromancer", "priest": "Priest",
}

# Per ogni classe: a cosa servono le sue carte alte e le sue carte basse. Non
# ripetono l'abilita' - quella sta in classi.html - dicono quanto pesa il valore
# per quella classe, che e' la domanda vera quando decidi campione e vice e quando
# ti trovi davanti al banco del mercante.
COMMENTI = {
    "warrior": (
        "<strong>Le sue carte basse valgono piu' di quanto dice il numero.</strong> "
        "L'aura da tre Warrior regala +2 quando la Potenza del Warrior e' <em>inferiore</em> "
        "a quella dell'avversario: e' l'unica aura del gioco che paga per essere la carta "
        "piu' debole del confronto. Un 4 dentro un monoclasse Warrior arriva quasi sempre "
        "con quel +2 addosso, mentre un 10 lo vede raramente."),
    "barbarian": (
        "<strong>La classe in cui il valore conta meno di tutte.</strong> La Furia si accumula "
        "sugli scambi persi e non ha tetto, quindi un Barbarian da 3 che resta in piedi due "
        "turni diventa piu' grosso di un 8 qualsiasi. Sono le carte da 12 oro piu' redditizie "
        "del mercante: un Barbarian basso capitato a caso non e' un ripiego."),
    "paladin": (
        "<strong>Qui invece il valore conta, e molto.</strong> Il Paladin fa il suo mestiere "
        "solo se sopravvive alla parata: uno che cade mentre protegge un alleato non ha "
        "protetto nessuno, e con l'aura non contrattacca. Un Paladin alto non si ordina: o e' "
        "lui il campione o il vice, o lo si compra dal mercante."),
    "rogue": (
        "<strong>Indifferente al valore, per costruzione.</strong> La soglia del rilancio "
        "dipende dal dado Vigore e non dalla carta — 1 su D4, 6 su D20 — quindi un Rogue da 2 "
        "ha esattamente la stessa rete di sicurezza di un 10. Con il Barbarian e' la classe "
        "che regge meglio un mazzo uscito male."),
    "assassin": (
        "<strong>Non combatte col numero: toglie un turno.</strong> Un'inibizione vale lo "
        "stesso che arrivi da un 3 o da un 9, e servono solo 3 mana. Il valore serve a una "
        "cosa sola, restare in campo abbastanza per rifarla ogni turno: un Assassin medio che "
        "sopravvive rende piu' di un Assassin alto che si prende il primo attacco."),
    "hunter": (
        "<strong>Il bonus lo regala a qualcun altro.</strong> Il marchio da' +2 (+4 con l'aura) "
        "a chi attacca dopo, quindi la Potenza dell'Hunter e' la parte meno importante di quello "
        "che porta. Un 2 di Hunter e' uno degli acquisti migliori del mercante: costa la fascia "
        "piu' bassa e fa lo stesso lavoro del campione."),
    "mage": (
        "<strong>Abbassa il dado, e questo non dipende dal suo numero.</strong> Ogni marchio "
        "e' uno scalino in giu' per il nemico, e si accumulano fino al D2. L'aura da tre Mage "
        "scatta <em>morendo</em>, quindi un Mage economico che cade facendo -2 permanente a "
        "chi lo ha ucciso ha fatto il suo lavoro per intero."),
    "necromancer": (
        "<strong>Vuole essere alto, per due motivi.</strong> Costa 4 mana, la seconda abilita' "
        "piu' cara, quindi deve restare in campo per giustificarli; e quello che riporta "
        "indietro e' una carta che avevi gia' perso, cioe' un vantaggio che matura tardi. Un "
        "Necromancer basso muore prima di rialzare qualcuno."),
    "priest": (
        "<strong>Come il Paladin: un supporto deve sopravvivere.</strong> La benedizione "
        "toglie tutti i malus a un alleato e gli da' +2 (+3 con l'aura), e le benedizioni si "
        "sommano — ma solo se il Priest e' ancora li' il turno dopo. Come il Paladin, e' una "
        "classe che rende molto piu' da campione o da vice che dalle sette carte capitate."),
}


def read_cards():
    """Le 81 carte dagli asset: (classe, valore, creatura, id)."""
    field = lambda text, name: re.search(rf"^  {name}: (.*)$", text, re.M)
    cards = {}
    for asset in sorted(CARDS.glob("*.asset")):
        text = asset.read_text(encoding="utf-8", errors="replace")
        card_id = field(text, "id")
        display = field(text, "displayName")
        strength = field(text, "strength")
        if not (card_id and display and strength):
            print(f"salto {asset.name}: campi mancanti", file=sys.stderr)
            continue

        card_id = card_id.group(1).strip()
        # L'id e' "<valore>-<creatura>-<classe>": la classe e' l'ultimo pezzo.
        hero_class = card_id.rsplit("-", 1)[-1]
        cards.setdefault(hero_class, []).append((
            int(strength.group(1)),
            CREATURES.get(display.group(1).strip(), display.group(1).strip()),
            card_id,
        ))

    for entries in cards.values():
        entries.sort()
    return cards


def art_size(card_id):
    """Le misure vere del WebP, per scriverle nell'HTML.

    Servono a evitare che la pagina salti mentre le immagini arrivano: senza
    width/height il browser non sa quanto spazio riservare, e con novanta
    illustrazioni in lazy load il salto si vede tutto. Le miniature non sono
    quadrate (gli originali sono 768-819 x 1024) e nemmeno tutte uguali fra loro,
    quindi il valore si legge dal file invece di darlo per scontato.
    """
    art = ART / f"{card_id}.webp"
    if not art.is_dir() and art.exists():
        try:
            from PIL import Image
            with Image.open(art) as image:
                return image.size
        except Exception as error:  # Pillow assente o file illeggibile
            print(f"non leggo le misure di {card_id} ({error})", file=sys.stderr)
    else:
        print(f"manca l'immagine di {card_id}: lancia gen-media.py", file=sys.stderr)
    return 240, 320


def card_figure(value, creature, card_id, class_name, lazy=True):
    """Una miniatura con il valore in evidenza e l'id sotto."""
    width, height = art_size(card_id)
    loading = ' loading="lazy"' if lazy else ""
    return (
        f'    <figure class="cardart">\n'
        f'      <img src="media/carte/{card_id}.webp" width="{width}" height="{height}"'
        f'{loading} alt="{creature} {class_name}, valore {value}">\n'
        f'      <figcaption><b>{value}</b> {creature}<code>{card_id}</code></figcaption>\n'
        f'    </figure>\n'
    )


HEAD = """<!DOCTYPE html>
<!-- GENERATA DA gen-carte.py — non modificare a mano: il prossimo rilancio
     dello script sovrascrive tutto. Il testo per classe sta nel dizionario
     COMMENTI dentro lo script, la nav e il piede nelle costanti HEAD/FOOT. -->
<html lang="it">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Database carte — AcCard N' Die</title>
<meta name="description" content="Tutte le 81 carte di AcCard N' Die con le illustrazioni del gioco: nove classi per nove valori, la creatura di ogni valore e quanto pesa il valore per ciascuna classe.">
<link rel="canonical" href="https://accardndie.com/carte">
<link rel="stylesheet" href="site.css?v=8019aee5">
<link rel="apple-touch-icon" href="media/apple-touch-icon.png">
<meta name="theme-color" content="#14100c">
<script async src="https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=ca-pub-3580486749764055" crossorigin="anonymous"></script>
<script src="site.js?v=49ec8cb4" defer></script>
</head>
<body>

<nav class="nav"><div class="wrap">
  <a class="brand" href="/">AcCard N' Die</a>
  <button class="menu-toggle" type="button" aria-expanded="false" aria-controls="menu-links" hidden>Menu</button>
  <div class="menu-links" id="menu-links">
    <a href="/guida">Come si gioca</a>
    <a href="/strategia">Strategia</a>
    <a href="/classi">Le nove classi</a>
    <a href="/carte" aria-current="page">Database carte</a>
    <a href="/campagna">Campagna</a>
    <a href="/duelli">Duelli</a>
    <a href="/hall-of-fame">Hall of Fame</a>
  </div>
  <a class="nav-play" data-play data-play-label="Scarica" href="/game/">Gioca</a>
</div></nav>

<header class="band band-testata">
  <div class="wrap">
    <h1>Database carte</h1>
    <p class="lead">Tutte le <strong>{total} carte schierabili</strong> del gioco, con le
    illustrazioni vere: nove classi, nove valori ciascuna, dal 2 al 10. Nomi, valori e
    identificativi sono letti dai dati del gioco, quindi quello che vedi qui e' quello che
    trovi in partita.</p>
  </div>
</header>

<main class="wrap wide">

<p>La collezione e' una griglia piena, senza buchi e senza rarita': per ogni
<a href="/classi">classe</a> esiste esattamente una carta di ogni valore. Quello che
cambia fra una carta da 2 e una da 10 e' la Potenza di base; quello che cambia fra due carte
dello stesso valore e' la classe, quindi l'abilita e la fazione.</p>

<div class="box">
  <p>Le carte non si collezionano e non si possiedono: non ci sono bustine da aprire, niente
  da scambiare e nessuna carta in vendita, ne' qui ne' altrove. Tutte e {total} sono sul tavolo
  dal primo giorno, uguali per tutti. Quello che cambia da una partita all'altra e' il mazzo,
  che si rifa da zero a ogni run: come funziona sta in
  <a href="/guida">Come si gioca</a>. Le cose che si sbloccano e restano sono altre — le
  <a href="/classi">classi</a> e le loro supreme — e si prendono al
  <a href="/rifugio">Santuario</a>.</p>
</div>

<h2>La creatura dice il valore</h2>

<p>Il disegno non e' decorativo: ogni valore corrisponde a una creatura precisa, la stessa in
tutte e nove le classi. Riconoscere la creatura significa sapere il numero senza leggerlo, ed
e' il modo in cui si legge il campo quando le pedine sono sei e il turno e' tuo.</p>

<div class="cardstrip creaturescale">
{creature_scale}</div>

<p>Le nove creature qui sopra sono le carte del <strong>Warrior</strong>, prese come
riferimento. La stessa scala vale identica per le altre otto classi: cambia lo stile
dell'illustrazione, non la creatura. L'unica eccezione della collezione e' l'Alieno del
Necromancer, che al valore 7 porta un nome diverso dagli altri otto.</p>

<h2>Le carte, classe per classe</h2>

<p>Sotto ogni classe c'e' la sua fila completa dal 2 al 10, e una riga su quanto pesa il
valore per <em>quella</em> classe: non e' la stessa risposta per tutte e nove. Serve a due
decisioni concrete — chi mettere come campione e vice, e cosa comprare dal mercante quando l'oro
basta per una carta sola.</p>
"""

FOOT = """
<h2>Come si legge una carta in partita</h2>

<p>Il valore e' la base della Potenza, ma non e' il totale: sopra ci vanno il tiro del Vigore,
i bonus dell'abilita e quelli dell'aura. Per questo una carta da 4 dentro uno schieramento
coerente batte regolarmente una da 8 lasciata da sola — il conto lo trovi in
<a href="/guida">Come si gioca</a>, e i casi in cui conviene davvero in
<a href="/strategia">Strategia</a>.</p>

<p>La scritta piccola sotto ogni illustrazione — <code>7-whitealien-priest</code> e le altre —
non e' niente da imparare a memoria: e' l'identificativo interno dell'asset, e serve solo se
stai confrontando questa pagina con la Biblioteca dentro il <a href="/rifugio">rifugio</a>.</p>

</main>

<footer class="site"><div class="wrap">
  <p><a href="/game/">Gioca</a> · <a href="/guida">Come si gioca</a> ·
     <a href="/strategia">Strategia</a> · <a href="/classi">Le nove classi</a> ·
     <a href="/campagna">Campagna</a> · <a href="/duelli">Duelli</a> ·
     <a href="/rifugio">Il rifugio</a> · <a href="/faq">Domande frequenti</a> ·
     <a href="/hall-of-fame">Hall of Fame</a> · <a href="/statistiche">Statistiche</a></p>
  <!-- Le tre righe qui sotto sono identiche in tutte le pagine statiche e in
       SiteLayout.Footer() sul server: se cambia una voce, cambiarla in tutti i posti. -->
  <p><a href="/chi-siamo">Chi siamo</a> · <a href="/contatti">Contatti</a> ·
     <a href="/privacy">Privacy</a> · <a href="/account/delete">Cancellazione account</a></p>
  <p><a href="https://www.youtube.com/@accardndie" rel="noopener">YouTube</a> ·
     <a href="https://www.instagram.com/accardndie/" rel="noopener">Instagram</a> ·
     <a href="https://www.tiktok.com/@accardndie" rel="noopener">TikTok</a></p>
  <p>AcCard N' Die</p>
</div></footer>

</body>
</html>
"""


def main():
    if not CARDS.is_dir():
        sys.exit(f"non trovo gli asset delle carte in {CARDS}")

    cards = read_cards()
    total = sum(len(entries) for entries in cards.values())
    if total == 0:
        sys.exit("nessuna carta letta")

    # La scala delle creature usa il Warrior: e' la classe che tutti hanno dal
    # tutorial, quindi le sue nove carte sono quelle che il lettore ha visto.
    scale = "".join(
        card_figure(value, creature, card_id, "Warrior", lazy=False)
        for value, creature, card_id in cards["warrior"]
    )

    out = [HEAD.format(total=total, creature_scale=scale)]

    for family_label, family_slug, class_ids in FAMILIES:
        out.append(f'\n<h3><span class="fam fam-{family_slug}">{family_label}</span></h3>\n')
        for class_id in class_ids:
            entries = cards.get(class_id)
            if not entries:
                print(f"nessuna carta per {class_id}", file=sys.stderr)
                continue

            name = CLASS_NAMES[class_id]
            out.append(
                f'\n<div class="classblock">\n'
                f'  <h4 class="classhead">'
                f'<img class="emblem" src="media/classi/{class_id}.png" alt=""'
                f' width="192" height="192" loading="lazy">'
                f'<span>{name}</span> '
                f'<span class="badge fam-{family_slug}">{family_label}</span></h4>\n'
                f'  <p>{COMMENTI[class_id]}</p>\n'
                f'  <div class="cardstrip">\n'
            )
            out.extend(
                card_figure(value, creature, card_id, name)
                for value, creature, card_id in entries
            )
            out.append('  </div>\n</div>\n')

    out.append(FOOT)
    OUT.write_text("".join(out), encoding="utf-8")
    print(f"scritto {OUT.relative_to(ROOT)} ({total} carte)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
