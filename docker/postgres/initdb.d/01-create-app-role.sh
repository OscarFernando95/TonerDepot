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

-- Rol de staff: sin login y sin BYPASSRLS; su acceso amplio viene de una política permisiva
-- explícita por tabla, no de un atributo de rol. Ver create-app-role.sh para el detalle completo.
CREATE ROLE toner_app_staff NOLOGIN
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;

-- toner_app es NOINHERIT: solo puede tomar estos privilegios con un SET ROLE explícito.
GRANT toner_app_staff TO toner_app;

-- El owner hereda (INHERIT) para poder pasar FORCE ROW LEVEL SECURITY en migraciones con DML.
DO $$
BEGIN
  EXECUTE format('GRANT toner_app_staff TO %I', current_user);
END
$$;

-- GRANT ... ON DATABASE no acepta CURRENT_CATALOG ni parámetros: hay que construirlo con format()
-- sobre current_database() e identificador citado.
DO $$
BEGIN
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO toner_app', current_database());
END
$$;
SQL

echo "[initdb] Roles toner_app y toner_app_staff creados. Los GRANT y las políticas RLS los aplica la migración AddRowLevelSecurity."
