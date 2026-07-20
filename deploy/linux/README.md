# Sincronizador Rampa Segura para Linux

Corre en el **servidor local** (el que tiene la base de datos local) y llama a los
endpoints de la API para empujar los datos a la nube. Cada tabla tiene su propio
intervalo, configurable sin tocar código.

## Por qué no cron

`cron` no baja de **1 minuto**, y los marcajes se quieren cada ~3 segundos. Por eso
esto es un servicio en bucle: lleva la cuenta de cuándo le toca a cada
sincronización y las dispara con su propia cadencia.

---

## Instalación

```bash
# 1. Script
sudo cp rampasegura-sync.sh /usr/local/bin/
sudo chmod +x /usr/local/bin/rampasegura-sync.sh

# 2. Configuración
sudo mkdir -p /etc/rampasegura
sudo cp rampasegura-sync.conf /etc/rampasegura/sync.conf
sudo chmod 600 /etc/rampasegura/sync.conf   # contiene la API key

# 3. Usuario del servicio (sin privilegios)
sudo useradd --system --no-create-home --shell /usr/sbin/nologin rampasegura
sudo chown rampasegura:rampasegura /etc/rampasegura/sync.conf

# 4. Servicio
sudo cp rampasegura-sync.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now rampasegura-sync
```

**Antes de arrancar**, edita `/etc/rampasegura/sync.conf` y ajusta al menos
`API_BASE` (dónde corre tu API) y `API_KEY`.

---

## Cambiar los intervalos

Todo se configura en `/etc/rampasegura/sync.conf`, en **segundos**:

```bash
INTERVAL_ATTENDANCE=3          # marcajes
INTERVAL_PERSON=300            # personal
INTERVAL_PHOTO=600             # fotos
INTERVAL_APPUSER=600           # roles + usuarios
INTERVAL_ALERTTHRESHOLD=600    # umbrales
INTERVAL_SYNCLOG=3600          # bitácoras
```

Poner **`0` desactiva** esa sincronización.

Después de editar:
```bash
sudo systemctl restart rampasegura-sync
```

---

## Ver qué está pasando

```bash
# En vivo
journalctl -u rampasegura-sync -f

# Últimas 100 líneas
journalctl -u rampasegura-sync -n 100

# Solo errores
journalctl -u rampasegura-sync -p err

# Estado del servicio
systemctl status rampasegura-sync
```

Por defecto **solo se registran los fallos**, para no llenar el journal con un
mensaje cada 3 segundos. Para ver también los éxitos (útil al depurar), pon
`LOG_SUCCESS=1` en la config y reinicia.

---

## Probar a mano antes de instalar el servicio

```bash
RAMPASEGURA_CONFIG=./rampasegura-sync.conf ./rampasegura-sync.sh
```
Se corta con `Ctrl+C`.

Y para probar un solo endpoint:
```bash
curl -i -X POST http://localhost:5000/api/attendancesync/execute \
     -H "X-Api-Key: TU_API_KEY"
```

---

## Notas

- **Orden y llaves foráneas:** cada endpoint es independiente y puede fallar por FK
  si le falta un dato de otra tabla (por ejemplo un marcaje de una persona que aún
  no se sincronizó). No es problema: los syncs incrementales dejan la fila como
  pendiente y **se recuperan solos** en el siguiente ciclo.
- **Sin internet / nube caída:** antes de cada sincronización el script comprueba
  que se pueda **alcanzar la nube** (conexión TCP a `CHECK_HOST:CHECK_PORT`). Si no
  hay conexión, pone todo **en pausa** (no llama a la API en vano) y reintenta solo
  cada segundo; en cuanto vuelve la conexión, sincroniza de inmediato. Los datos
  quedan pendientes y **no se pierde nada**. Solo se escribe un aviso en el log
  cuando el estado cambia (se cae / se restablece), no cada segundo.
  - Por defecto se prueba el MySQL de la nube (`185.225.232.107:3307`), que es lo
    que realmente usa el sync. Para verificar solo "hay internet", usa
    `CHECK_HOST="8.8.8.8"` y `CHECK_PORT=53`.
  - Para desactivar la verificación (que siempre intente), deja `CHECK_HOST=""`.
- **Un ciclo lento no atropella al siguiente:** las llamadas son secuenciales, así
  que si una sincronización de fotos tarda 30s, el resto simplemente espera su turno.
- **`CURL_TIMEOUT`:** si sincronizas muchas fotos por primera vez, súbelo (la carga
  inicial puede tardar).
