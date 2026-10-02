Aqui quiero expresar ideas que tengo para el proyecto

1. Actualizacion en linea y tiempo real de los datos | CORREGIDO en codigo (reconexion infinita + resync, 13 vistas web y 21 states de la app nuevos), pendiente de prueba en vivo
2. ~~Manejo de las sesiones (tiempos maximos, sesion unica, logueo en base de datos de sesion para auditorias)~~ | HECHO (UserSessions + SessionService)
3. ~~Posible implementacion de geolocalizacion en los mantenimimientos o servicios~~ | HECHO (check-in/out con coordenadas, GeoDistance)
4. ~~Cola de asignacion automatica de servicios si los tecnicos estan ocupados~~ | HECHO (PendingAssignmentService + PendingAssignmentJob)
5. Ir implementando la parte de los equipos de computo (Clientes, activos, contratos, cronogramas, etc)
6. Manejo e inventario de insumos (toner, revelador, fusor, etc)
7. Wiki base de casos mas comunes de errores y como se solucionan como base de conocimiento
8. Base de drivers, backups de configuraciones, usuarios, etc.
9. Ir revisando el desarrollo de la app movil con flutter | En progreso (paridad con la web casi completa: tickets, festivos, historiales, filtros en cascada, diseño Clay)
10. Revisiones de seguridad (Request limit, sql injection, IP limiting, RLS, Encripcion de datos, RLS, Server Side Validation, Input Sanity, Autenticacion con expiracion de sesion, CORS, CI/CD para despliegues automaticos, manejo de excepciones, manejo de errores, controles de fallo) - En progreso (80%)
11. Aprovechamiento del espacio de Inicio de los tecnicos
12. ~~calculo de horas en horario laboral para los sla y demas, tambien tener en cuenta festivos y fines de semana~~ | HECHO (WorkCalendarContext.BusinessHours, festivos de Colombia, cobertura HorarioOficina/24x7)

Pendientes por aplicar y cambios

1. ~~Limitar a 10 caracteres el numero de contacto de todo usuario, tecnico, cliente.~~ | HECHO (PhoneRules en backend, phone.ts en web, phone_input.dart en app)
2. ~~Vincular, automaticamente, un activo al tecnico que realiza una instalacion por primera vez. En caso de se un favor que se pida, la re-vincuacion se debe hacer por el administrador.~~ | HECHO (TechnicianAssets: se vincula en el check-out de instalacion; gestion manual por Administrador)
3. ~~Actualizaciones en tiempo real en Web y App (sockets o algo)~~ | CORREGIDO en codigo, falta prueba en vivo: hoy no funciona bien, si estoy en una ventana o en el navegador y hay cambios no se actualizan correctamente. Debe cubrir todo lo que pueda cambiar de estado.
4. ~~Pedir foto del contador en un servicio o mantenimiento.~~ | HECHO en codigo (tipo de evidencia Contador, obligatoria en tickets/ordenes de equipos bajo contrato, opcional en tickets de clientes sin contrato vigente (con o sin activo catalogado); instalaciones no, porque no llevan evidencias), falta prueba en vivo
5. ~~Control del horario para las instalaciones: hoy no se controla.~~ | HECHO en codigo: el check-in de una instalacion se rechaza fuera de la jornada del tecnico, en festivo o en permiso (cliente 24/7 solo respeta permisos); la lista marca 'Fuera de horario'
6. Inventario de consumibles y repuestos, con checks al atender un mantenimiento o ticket de un cliente.
7. Control de uso de toner: contabilizar toner usados y su duracion comparado con las impresiones (BI para administradores).
8. ~~Permitir poner una maquina en mantenimiento en cualquier momento.~~ | HECHO (interpretado como mantenimiento a demanda: el staff pide una orden para un equipo instalado con cronograma activo, fuera de los umbrales; entra por el mismo flujo de asignacion/check-in/cierre; web y app). Si se referia a otra cosa (p. ej. que el tecnico lo pida desde campo, o cambiar el estado del activo), ajustar
9. Aprovechamiento del espacio de Inicio de los tecnicos (ya esta como punto 11 arriba).
10. Notificaciones push (Firebase/FCM): hoy NO existen. Solo hay tiempo real por SignalR (con la app abierta) y correo de contrasena generada. Hay proyecto Firebase y clave de servicio en secrets/, pero la app no tiene firebase_messaging, el backend no tiene emisor (FirebaseAdmin se quito por no usarse) y no hay tabla de tokens de dispositivo. Propuesta: token por dispositivo atado a usuario y sesion, push en los mismos puntos del aviso en vivo (empezando por ticket u orden asignada a un tecnico), mensaje solo con identificadores. Decision pendiente: con la sesion cerrada, propuesta = borrar el token al cerrar sesion y no enviar nada (seguridad); alternativa = seguir avisando.
