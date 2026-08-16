#!/usr/bin/env bash
# Genera un certificado autofirmado para que el Postgres local de docker-compose pueda arrancar con
# SSL (ver SECURITY_AUDIT.md hallazgo #10). Idempotente: si server.crt/server.key ya existen, no hace
# nada. Los archivos generados NO se versionan (.gitignore) — son artefactos locales, no secretos:
# la app se conecta con "Ssl Mode=Require", que cifra el canal pero nunca valida este certificado,
# así que no protege nada sensible por sí mismo.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"

if [ -f server.crt ] && [ -f server.key ]; then
  echo "server.crt/server.key ya existen, no se regeneran."
  exit 0
fi

openssl req -new -x509 -days 3650 -nodes -text \
  -out server.crt \
  -keyout server.key \
  -subj "/CN=localhost"

chmod 600 server.key
chmod 644 server.crt

echo "Generados docker/postgres/server.crt y server.key (válidos 10 años, dev-only)."
