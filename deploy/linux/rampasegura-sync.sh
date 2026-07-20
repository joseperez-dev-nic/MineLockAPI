#!/usr/bin/env bash
# =====================================================================
# Sincronizador Rampa Segura: llama a los endpoints de la API local
# para empujar los datos a la nube, cada cual con su propio intervalo.
#
# Instalar en: /usr/local/bin/rampasegura-sync.sh
# Config en:   /etc/rampasegura/sync.conf
#
# No usa cron porque cron no baja de 1 minuto y los marcajes necesitan
# ~pocos segundos. En vez de eso corre en bucle y lleva la cuenta de
# cuando le toca a cada sincronizacion.
#
# Antes de sincronizar comprueba que se pueda ALCANZAR la nube; si no hay
# conexion, pone todo en pausa y reintenta hasta que vuelva. Nada se pierde.
# =====================================================================

set -uo pipefail

CONFIG="${RAMPASEGURA_CONFIG:-/etc/rampasegura/sync.conf}"

if [[ ! -r "$CONFIG" ]]; then
    echo "ERROR: no se pudo leer la configuracion: $CONFIG" >&2
    exit 1
fi
# shellcheck source=/dev/null
source "$CONFIG"

: "${API_BASE:?Falta API_BASE en $CONFIG}"
: "${API_KEY:?Falta API_KEY en $CONFIG}"
: "${CURL_TIMEOUT:=120}"
: "${LOG_SUCCESS:=0}"
: "${CHECK_HOST:=}"        # vacio = no verificar (siempre intenta)
: "${CHECK_PORT:=443}"
: "${CHECK_TIMEOUT:=3}"

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }

# El bucle avanza de a 1 segundo.
TICK=1

# ---------------------------------------------------------------------
# Conectividad: intenta abrir un socket TCP a la nube (host:puerto).
# Usa /dev/tcp (propio de bash, sin instalar nada) con un timeout.
# Devuelve 0 si se pudo conectar, 1 si no. Si CHECK_HOST esta vacio,
# no se verifica y siempre devuelve 0.
# ---------------------------------------------------------------------
have_connectivity() {
    [[ -z "$CHECK_HOST" ]] && return 0
    timeout "$CHECK_TIMEOUT" bash -c "exec 3<>/dev/tcp/${CHECK_HOST}/${CHECK_PORT}" 2>/dev/null
}

# Estado de conexion, para avisar solo cuando CAMBIA (no cada segundo).
CONN_STATE="unknown"
mark_online() {
    if [[ "$CONN_STATE" != "online" ]]; then
        [[ "$CONN_STATE" != "unknown" ]] && \
            log "CONEXION restablecida (${CHECK_HOST}:${CHECK_PORT}). Reanudando sincronizacion."
        CONN_STATE="online"
    fi
}
mark_offline() {
    if [[ "$CONN_STATE" != "offline" ]]; then
        log "SIN CONEXION a ${CHECK_HOST}:${CHECK_PORT}. Sincronizacion EN PAUSA (los datos quedan pendientes)."
        CONN_STATE="offline"
    fi
}

# ---------------------------------------------------------------------
# Llama un endpoint de sincronizacion.
#   $1 = nombre del controlador (ej. attendancesync)
#   $2 = etiqueta para el log   (ej. ATTENDANCE)
# ---------------------------------------------------------------------
sync_one() {
    local endpoint="$1" label="$2"
    local url="${API_BASE}/api/${endpoint}/execute"
    local body http curl_rc

    body=$(curl -sS -m "$CURL_TIMEOUT" -w $'\n%{http_code}' \
                -X POST "$url" \
                -H "X-Api-Key: ${API_KEY}" \
                -H "Content-Length: 0" 2>&1)
    curl_rc=$?

    if (( curl_rc != 0 )); then
        log "ERROR  [$label] no se pudo llamar a la API (curl rc=$curl_rc): ${body}"
        return 1
    fi

    http="${body##*$'\n'}"     # ultima linea = codigo HTTP
    body="${body%$'\n'*}"      # el resto = cuerpo JSON

    if [[ "$http" == "200" ]]; then
        (( LOG_SUCCESS == 1 )) && log "OK     [$label] ${body}"
        return 0
    fi

    log "FALLO  [$label] HTTP $http: ${body}"
    return 1
}

