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

-- Rol de staff: SIN login (nadie se conecta como él) y SIN BYPASSRLS. Su "bypass" es explícito y
-- auditable — una política permisiva USING (true) por tabla, visible en pg_policies — en vez de un
-- atributo de rol que saltaría también cualquier tabla futura. Ver CODE_QUALITY_AUDIT.md #2.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'toner_app_staff') THEN
    CREATE ROLE toner_app_staff NOLOGIN
      NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
    RAISE NOTICE 'Rol toner_app_staff creado.';
  ELSE
    RAISE NOTICE 'Rol toner_app_staff ya existe.';
  END IF;
END
$$;

-- toner_app es NOINHERIT: esta membresía NO le da los privilegios de staff de forma pasiva, solo la
-- capacidad de pedirlos con un SET ROLE explícito (que es lo que hace TenantContextInterceptor
-- cuando el contexto de la request es staff). Verificado: sin SET ROLE, toner_app sigue viendo solo
-- las filas de su propio cliente.
GRANT toner_app_staff TO toner_app;

-- El rol OWNER también necesita ser miembro, y este sí hereda (INHERIT por defecto). Motivo: las
-- tablas tienen FORCE ROW LEVEL SECURITY, así que las políticas aplican también al owner; con
-- políticas restringidas por rol (TO toner_app / TO toner_app_staff) un owner que no sea miembro no
-- coincide con NINGUNA y sus UPDATE/DELETE de migración afectarían 0 filas EN SILENCIO.
-- Se concede sobre current_user (el owner que corre este script) en vez de un nombre fijo, para no
-- versionar el nombre del rol owner, que cambia entre entornos.
DO $$
BEGIN
  EXECUTE format('GRANT toner_app_staff TO %I', current_user);
  RAISE NOTICE 'Owner % agregado a toner_app_staff (necesario para migraciones con DML bajo FORCE RLS).', current_user;
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
  FOR r IN SELECT * FROM pg_roles WHERE rolname IN ('toner_app', 'toner_app_staff') LOOP
    IF r.rolsuper OR r.rolbypassrls OR r.rolcreatedb OR r.rolcreaterole THEN
      RAISE EXCEPTION '% tiene atributos peligrosos (super=% bypassrls=% createdb=% createrole=%)',
        r.rolname, r.rolsuper, r.rolbypassrls, r.rolcreatedb, r.rolcreaterole;
    END IF;
  END LOOP;
  RAISE NOTICE 'Atributos verificados: ni toner_app ni toner_app_staff tienen superuser o bypassrls.';
END
$$;

-- El fix de FORCE RLS + migraciones solo sirve si la membresía es HEREDADA: pg_has_role con modo
-- 'usage' (no 'member') es justo el criterio con que Postgres empareja las políticas TO <rol>.
DO $$
BEGIN
  IF NOT pg_has_role(current_user, 'toner_app_staff', 'usage') THEN
    RAISE EXCEPTION 'El owner % no hereda privilegios de toner_app_staff (¿NOINHERIT?). '
                    'Las migraciones con DML sobre tablas con FORCE RLS afectarían 0 filas.', current_user;
  END IF;
  RAISE NOTICE 'Owner % hereda de toner_app_staff: las migraciones con DML pasarán FORCE RLS.', current_user;
END
$$;
SQL

echo "Rol toner_app listo en ${PGHOST}:${PGPORT}/${PGDATABASE}."
echo "Recuerda correr las migraciones (con MigrationsConnection / rol owner) para aplicar los GRANT y las políticas RLS."
