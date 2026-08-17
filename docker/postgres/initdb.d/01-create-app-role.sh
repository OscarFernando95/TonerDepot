#!/usr/bin/env bash
# Crea el rol toner_app al inicializar el contenedor de Postgres por primera vez.
#
# ⚠️ Los scripts de /docker-entrypoint-initdb.d/ SOLO se ejecutan cuando el directorio de datos está
# vacío (primera inicialización del clúster). Si el volumen toner_postgres_data ya tiene datos, este
# script NO corre — en ese caso usa docker/postgres/create-app-role.sh contra la base existente.
set -euo pipefail

APP_PASSWORD="${TONER_APP_PASSWORD:-toner_app_dev_password}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
     -v app_password="$APP_PASSWORD" <<'SQL'
CREATE ROLE toner_app LOGIN
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS NOINHERIT;

ALTER ROLE toner_app WITH PASSWORD :'app_password';

-- GRANT ... ON DATABASE no acepta CURRENT_CATALOG ni parámetros: hay que construirlo con format()
-- sobre current_database() e identificador citado.
DO $$
BEGIN
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO toner_app', current_database());
END
$$;
SQL

echo "[initdb] Rol toner_app creado. Los GRANT y las políticas RLS los aplica la migración AddRowLevelSecurity."
