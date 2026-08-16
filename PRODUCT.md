# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Sistema multi-rol de uso interno y de clientes, con cinco roles fijos: Administrador, Coordinador, Técnico, Cliente y Ventas.

- **Administrador / Coordinador**: gestionan clientes, contratos, activos (impresoras Ricoh) y asignan mantenimientos preventivos/correctivos desde escritorio (back-office).
- **Técnico**: personal de campo que atiende mantenimientos y tickets; usa su propia vista (`MyWorkView`) para gestionar el trabajo asignado.
- **Cliente**: usuario final que consulta sus activos y contratos vía portal propio (`MyAssetsView`, `MyContractsView`).
- **Ventas**: rol adicional dentro del mismo sistema (alcance por confirmar a futuro).

Confirmado explícitamente: las tres superficies (back-office, campo, portal de cliente) tienen la misma prioridad de cuidado visual — no hay un rol "principal" sobre los demás.

## Product Purpose

Gestión integral de alquiler y servicio técnico de impresoras: mantenimiento preventivo, soporte correctivo, y asignación de técnicos de campo, cubriendo el ciclo completo desde el contrato de alquiler hasta la atención en sitio.

## Positioning

Especializado en el ciclo de alquiler + mantenimiento de impresoras, frente a llevarlo en Excel/WhatsApp o adaptar un ERP genérico: modela de forma nativa cronogramas de mantenimiento preventivo, SLA, y asignación de técnicos de campo — algo que herramientas genéricas no representan bien.

## Operating Context

- Mercado colombiano: catálogo de departamentos/ciudades precargado para clientes y sedes.
- Backend .NET 8 en capas (Domain/Application/Infrastructure/Api), EF Core + Postgres, Hangfire para jobs programados (cronogramas de mantenimiento), autenticación JWT con login por cédula.
- Frontend web: Vue 3 + TypeScript + Vite, Element Plus, Pinia, Vue Router.
- App móvil en Flutter: pendiente de desarrollo (no es parte del alcance visual actual).
- Los cinco roles se siembran automáticamente al arrancar la API; existe un administrador de arranque para entornos nuevos.

## Capabilities and Constraints

- Gestión de clientes (con sede(s) y cascada departamento→ciudad), contratos, activos (impresoras) y marcas de activos, incluyendo lecturas de contador (`MeterReadingsView`).
- Órdenes y cronogramas de mantenimiento; sistema de tickets.
- Autoservicio de técnicos (vista propia de trabajo asignado) y portal de autoservicio de clientes.
- Gestión de usuarios y contraseñas; login por cédula.
- Ideas futuras registradas por el usuario (no implementadas aún, no tratar como compromiso de producto): actualización en tiempo real, control de sesiones/auditoría, geolocalización de servicios, cola de asignación automática de técnicos, inventario de insumos (tóner, revelador, fusor), wiki de casos comunes, cálculo de SLA en horario laboral considerando festivos/fines de semana, mejoras al espacio de inicio de técnicos.

## Brand Commitments

Ninguno. Confirmado explícitamente por el usuario: no existe todavía marca/paleta/logo que preservar — hay libertad total para proponer una identidad visual nueva.

## Evidence on Hand

No hay clientes, testimonios, casos de estudio ni activos de marca reales disponibles. El trabajo de diseño futuro no debe inventar logos, testimonios ni datos de clientes reales.

## Product Principles

- Diseñado específicamente para el dominio de alquiler + mantenimiento de impresoras, no un ERP genérico reutilizado.
- Igual nivel de cuidado visual para las tres superficies: back-office (Administrador/Coordinador), campo (Técnico) y portal (Cliente).
- Precisión operativa y trazabilidad (SLA, cronogramas, roles) como base, con libertad para construir una identidad visual propia y memorable ya que no hay marca heredada que restrinja el diseño.
- El contexto colombiano (ciudades, festivos, horario laboral) es un dato de primera clase del producto, no un caso especial.
