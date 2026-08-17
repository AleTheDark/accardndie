#!/usr/bin/env python3
"""Server di sviluppo per Docs/web, con le stesse regole di nginx sugli indirizzi.

Serve a provare in locale quello che in produzione fa nginx, cioe' le due regole
del blocco "Indirizzi senza .html" in Docs/deploy/accardndie-nginx.conf:

  - /guida  -> serve guida.html            (try_files $uri $uri.html)
  - /guida.html -> 301 su /guida           (il redirect che tiene un solo URL)

Con il semplice `python -m http.server` /guida darebbe 404 e non ci si
accorgerebbe di un link sbagliato fino al deploy.

    python Docs/web/devserver.py [porta]
"""

import sys
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

RADICE = Path(__file__).resolve().parent


class Handler(SimpleHTTPRequestHandler):
    # Quello che si modifica lavorando non deve mai arrivare dalla cache: un
    # site.css tenuto dal browser mostra il layout di prima e fa sembrare rotta
    # una cosa che funziona (la griglia delle carte che sembra collassata a una
    # colonna e' sempre e solo il foglio vecchio).
    #
    # Le immagini invece si lasciano cachare, ed e' altrettanto importante: il
    # database carte ne ha novanta, e con no-store il browser le riscarica a ogni
    # scorrimento da un server Python a un thread per richiesta. Il risultato e'
    # una pagina che mostra i riquadri vuoti e sembra avere le immagini rotte -
    # cioe' di nuovo un problema che non esiste in produzione.
    # La lista e' quella di cosa si PUO' cachare, non il contrario: gli indirizzi
    # delle pagine non hanno estensione (/carte, /guida), quindi una regola scritta
    # al negativo li lascerebbe fuori proprio dove serve di piu'.
    CACHABILI = (".webp", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico",
                 ".ttf", ".woff", ".woff2", ".mp4", ".webm")

    def end_headers(self):
        percorso = self.path.split("?", 1)[0].lower()
        if percorso.endswith(self.CACHABILI):
            self.send_header("Cache-Control", "max-age=300")
        else:
            self.send_header("Cache-Control", "no-store, must-revalidate")
        super().end_headers()

    def do_GET(self):
        if self._redirect_html():
            return
        super().do_GET()

    def do_HEAD(self):
        if self._redirect_html():
            return
        super().do_HEAD()

    def _redirect_html(self):
        """/guida.html -> 301 /guida. Come la location ~ \\.html$ di nginx."""
        percorso = self.path.split("?", 1)[0]
        if not percorso.endswith(".html") or percorso.startswith("/game/"):
            return False
        nuovo = "/" if percorso == "/index.html" else percorso[: -len(".html")]
        self.send_response(301)
        self.send_header("Location", nuovo)
        self.end_headers()
        return True

    def translate_path(self, path):
        """try_files $uri $uri.html $uri/: se non c'e' il file, prova con .html."""
        risolto = Path(super().translate_path(path))
        if not risolto.exists():
            con_estensione = risolto.with_name(risolto.name + ".html")
            if con_estensione.is_file():
                return str(con_estensione)
        return str(risolto)


def main():
    porta = int(sys.argv[1]) if len(sys.argv) > 1 else 8123
    handler = partial(Handler, directory=str(RADICE))
    print(f"http://localhost:{porta}  (radice {RADICE}, indirizzi senza .html)")
    # Threading e non HTTPServer semplice: con un browser aperto sulla pagina, la
    # versione a un thread solo resta occupata dalla connessione tenuta viva e
    # ogni altra richiesta (un curl di controllo, una seconda scheda) si blocca.
    ThreadingHTTPServer(("127.0.0.1", porta), handler).serve_forever()


if __name__ == "__main__":
    main()
