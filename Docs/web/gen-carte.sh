#!/usr/bin/env bash
# Genera Docs/web/carte.html leggendo gli asset delle carte, cosi' la pagina non puo'
# divergere dal gioco. Da rilanciare quando si aggiungono o cambiano carte.
set -euo pipefail

ROOT="C:/Users/accar/AccardND/AccardND"
SRC="$ROOT/Assets/_Project/Data/Cards/Monster"
OUT="$ROOT/Docs/web/carte.html"

field() { sed -n "s/^  $2: //p" "$1"; }

# Nome italiano della creatura a partire dal displayName dell'asset.
creature() {
  case "$1" in
    goblin) echo "Goblin" ;;
    skeleton) echo "Scheletro" ;;
    animal) echo "Bestia" ;;
    darkelf) echo "Elfo oscuro" ;;
    chimera) echo "Chimera" ;;
    whitealien) echo "Alieno bianco" ;;
    alien) echo "Alieno" ;;
    spirit) echo "Spirito" ;;
    faceless) echo "Senza volto" ;;
    champion) echo "Campione" ;;
    *) echo "$1" ;;
  esac
}

total=$(ls "$SRC"/*.asset | wc -l | tr -d ' ')

cat > "$OUT" <<'HEAD'
<!DOCTYPE html>
<html lang="it">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Database carte — Accard N' Die</title>
<meta name="description" content="Tutte le carte schierabili di Accard N' Die: nove classi per nove valori, dal Goblin da 2 al Campione da 10, con la creatura di ogni valore.">
<link rel="stylesheet" href="site.css">
<link rel="apple-touch-icon" href="media/apple-touch-icon.png">
<script async src="https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=ca-pub-3580486749764055" crossorigin="anonymous"></script>
<script src="site.js" defer></script>
</head>
<body>

<nav class="nav"><div class="wrap">
  <a class="brand" href="/">Accard N' Die</a>
  <button class="menu-toggle" type="button" aria-expanded="false" aria-controls="menu-links" hidden>Menu</button>
  <div class="menu-links" id="menu-links">
    <a href="guida.html">Come si gioca</a>
    <a href="classi.html">Le nove classi</a>
    <a href="carte.html" aria-current="page">Database carte</a>
    <a href="/statistiche">Statistiche</a>
    <a href="privacy.html">Privacy</a>
  </div>
  <a class="nav-play" data-play data-play-label="Scarica" href="/game/">Gioca</a>
</div></nav>

<header class="band band-testata">
  <div class="wrap">
    <h1>Database carte</h1>
HEAD

cat >> "$OUT" <<LEAD
    <p class="lead">Tutte le <strong>$total carte schierabili</strong> del gioco: nove classi,
    nove valori ciascuna, dal 2 al 10. Questa pagina e' generata dai dati veri del gioco, quindi
    quello che leggi qui e' quello che trovi in partita.</p>
LEAD

cat >> "$OUT" <<'INTRO'
  </div>
</header>

<main class="wrap">

<p>La collezione e' una griglia piena, senza buchi e senza rarita': per ogni
<a href="classi.html">classe</a> esiste esattamente una carta di ogni valore. Quello che
cambia fra una carta da 2 e una da 10 e' la Potenza di base; quello che cambia fra due carte
dello stesso valore e' la classe, quindi l'abilita e la famiglia.</p>

<h2>La creatura dice il valore</h2>

<p>Il disegno non e' decorativo: ogni valore corrisponde a una creatura precisa, uguale in
tutte e nove le classi. Riconoscere la creatura significa sapere il numero senza leggerlo.</p>

<table>
  <caption>La scala delle creature, dal valore piu' basso al piu' alto.</caption>
  <tr><th>Valore</th><th>Creatura</th></tr>
  <tr><td>2</td><td>Goblin</td></tr>
  <tr><td>3</td><td>Scheletro</td></tr>
  <tr><td>4</td><td>Bestia</td></tr>
  <tr><td>5</td><td>Elfo oscuro</td></tr>
  <tr><td>6</td><td>Chimera</td></tr>
  <tr><td>7</td><td>Alieno bianco</td></tr>
  <tr><td>8</td><td>Spirito</td></tr>
  <tr><td>9</td><td>Senza volto</td></tr>
  <tr><td>10</td><td>Campione</td></tr>
</table>

<p>L'unica eccezione della collezione e' l'Alieno del Necromancer, che al valore 7 porta un
nome diverso dagli altri otto.</p>

<h2>Le carte, classe per classe</h2>

<p>Fra parentesi c'e' l'identificativo interno della carta, quello che compare negli asset del
gioco: serve se stai confrontando questa lista con una schermata di collezione.</p>
INTRO

# Sezioni per famiglia, con le classi nello stesso ordine di classi.html.
emit_family() {
  local famLabel="$1" famSlug="$2"; shift 2
  {
    echo
    echo "<h3><span class=\"fam fam-$famSlug\">$famLabel</span></h3>"
    echo '<div class="cards">'
  } >> "$OUT"

  local class
  for class in "$@"; do
    local slug
    slug=$(echo "$class" | tr '[:upper:]' '[:lower:]')
    {
      echo '  <div class="card">'
      # Lo stemma e' quello del gioco (Docs/web/media/classi/, ritagliato
      # dall'atlante del costruttore di mazzi): il nome della classe e' scritto
      # accanto, quindi alt resta vuoto e non lo si fa leggere due volte.
      echo "    <h3 class=\"classhead\"><img class=\"emblem\" src=\"media/classi/$slug.png\" alt=\"\" width=\"192\" height=\"192\" loading=\"lazy\"><span>$class</span> <span class=\"badge fam-$famSlug\">$famLabel</span></h3>"
      echo '    <ul>'
    } >> "$OUT"

    # Ordinate per valore crescente, e l'ordinamento dev'essere numerico: sui nomi
    # file "10-champion" verrebbe prima di "2-goblin".
    local f id dn st
    for f in "$SRC"/*-"$slug".asset; do
      printf '%s\t%s\n' "$(field "$f" strength)" "$f"
    done | sort -n | cut -f2- | while read -r f; do
      id=$(field "$f" id)
      dn=$(field "$f" displayName)
      st=$(field "$f" strength)
      printf '      <li><strong>%s</strong> — %s <code>%s</code></li>\n' \
        "$st" "$(creature "$dn")" "$id" >> "$OUT"
    done

    {
      echo '    </ul>'
      echo '  </div>'
    } >> "$OUT"
  done

  echo '</div>' >> "$OUT"
}

emit_family "Might" "might" Warrior Barbarian Paladin
emit_family "Cunning" "cunning" Rogue Assassin Hunter
emit_family "Magic" "magic" Mage Necromancer Priest

cat >> "$OUT" <<'FOOT'

<h2>Come si legge una carta in partita</h2>

<p>Il valore e' la base della Potenza, ma non e' il totale: sopra ci vanno il tiro del Vigore,
i bonus dell'abilita e quelli dell'aura. Per questo una carta da 4 dentro uno schieramento
coerente batte regolarmente una da 8 lasciata da sola — il conto lo trovi in
<a href="guida.html">Come si gioca</a>.</p>

</main>

<footer class="site"><div class="wrap">
  <p><a href="/game/">Gioca</a> · <a href="guida.html">Come si gioca</a> ·
     <a href="classi.html">Le nove classi</a> · <a href="/statistiche">Statistiche</a> ·
     <a href="privacy.html">Privacy</a></p>
  <p>Accard N' Die</p>
</div></footer>

</body>
</html>
FOOT

echo "scritto $OUT ($total carte)"
