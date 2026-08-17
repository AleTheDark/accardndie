#!/usr/bin/env bash
# Campionatore da tenere acceso sul VPS mentre gira la prova di carico.
#
#   ./loadtest-monitor.sh [porta] [secondi_tra_i_campioni] > prova.csv
#
# La prova di carico misura il gioco dal lato del giocatore: latenze e errori. Questo
# misura la macchina dal lato di dentro, e serve a rispondere alla domanda vera - "cosa
# si e' saturato per primo?". Senza, si sa solo che a un certo punto i tempi salgono.
#
# Le colonne, in ordine: momento, carico medio a 1 minuto, CPU e memoria del processo
# del server, memoria e swap di tutta la macchina, connessioni stabilite verso la porta
# del server, dimensione del database e del suo WAL.

set -euo pipefail

PORT="${1:-5017}"
INTERVAL="${2:-5}"
DB="${ACCARDND_DB:-/opt/accardnd/accardnd.db}"

pid="$(pgrep -f 'AccardND.Server' | head -n 1 || true)"
if [ -z "$pid" ]; then
    echo "Nessun processo AccardND.Server in esecuzione." >&2
    exit 1
fi

echo "momento,load1,cpu_processo,rss_mb,mem_usata_mb,mem_totale_mb,swap_usata_mb,connessioni,db_mb,wal_mb"

while kill -0 "$pid" 2>/dev/null; do
    now="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    load1="$(awk '{print $1}' /proc/loadavg)"

    # ps riporta la CPU come percentuale di un core: su 2 vCore il tetto e' 200.
    read -r cpu rss_kb <<<"$(ps -o %cpu=,rss= -p "$pid" | awk '{print $1, $2}')"
    rss_mb=$((rss_kb / 1024))

    read -r mem_used mem_total swap_used <<<"$(free -m | awk '/^Mem:/ {u=$3; t=$2} /^Swap:/ {s=$3} END {print u, t, s}')"

    # -H toglie l'intestazione, state connected conta solo le sessioni davvero aperte.
    connections="$(ss -Htn state connected "sport = :$PORT" 2>/dev/null | wc -l)"

    db_mb=0
    wal_mb=0
    [ -f "$DB" ] && db_mb=$(( $(stat -c %s "$DB") / 1048576 ))
    [ -f "$DB-wal" ] && wal_mb=$(( $(stat -c %s "$DB-wal") / 1048576 ))

    echo "$now,$load1,$cpu,$rss_mb,$mem_used,$mem_total,$swap_used,$connections,$db_mb,$wal_mb"
    sleep "$INTERVAL"
done
