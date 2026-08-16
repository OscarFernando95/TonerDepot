# Postgres local con SSL habilitado (ver SECURITY_AUDIT.md hallazgo #10), para que la app pueda
# conectarse con "Ssl Mode=Require" igual que en producción. El certificado se copia DENTRO de la
# imagen (no por bind-mount) a propósito: un bind-mount desde el host llega con ownership/permisos
# que varían según el sistema operativo de quien lo levante, y Postgres rechaza arrancar si la clave
# privada tiene acceso de grupo/otros — horneándolo en build time se evita esa variabilidad.
FROM postgres:16-alpine

COPY server.crt /var/lib/postgresql/certs/server.crt
COPY server.key /var/lib/postgresql/certs/server.key

RUN chown postgres:postgres /var/lib/postgresql/certs/server.crt /var/lib/postgresql/certs/server.key \
    && chmod 644 /var/lib/postgresql/certs/server.crt \
    && chmod 600 /var/lib/postgresql/certs/server.key
