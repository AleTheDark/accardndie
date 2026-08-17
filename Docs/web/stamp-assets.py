#!/usr/bin/env python3
"""Marca site.css e site.js con un numero di versione preso dal loro contenuto.

Il problema che risolve. Le pagine chiedono `site.css` secco, e nginx non gli mette
nessun Cache-Control: Cloudflare lo tiene al bordo per ore. Dopo un deploy che
cambia il foglio di stile, i visitatori continuano a ricevere quello vecchio - e un
ricaricamento forzato non basta, perche' salta la cache del browser e non quella del
bordo. Il sintomo non e' una pagina senza stile, che si noterebbe: e' una pagina
quasi giusta, con una regola nuova che manca. La griglia del database carte che va a
capo dove non dovrebbe, per esempio.

La soluzione e' la stessa che il template WebGL usa gia' per il suo foglio di stile:
mettere una versione nell'URL. Qui la versione e' l'hash del file, quindi cambia da
sola quando il contenuto cambia e resta identica quando non cambia - nessun numero
da ricordare e nessun deploy che invalida la cache per niente.

Da lanciare dopo ogni modifica a site.css o site.js, prima del deploy:

    python Docs/web/stamp-assets.py

E' idempotente: rilanciarlo senza aver toccato niente non modifica nessun file.
"""

import hashlib
import re
import sys
from pathlib import Path

WEB = Path(__file__).resolve().parent
ASSET = ("site.css", "site.js")
# Le pagine statiche piu' il generatore, che porta la sua copia dei tag nel
# blocco HEAD: se si marca solo l'HTML, il primo gen-carte.py rimette il tag nudo.
BERSAGLI = sorted(WEB.glob("*.html")) + [WEB / "gen-carte.py"]


def versione(nome):
    """Otto caratteri di hash del file: cambiano solo se cambia il contenuto."""
    percorso = WEB / nome
    if not percorso.is_file():
        sys.exit(f"non trovo {percorso}")
    return hashlib.sha256(percorso.read_bytes()).hexdigest()[:8]


def main():
    versioni = {nome: versione(nome) for nome in ASSET}
    for nome, v in versioni.items():
        print(f"{nome} -> v={v}")

    # Cattura sia il tag nudo (site.css) sia uno gia' marcato (site.css?v=abc),
    # cosi' il rilancio aggiorna invece di accodare un secondo ?v=.
    schemi = {
        nome: re.compile(r'(["\'])' + re.escape(nome) + r'(?:\?v=[0-9a-f]+)?\1')
        for nome in ASSET
    }

    cambiati = 0
    for percorso in BERSAGLI:
        testo = originale = percorso.read_text(encoding="utf-8")
        for nome, schema in schemi.items():
            testo = schema.sub(lambda m, n=nome: f'{m.group(1)}{n}?v={versioni[n]}{m.group(1)}', testo)
        if testo != originale:
            percorso.write_text(testo, encoding="utf-8")
            cambiati += 1

    print(f"{cambiati} file aggiornati" if cambiati else "gia' aggiornati, niente da fare")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