# ---------------------------------------------------------------------
# Contadores: cuantos segundos faltan para el proximo disparo de cada uno.
# Arrancan en 0 para que todo se sincronice una vez al iniciar.
# ---------------------------------------------------------------------
declare -A NEXT=(
    [attendance]=0 [person]=0 [photo]=0
    [appuser]=0 [alertthreshold]=0 [synclog]=0
)
declare -A ENDPOINT=(
    [attendance]=attendancesync      [person]=personsync
    [photo]=photosync                [appuser]=appusersync
    [alertthreshold]=alertthresholdsync
    [synclog]=synclogsync
)
declare -A LABEL=(
    [attendance]=ATTENDANCE          [person]=PERSON
    [photo]=PHOTO                    [appuser]=ROLE+APP_USER
    [alertthreshold]=ALERT_THRESHOLD [synclog]=SYNCLOG+AUDIT
)

interval_of() {
    case "$1" in
        attendance)     echo "${INTERVAL_ATTENDANCE:-0}" ;;
        person)         echo "${INTERVAL_PERSON:-0}" ;;
        photo)          echo "${INTERVAL_PHOTO:-0}" ;;
        appuser)        echo "${INTERVAL_APPUSER:-0}" ;;
        alertthreshold) echo "${INTERVAL_ALERTTHRESHOLD:-0}" ;;
        synclog)        echo "${INTERVAL_SYNCLOG:-0}" ;;
        *)              echo 0 ;;
    esac
}

TASKS=(attendance person photo appuser alertthreshold synclog)

# Apagado limpio cuando systemd manda SIGTERM.
running=1
trap 'running=0' SIGTERM SIGINT

log "Sincronizador iniciado. API=${API_BASE}"
if [[ -n "$CHECK_HOST" ]]; then
    log "  Verificacion de conexion: ${CHECK_HOST}:${CHECK_PORT} (timeout ${CHECK_TIMEOUT}s)"
else
    log "  Verificacion de conexion: DESACTIVADA (siempre intenta)"
fi
for task in "${TASKS[@]}"; do
    iv=$(interval_of "$task")
    if (( iv > 0 )); then log "  ${LABEL[$task]}: cada ${iv}s"; else log "  ${LABEL[$task]}: DESACTIVADO"; fi
done

# ---------------------------------------------------------------------
# Bucle principal
# ---------------------------------------------------------------------
while (( running )); do

    # 1) Ver si alguna tarea esta lista para dispararse en este tick.
    any_due=0
    for task in "${TASKS[@]}"; do
        iv=$(interval_of "$task")
        (( iv <= 0 )) && continue
        (( NEXT[$task] <= 0 )) && any_due=1
    done

    # 2) Solo si hay algo que hacer, se comprueba la conexion (evita
    #    verificar cada segundo cuando no toca sincronizar nada).
    online=1
    if (( any_due )); then
        if have_connectivity; then mark_online; online=1; else mark_offline; online=0; fi
    fi

    # 3) Recorrer las tareas.
    for task in "${TASKS[@]}"; do
        iv=$(interval_of "$task")
        (( iv <= 0 )) && continue

        if (( NEXT[$task] <= 0 )); then
            if (( online )); then
                sync_one "${ENDPOINT[$task]}" "${LABEL[$task]}"
                NEXT[$task]=$iv          # reprograma al siguiente ciclo
            fi
            # Si esta offline: NO se reprograma, queda pendiente y se
            # dispara en cuanto vuelva la conexion.
        else
            NEXT[$task]=$(( NEXT[$task] - TICK ))
        fi
    done

    sleep "$TICK"
done

log "Sincronizador detenido."
