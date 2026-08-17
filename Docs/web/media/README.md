# Docs/web/media — immagini e video del sito

Roba servita dalla radice del dominio come `/media/...`. Non e' materiale Unity:
sta qui e non nel template WebGL perche' cambiarla non deve richiedere una build.

## Cosa c'e' gia'

| file | dove si vede | da dove viene |
|---|---|---|
| `poster-hub.jpg`, `poster-scontro.jpg`, `poster-boss.jpg` | fotogramma fisso dei tre video della homepage | screenshot del Play Store ridotti a 540x960 |
| `social-card.jpg` | anteprima quando il link viene incollato su chat e social (`og:image`) | `StoreAssets/GooglePlay/featured-graphic-1024x500.jpg` |
| `apple-touch-icon.png` | icona di "Aggiungi a Home" delle pagine del sito | `StoreAssets/GooglePlay/app-icon-512.png` a 180x180 |
| `bg/hero.jpg`, `bg/banda-classi.jpg`, `bg/banda-inizia.jpg`, `bg/banda-testata.jpg` | le fasce a tutta larghezza (`.band` in site.css) | arte degli scenari in `Assets/_Project/Art/Scenarios/*_landscape.png`, a 1280px e JPEG qualita' 68 |
| `classi/*.png` | stemmi delle nove classi in `classi.html`, `carte.html` e homepage | ritagliati da `Assets/Resources/UI/DeckBuilder/class_icons_atlas.png` usando i rettangoli delle sprite nel suo `.meta` |
| `ranks/*.png` | emblemi delle cinque leghe accanto al grado, in `/hall-of-fame` e `/statistiche` | da `Assets/_Project/Resources/UI/MultiplayerRestyle/Ranks/rank_*_v1.png`, ritagliati sull'alfa e riportati a 128x128 |

Gli emblemi delle leghe hanno un nome per tier (`nabbo`, `apprendista`, `esperto`,
`divino`, `onnipotente`, gli stessi di `RankedConfig.Tiers`) e non il nome del file
Unity: e' `SiteLayout.RankBadge` a fare l'accoppiamento, e un tier senza file esce
come solo testo. Gli originali sono PNG da 200-400 KB con misure diverse fra loro;
la riduzione li ritaglia sul contenuto e li centra in un quadrato di 128, cosi' in
tabella si incolonnano. Il comando, con Pillow:

```
python -c "
from PIL import Image
import os
src='Assets/_Project/Resources/UI/MultiplayerRestyle/Ranks'
dst='Docs/web/media/ranks'
S=128
for n in ['nabbo','apprendista','esperto','divino','onnipotente']:
    im=Image.open(os.path.join(src,f'rank_{n}_v1.png')).convert('RGBA')
    im=im.crop(im.getbbox())
    w,h=im.size
    s=S/max(w,h)
    im=im.resize((max(1,round(w*s)),max(1,round(h*s))),Image.LANCZOS)
    c=Image.new('RGBA',(S,S),(0,0,0,0))
    c.paste(im,((S-im.size[0])//2,(S-im.size[1])//2),im)
    c.save(os.path.join(dst,f'{n}.png'),optimize=True)
"
```

Gli stemmi vanno rigenerati se cambia l'atlante. I rettangoli stanno nel `.meta`
dell'atlante, con l'origine in **basso** a sinistra: per ritagliarli con qualunque
strumento che conta dall'alto serve `top = altezzaAtlante - (y + altezza)`. Averlo
scoperto sbagliando e' il motivo per cui e' scritto qui.

Il font dei titoli non sta qui ma in `../fonts/`: e' servito da `/fonts/` ed e'
caricato da `@font-face` in cima a `site.css`.

## Cosa manca: i video della vetrina

La homepage cerca tre coppie di file che **non sono ancora in questa cartella**:

- `vetrina-campagna.webm` + `vetrina-campagna.mp4`
- `vetrina-scontro.webm` + `vetrina-scontro.mp4`
- `vetrina-boss.webm` + `vetrina-boss.mp4`

Finche' non ci sono, la pagina non si rompe: il browser mostra il poster, che e'
comunque una schermata vera del gioco. Appena i file compaiono, partono da soli.

Regole per registrarli, tutte dettate dal fatto che si comportano come GIF:

- **verticali 9:16** (es. 540x960): la griglia della homepage ha quel rapporto, un
  video orizzontale verrebbe tagliato ai lati da `object-fit: cover`;
- **muti**, senza traccia audio: un video con audio non parte in autoplay, i browser
  lo bloccano e resterebbe fermo sul poster;
- **corti**, 6-10 secondi: vanno in loop, un filmato lungo si nota che ricomincia;
- **leggeri**, sotto i 2 MB l'uno: la pagina si apre quasi sempre da telefono sotto
  rete mobile. Se serve tagliare, tagliare il bitrate prima della durata.

Il `.webm` (VP9) pesa meno ed e' quello che prendono Chrome e Firefox; il `.mp4`
(H.264) serve a Safari, che il webm non lo prende sempre. Vanno messi tutti e due:
l'ordine nei `<source>` della homepage fa scegliere il webm a chi puo'.

Conversione da una registrazione qualsiasi, con ffmpeg:

```bash
ffmpeg -i registrazione.mp4 -an -vf "scale=540:960:force_original_aspect_ratio=increase,crop=540:960" -c:v libvpx-vp9 -b:v 700k -crf 34 vetrina-scontro.webm
```

```bash
ffmpeg -i registrazione.mp4 -an -vf "scale=540:960:force_original_aspect_ratio=increase,crop=540:960" -c:v libx264 -profile:v main -pix_fmt yuv420p -b:v 900k -movflags +faststart vetrina-scontro.mp4
```

`-an` toglie l'audio, `-movflags +faststart` sposta l'indice all'inizio del file
cosi' il video comincia prima di essere scaricato tutto.

> Ricordarsi che il deploy fa `rm -rf /var/www/html/*`: i file nuovi qui dentro
> arrivano sul sito solo se la cartella `media` e' nella riga di `tar` in
> [`../webgl-hosting-cache.md`](../webgl-hosting-cache.md). La cartella c'e' gia',
> quindi basta aggiungere i file.
