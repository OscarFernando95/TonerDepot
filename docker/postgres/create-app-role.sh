#!/usr/bin/env bash
# Crea (o actualiza) el rol de conexión de la aplicación, toner_app, SIN privilegios de owner.
# Ver SECURITY_AUDIT.md hallazgo #5.
#
# Por qué NO vive en una migración de EF Core: un rol es objeto de CLUSTER, no de base de datos, y
# sobre todo su contraseña quedaría versionada en git — exactamente el hallazgo #1 que ya se purgó
# del historial. Los GRANT y las políticas RLS sí van en la migración AddRowLevelSecurity.
#
# Este script se corre a mano contra un entorno existente. Para clones nuevos, el mismo rol se crea
# automáticamente vía docker/postgres/initdb.d/ al inicializar el contenedor por primera vez.
#
# Uso:
#   TONER_APP_PASSWORD='...' ./docker/postgres/create-app-role.sh
#   TONER_APP_PASSWORD='...' PGHOST=prod-db PGUSER=toner ./docker/postgres/create-app-role.sh
set -euo pipefail

: "${TONER_APP_PASSWORD:?Falta TONER_APP_PASSWORD (se pasa por entorno, nunca hardcodeada)}"

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5433}"
PGUSER="${PGUSER:-toner}"        # owner: el único que puede crear roles y otorgar privilegios
PGDATABASE="${PGDATABASE:-toner}"

psql -v ON_ERROR_STOP=1 \
     -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" \
     -v app_password="$TONER_APP_PASSWORD" <<'SQL'
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'toner_app') THEN
    CREATE ROLE toner_app LOGIN
      NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS NOINHERIT;
    RAISE NOTICE 'Rol toner_app creado.';
  ELSE
    RAISE NOTICE 'Rol toner_app ya existe; solo se actualiza la contraseña.';
  END IF;
END
$$;

-- Fuera del bloque DO: :'app_password' es una sustitución de psql con comillado seguro, y
-- ALTER ROLE no acepta parámetros vinculados dentro de plpgsql sin EXECUTE format().
ALTER ROLE toner_app WITH PASSWORD :'app_password';

-- GRANT ... ON DATABASE no acepta CURRENT_CATALOG ni parámetros: hay que construirlo con format()
-- sobre current_database() e identificador citado.
DO $$
BEGIN
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO toner_app', current_database());
END
$$;

-- Verificación explícita: si el rol quedara con BYPASSRLS o SUPERUSER, todas las políticas RLS
-- serían decorativas. Mejor que explote aquí a que nadie lo note.
DO $$
DECLARE r pg_roles%ROWTYPE;
BEGIN
  SELECT * INTO r FROM pg_roles WHERE rolname = 'toner_app';
  IF r.rolsuper OR r.rolbypassrls OR r.rolcreatedb OR r.rolcreaterole THEN
    RAISE EXCEPTION 'toner_app tiene atributos peligrosos (super=% bypassrls=% createdb=% createrole=%)',
      r.rolsuper, r.rolbypassrls, r.rolcreatedb, r.rolcreaterole;
  END IF;
  RAISE NOTICE 'Atributos de toner_app verificados: sin superuser, sin bypassrls.';
END
$$;
SQL

echo "Rol toner_app listo en ${PGHOST}:${PGPORT}/${PGDATABASE}."
echo "Recuerda correr las migraciones (con MigrationsConnection / rol owner) para aplicar los GRANT y las políticas RLS."
