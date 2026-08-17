#!/usr/bin/env python3
"""Ridimensiona le illustrazioni del gioco per il sito.

Gli originali stanno in Assets/_Project/Art/Cards e sono file da 1-5 MB l'uno: 81
carte fanno 111 MB, cioe' una pagina che non si apre. Qui diventano WebP piccoli
dentro Docs/web/media/, che e' la cartella che finisce sul server.

Da rilanciare quando cambia un'illustrazione o si aggiunge una carta. Non tocca
gli originali e riscrive solo i file di destinazione piu' vecchi della sorgente,
quindi rilanciarlo a vuoto non costa niente.

    python Docs/web/gen-media.py

Le larghezze sono il doppio di come le carte si vedono a schermo (120 px nella
griglia del database, 240 per un boss): serve agli schermi a densita' doppia, e
raddoppiare qui costa pochi KB mentre servire l'originale ne costa mille.
"""

import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("serve Pillow: python -m pip install Pillow")

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets" / "_Project" / "Art" / "Cards"
MEDIA = Path(__file__).resolve().parent / "media"

# (cartella sorgente, cartella di destinazione, larghezza finale, qualita')
JOBS = [
    (ART / "Monsters", MEDIA / "carte", 240, 78),
    (ART / "Bosses", MEDIA / "boss", 480, 82),
]


def convert(src: Path, dst: Path, width: int, quality: int) -> bool:
    """Scrive dst da src. Torna False se era gia' aggiornato."""
    if dst.exists() and dst.stat().st_mtime >= src.stat().st_mtime:
        return False

    with Image.open(src) as image:
        if image.width > width:
            height = round(image.height * width / image.width)
            image = image.resize((width, height), Image.LANCZOS)
        # I PNG di partenza sono RGB, ma un domani potrebbero avere l'alpha:
        # convertire a monte evita un errore a valle sul primo file con canale in piu'.
        if image.mode not in ("RGB", "RGBA"):
            image = image.convert("RGB")
        image.save(dst, "WEBP", quality=quality, method=6)
    return True


def main() -> int:
    if not ART.is_dir():
        sys.exit(f"non trovo le illustrazioni in {ART}")

    total_written = 0
    for source, target, width, quality in JOBS:
        if not source.is_dir():
            print(f"salto {source.name}: la cartella non c'e'")
            continue

        target.mkdir(parents=True, exist_ok=True)
        written = skipped = 0
        for png in sorted(source.glob("*.png")):
            if convert(png, target / (png.stem + ".webp"), width, quality):
                written += 1
            else:
                skipped += 1

        size_kb = sum(f.stat().st_size for f in target.glob("*.webp")) // 1024
        print(f"{target.relative_to(MEDIA.parent)}: {written} scritti, "
              f"{skipped} gia' aggiornati, {size_kb} KB in tutto")
        total_written += written

    print(f"fatto ({total_written} file scritti)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
