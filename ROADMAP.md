# D2STServer — Roadmap de capacidades

Última actualización: 2026-08-08

Este documento describe qué puede ampliarse en el servidor, en qué orden y con
qué evidencia debe considerarse terminado. Es un plan técnico y no implica que
todas las funciones deban implementarse de inmediato.

## 1. Alcance y decisiones vigentes

El objetivo principal sigue siendo ofrecer una experiencia local completa para
el cliente Dota 2 build 6783:

```text
crear lobby -> unir cuentas -> lanzar listen server -> jugar
-> recibir estado en vivo -> cerrar partida -> guardar resultado
-> consultar historial/estadísticas -> mostrar perfil
```

Las reglas que deben respetarse en cada fase son:

- Los datos que se muestran como reales deben proceder del lobby, del listen
  server o de filas persistidas; no se deben inventar valores de Valve.
- `7004 GameMatchSignOut` es la fuente autoritativa de resultado y estadísticas
  finales.
- `7034 ConnectedPlayers` es una fuente transitoria para estado en vivo; no
  reemplaza el cierre final de la partida.
- La conducta queda, por decisión vigente, como política local fija de score
  `10.000`, estado bueno y sin sanciones. No se implementará ahora una
  conducta dinámica, reportes, commends o low priority.
- Las respuestas de contratos no confirmados deben validarse mediante captura
  del cliente real antes de ampliar el protobuf o persistir datos.
- Cada fase de código debe compilar, pasar smoke de arranque, actualizar
  `HANDOFF.md`, revisarse y publicarse en `main`.
- El cliente Windows real es la autoridad final de compatibilidad; compilar no
  demuestra por sí solo que la interfaz del cliente acepte una respuesta.

## 2. Línea base ya implementada

Las Fases 1–6 y el soporte de partidas contra bots están implementados y
publicados. El detalle y la evidencia se encuentran en
[HANDOFF.md](HANDOFF.md).

### Lobby y partida local

- Login, sesión GC, Shared Objects y flujo de lobby existente.
- Creación, unión, salida y configuración de equipos del lobby.
- Lanzamiento del listen server y publicación de conexión mediante el flujo
  `4007/4508/4511/4506`.
- Actualizaciones de jugadores y estado de partida mediante `7034`.
- Primera sangre, kills Radiant/Dire, ventaja Radiant y edificios enviados en
  vivo cuando el listen server los reporta.
- Cierre `7004` normalizado, transaccional e idempotente.
- Lobby llevado a estado `POSTGAME` después de un cierre válido.
- Lanzamiento con un solo humano cuando `fill_with_bots` está activo; Dota es
  responsable de poblar la partida con sus bots nativos.
- Dificultad de bots conservada desde los mensajes de configuración del lobby.
- En partidas contra bots, `7004` solo incorpora participantes humanos del
  lobby a cuentas, historial y agregados; los bots no reciben identidad local.
- Las partidas contra bots actualizan estadísticas del humano, pero no aplican
  Elo porque no existe un rival humano equivalente.

### Datos persistentes

- Partida, jugadores, equipos, héroes, K/D/A, last hits, denies, GPM, XPM,
  oro, nivel, daño, curación, net worth, abandono, ítems y metadatos del
  servidor.
- Agregados generales por cuenta y agregados por héroe.
- Historial `7408 -> 7409`, resúmenes `8063 -> 8064` y compañeros
  `8124 -> 8125`.
- Lecturas de héroes `7274 -> 7275`, progreso `7521 -> 7522` y orden de héroes
  `7606 -> 7607`.
- Migración EF Core `20260808144219_InitialSchema` y puente compatible para
  bases antiguas creadas con el bootstrap SQL.
- Las dependencias de diseño de EF Core/Roslyn no se distribuyen con el
  servidor; las migraciones ya generadas y `Database.Migrate()` siguen activas.

### Perfil

- Victorias, derrotas, partidas y abandonos desde la base local.
- Tarjeta de perfil con slots estadísticos básicos.
- Edición persistente de slots mediante `7538 -> 7539`.
- Estado de conducta local mediante `8095 -> 8096`, score `10.000` y
  estadísticas locales de partidas/abandonos.

### Soporte inmediato de partidas contra bots

Esta capacidad ya está creada en el servidor, pero necesita validación con el
cliente build 6783:

- `FillWithBots=true` permite que el host único lance el lobby.
- La configuración de dificultad se conserva en `CSODOTALobby`.
- El cierre `7004` se filtra por los miembros humanos conocidos del lobby
  cuando la partida es contra bots.
- El resultado y las estadísticas del único humano siguen siendo datos reales;
  las filas de bots no se convierten en perfiles ficticios.
- La población y la IA de los bots dependen del listen server de Dota, no de
  D2STServer.

## 3. Criterio común de terminación

Una fase futura se considera terminada solo si cumple todos estos puntos:

1. El contrato está respaldado por código generado o por una captura real del
   cliente build 6783.
2. La cuenta solicitante queda aislada: no se mezclan estadísticas, lobbies o
   slots de otra cuenta.
3. Los datos se leen de una fuente real y se documentan los campos que siguen
   sin existir.
4. Las operaciones repetidas son idempotentes cuando puedan llegar paquetes
   duplicados o reconexiones.
5. La solución compila en `Release` con cero errores y cero advertencias.
6. El servidor arranca con una base nueva y con una base existente.
7. Se prueba el flujo normal y al menos un caso de reconexión o fallo relevante.
8. `HANDOFF.md` registra cambios, evidencia, limitaciones y siguiente paso.

## 4. Puerta inmediata: validación real con una PC

**Prioridad: P0. No es una fase de código; es la validación que desbloquea las
siguientes decisiones.**

### Flujo a probar

La primera validación puede hacerse con una sola cuenta, un cliente Windows y
los bots nativos de Dota:

1. Iniciar el servidor con una base nueva o una copia controlada de la base
   existente.
2. Conectar un cliente y verificar login, cachés y perfil.
3. Crear el lobby y activar `FillWithBots` y la dificultad deseada.
4. Seleccionar héroe y lanzar con un único humano.
5. Comprobar el mensaje de conexión al listen server y la creación de bots.
6. Confirmar cambios de jugadores, kills, primera sangre, ventaja y edificios.
7. Terminar la partida y capturar el `7004` completo.
8. Confirmar que el perfil humano recibe la partida, pero no los bots ni Elo.
9. Reconectar, consultar perfil, historial, resumen y estadísticas por héroe.
10. Editar la tarjeta de perfil, reiniciar el servidor y confirmar que el cambio
    continúa guardado.

Si el hardware permite abrir dos sesiones del cliente, repetir después con dos
cuentas en la misma PC. Si no, la validación de dos humanos queda pendiente,
pero no bloquea la verificación del flujo contra bots.

### Evidencia requerida

Guardar en el diagnóstico del repositorio, sin secretos:

- secuencia de mensajes y cuenta/lobby/match id asociado;
- petición y respuesta de cada handler usado;
- campos que el cliente efectivamente pinta en pantalla;
- errores del cliente o respuestas que sean ignoradas;
- comportamiento después de reconexión y reinicio;
- comprobación de que un `7004` duplicado no duplica partida, Elo ni agregados.

### Resultado esperado

Al terminar esta puerta debe quedar una lista de incompatibilidades reales del
build 6783. No se debe comenzar una ampliación de perfil o showcase basándose
solo en suposiciones del protobuf.

## 5. Fase 7 — perfil y estadísticas ampliadas

**Prioridad: P1. Dependencia: puerta de validación real.**

### 5.1 Estadísticas adicionales del perfil

Investigar y, si el cliente lo solicita, implementar `8034 -> 8035`:

- capturar la petición real y confirmar sus campos;
- confirmar si el cliente espera estadísticas de cuenta, héroe, temporada o
  tarjeta;
- guardar únicamente los valores que tengan una fuente local comprobable;
- responder con límites y orden deterministas;
- mantener cero, vacío o ausencia solo cuando el contrato indique que el dato
  no existe, documentándolo para no confundirlo con un fallo.

La respuesta no debe fabricarse hasta conocer el contrato exacto que usa el
cliente en esta versión.

### 5.2 Proyección enriquecida del perfil

Ampliar el perfil con datos derivados de `Matches` y `MatchPlayers`:

- promedio de K/D/A;
- promedio de GPM, XPM, daño, curación, last hits, denies y net worth;
- última partida y fecha de primera partida;
- héroe más jugado y héroes con mayor tasa de victoria;
- partidas recientes compactas;
- mejor K/D/A, más kills, más daño y mejor GPM/XPM;
- rachas solo si se conserva el orden temporal necesario;
- resultados por modo local y por equipo;
- abandono y partidas incompletas como contadores separados.

La capa de lectura debe evitar recalcular agregados pesados en cada conexión.
Cuando una métrica no pueda calcularse sin ambigüedad, se debe extender la
proyección o dejarla explícitamente sin valor.

### 5.3 Tarjeta de perfil editable

Extender `ProfileCards` y la proyección existente para:

- más slots de estadísticas reales;
- héroes jugados y sus victorias/derrotas;
- mejores partidas con referencia a un `MatchId` válido;
- validación de tipos, límites y duplicados de slots;
- notificación a los clientes cuando una edición deba reflejarse en vivo;
- recuperación consistente después de reiniciar o migrar la base.

Los campos de badge, trofeos, leaderboard, MVP o temporada requieren primero
una fuente persistente; no deben rellenarse con valores de conveniencia.

## 6. Fase 8 — showcase, economía local y ownership

**Estado: showcase, economía local e importación administrativa del catálogo
implementados; validación del cliente real pendiente.**

Los handlers `8886 -> 8887` y `8888 -> 8889` ya persisten y sirven por cuenta
los showcases de perfil y mini perfil. El editor solo puede escribir para la
cuenta autenticada; cualquier cliente puede leer el `AccountId` solicitado.
Los payloads protobuf, posiciones, escala, fondo y versión de formato se
conservan en `Showcases` y sobreviven a reconexiones, reinicios y migraciones.

El servidor ya dispone de un catálogo/ownership local separado del ownership
oficial de Valve. `Wallets`, `WalletTransactions`, `StoreCatalogItems`,
`StoreCatalogComponents` y `EconItems` persisten saldo, compras, sets e
inventario. Cada victoria limpia de un humano acredita `1` dólar local
de forma idempotente; el checkout reserva, debita y entrega los componentes
del producto dentro de transacciones SQLite. La consulta REST está disponible
en `/api/store/catalog`, `/api/store/wallet`, `/api/store/transactions` y
`/api/store/inventory`; el catálogo se administra mediante
`GET/POST /api/admin/store/catalog`. `/api/admin/store/catalog/discover` lee
las definiciones cosméticas del `pak01_dir.vpk` del cliente y
`/api/admin/store/catalog/import` las incorpora con precio base configurable,
conservando precios y activación existentes. La ruta GC cubre ventas, init,
finalize, cancelación y limpieza de transacciones pendientes.

La unidad vigente de la economía local es el dólar ficticio 1:1: `1` en el
saldo o precio representa `$1.00`. La migración
`20260808230000_ConvertLocalCreditsToDollars` convierte los valores históricos
que estaban expresados en unidades menores (`100` por `$1`) y renombra los
campos de persistencia/API a `*Dollars`. En el protocolo nativo se mantienen
las unidades menores de USD, por lo que el servidor traduce `$1` a `100` al
publicar el saldo o el precio al cliente.

La ampliación aún pendiente requiere captura del cliente y catálogo local del
build objetivo:

- héroes mostrados;
- ítems y cosméticos seleccionados;
- trofeos y emblemas;
- fondo de perfil;
- partidas destacadas;
- estadísticas destacadas;
- posición, escala y orden de cada elemento;
- configuración de la tarjeta mini o vista pública.

La implementación de showcase conserva el payload para permitir que el cliente
local lo dibuje. Su validación semántica de ítems/trofeos depende del catálogo
del build y de una captura real; la economía local sí valida catálogo y
ownership en su propio flujo. No se debe enviar ownership de la economía
oficial de Valve si el servidor no posee ese dato.

Esta fase puede requerir entidades separadas para layout, elementos y
propiedad. Debe incluir validación de tamaño de JSON, límites por cuenta y
eliminación segura de elementos que ya no existan.

## 7. Fase 9 — lobbies durables y asociación de servidores

**Prioridad: P0 después de la validación; dependencia: diseño de ciclo de vida.**

Actualmente el resultado final es persistente, pero los lobbies activos y el
índice servidor-lobby permanecen en memoria. Esta fase debe resolver:

### Estado a persistir

- `LobbyId`, propietario y miembros;
- slots, equipos, héroes seleccionados y ready state;
- nombre, contraseña y configuración de la partida;
- modo y región local;
- estado del lobby y timestamps de transición;
- `GameServerId`, endpoint, puerto, proceso y heartbeat;
- vínculo exacto entre lobby, lanzamiento y `MatchId`.

### Comportamiento

- reconstruir lobbies activos después de reiniciar el servidor;
- limpiar lobbies abandonados mediante TTL explícito;
- reconectar un cliente al lobby correcto;
- impedir que dos lanzamientos simultáneos compartan el mismo servidor;
- reservar el servidor antes de publicar `4506`;
- liberar el servidor al cerrar, fallar o expirar la partida;
- correlacionar cada `7034` y `7004` con un único lobby y una única partida;
- mantener idempotentes las operaciones de launch, close y reconnect.

### Verificación

- dos lobbies simultáneos;
- reinicio antes de lanzar;
- reinicio durante una partida;
- cierre duplicado y cierre sin `7004`;
- dos listen servers en puertos distintos;
- reconexión con un lobby en `POSTGAME`.

## 8. Fase 10 — cierre de partida y desconexiones robustas

**Prioridad: P0. Dependencia: asociación durable de lobby/servidor.**

Fortalecer el procesamiento de la partida cuando la red o el listen server no
se comportan idealmente:

- distinguir desconexión temporal, reconexión y abandono definitivo;
- registrar partidas incompletas y separarlas de resultados válidos;
- procesar participantes parciales sin corromper agregados;
- aceptar paquetes fuera de orden sin cambiar un resultado final confirmado;
- registrar un cierre recibido antes de la última actualización `7034`;
- almacenar el estado de procesamiento: recibido, normalizado, persistido y
  publicado;
- reintentar publicación sin repetir la transacción de persistencia;
- guardar la razón técnica de un cierre inválido;
- detectar servidor muerto mediante heartbeat y marcar la partida como
  interrumpida cuando corresponda;
- reconciliar el resultado persistido con el estado visible del lobby.

La penalización de conducta sigue fuera de esta fase: los abandonos se pueden
contabilizar para estadísticas locales, pero el score continúa fijo en 10.000.

## 9. Fase 11 — detalle de partida y analítica local

**Prioridad: P1. Dependencia: cierre robusto.**

El historial actual expone el contrato compacto disponible. Se puede añadir un
detalle local más rico, siempre que el cliente o una herramienta administrativa
lo necesite:

- vista completa de cada jugador y sus objetos;
- posiciones o lanes si el listen server las reporta;
- orden de selección y picks/bans si existe en el payload;
- edificios destruidos y estado final por equipo;
- oro, experiencia, daño, curación, net worth y duración;
- marcas temporales de primera sangre y eventos disponibles;
- diferencia de rango/Elo antes y después, si se guarda por partida;
- exportación JSON/CSV para depuración o panel administrativo;
- promedios por héroe, cuenta, equipo y periodo;
- consultas paginadas y límites para evitar respuestas grandes.

No se debe persistir cada heartbeat `7034` por defecto. Solo se agregará una
línea temporal si el cliente o una herramienta de diagnóstico demuestra que la
necesita, porque su coste de almacenamiento y privacidad es mayor.

## 10. Fase 12 — catálogo de héroes e ítems del build objetivo

**Prioridad: P1. Estado: importación de ítems implementada; dependencia
pendiente: archivos y validación del cliente 6783 en Windows.**

El panel `/admin`, mediante el importador local, ya puede leer
`pak01_dir.vpk/scripts/items/items_game.txt` y `steam.inf`. Para completar y
validar un catálogo coherente con el cliente se debe:

- importar héroes desde los archivos del build objetivo;
- importar ítems, nombres, IDs, tipos e imágenes disponibles localmente;
- versionar el catálogo por build del cliente;
- validar que un ID recibido pertenece a esa versión;
- diferenciar héroe desconocido, ítem desconocido y dato omitido;
- usar el catálogo para validar showcase y tarjeta de perfil;
- exponer héroes sin partidas en listas cuando el contrato lo requiera;
- añadir una tarea administrativa de actualización/reindexación.

Los productos nuevos se importan inactivos por defecto y requieren precio local
antes de ponerse a la venta. Los sets siguen siendo una composición
administrativa de productos importados; el coste de oro de `items_game.txt` no
se usa como precio de la tienda.

El catálogo local no equivale a tener la economía oficial. No se deben crear
inventarios o valores de mercado que el servidor no pueda respaldar.

## 11. Fase 13 — funciones sociales del lobby

**Prioridad: P2. Dependencia: lobbies durables y validación de contratos.**

La base ya contiene parties, chat, amigos y presencia; las ampliaciones
posibles son:

### Party y lobby

- vincular una party existente a un lobby;
- invitar miembros al lobby desde la party;
- conservar la party durante reconexiones;
- transferir ownership de forma explícita;
- impedir que una invitación caducada agregue miembros;
- publicar correctamente los cambios a todos los miembros.

### Chat

- separar chat de party, lobby, partida y postpartida;
- conservar mensajes solo durante el ciclo permitido;
- ordenar mensajes y evitar duplicados;
- controlar tamaño, frecuencia y remitente;
- manejar reconexión sin mezclar canales.

### Visibilidad de perfiles

- consultar el perfil de otro miembro con autorización local;
- publicar tarjeta y estadísticas con el mismo aislamiento de cuenta;
- distinguir propietario, miembro, espectador y usuario no autorizado;
- notificar cambios de tarjeta solo cuando el contrato lo exija.

Los mensajes que no estén confirmados en tráfico real deben capturarse antes de
crear handlers definitivos.

## 12. Fase 14 — espectadores y visualización en vivo

**Prioridad: P2. Dependencia: asociación de servidores y flujo 7034 estable.**

Se puede ampliar el lobby para soportar espectadores locales:

- slots de espectadores y límite configurable;
- invitación o solicitud para observar;
- permisos de propietario y servidor;
- conexión/reconexión al listen server;
- estado de espectadores en `CSODOTALobby` cuando el contrato lo requiera;
- uso coherente de `live_spectator_team`, `live_spectator_account_id` y
  `num_spectators` si el build los consume;
- separación entre espectador del lobby y jugador de la partida;
- cierre de la vista al terminar o invalidar el servidor;
- métricas de conexiones y errores de espectador.

Primero debe confirmarse si el listen server del entorno permite observar y
qué mensajes solicita realmente el cliente.

## 13. Fase 15 — matchmaking local

**Prioridad: P2. Dependencia: lobbies durables, servidor múltiple y reglas de
cola.**

El matchmaking puede existir como servicio local, sin depender de servidores
oficiales de Valve:

- cola por modo, región y tamaño de party;
- entrada, salida y cancelación de cola;
- estado de búsqueda y tiempo transcurrido;
- selección de jugadores compatibles;
- creación automática del lobby;
- invitación o confirmación de los jugadores;
- asignación de un listen server disponible;
- expiración por falta de jugadores;
- reintento si un cliente no acepta;
- aislamiento de cuentas y parties;
- métricas de cola, aceptación y cancelación.

El código generado contiene otras superficies de búsqueda, pero cada una debe
compararse con una captura real antes de implementarse. Entre los candidatos a
auditar están `7033`, `7036`, `8055`, `7413`, `7070` y `7170`; estos IDs son
puntos de investigación, no una garantía de que el cliente los use en el
flujo actual.

El matchmaking local no proporcionará matchmaking oficial, MMR oficial ni
jugadores externos mientras no exista una integración distinta.

## 14. Fase 16 — administración, observabilidad y operación

**Prioridad: P1 para uso continuo; puede hacerse en paralelo después de la
validación básica.**

### Panel o API administrativa

- lobbies activos y su ciclo de vida;
- servidores asociados, heartbeat y puertos;
- partidas, cierres `7004` y estado de procesamiento;
- jugadores, héroes y agregados;
- historial de errores de protocolo;
- migraciones aplicadas y versión de la base;
- búsqueda por `AccountId`, `LobbyId` y `MatchId`;
- exportación controlada de diagnósticos.

### Observabilidad

- logs estructurados por cuenta, lobby, server y match;
- correlación de paquetes de una misma partida;
- métricas de latencia, duplicados, cierres inválidos y reconexiones;
- captura/replay sanitizado para reproducir handlers;
- health checks de API, base y listen servers;
- alarmas locales cuando el servidor queda sin asociaciones o con migración
  pendiente.

### Seguridad operativa

- autorización de endpoints administrativos;
- límites de tamaño y frecuencia por cuenta;
- validación de IDs y ownership local;
- protección contra lobby enumeration;
- aislamiento de datos entre cuentas;
- copias de seguridad antes de migraciones importantes;
- procedimiento documentado de restauración, sin borrar la base por defecto.

## 15. Fase 17 — calidad, compatibilidad y mantenimiento

**Prioridad: transversal.**

Cuando el flujo real tenga cobertura suficiente, se puede añadir:

- pruebas de contrato para serialización/deserialización de mensajes;
- pruebas de idempotencia para `7004`, launch y reconnect;
- pruebas de migración nueva, heredada y actualización incremental;
- pruebas de aislamiento entre cuentas;
- replays de capturas de `7034` y `7004`;
- pruebas de carga de lecturas paginadas;
- matriz por versión de cliente/protobuf;
- verificación de compatibilidad hacia atrás de la base;
- documentación de recuperación ante fallo del servidor.

No se añadirá un proyecto de tests automáticamente si no aporta valor al
entorno actual; por ahora la verificación mínima sigue siendo build Release,
smoke de arranque y cliente Windows real.

La generación de nuevas migraciones es una tarea de desarrollo separada: se
debe habilitar temporalmente `Microsoft.EntityFrameworkCore.Design` o usar un
proyecto de tooling, generar y revisar la migración, y retirar después esa
dependencia antes del despliegue. El servidor en ejecución no necesita Roslyn.

## 16. Ideas futuras separadas del alcance aprobado

Estas capacidades son técnicamente posibles, pero no forman parte del plan
activo y requieren una decisión posterior:

- conducta dinámica con penalización por abandonos;
- reports, commends, moderación y low priority;
- temporadas, leaderboard y rangos oficiales;
- economía, mercado, trades o propiedad sincronizada con Valve (la economía
  local descrita en la Fase 8 no incluye esas funciones);
- matchmaking oficial o conexión a servicios externos de Valve;
- ranked oficial, anti-cheat o validación competitiva;
- cloud profile y sincronización fuera del servidor local;
- guilds, coaching, workshop y otras superficies no relacionadas con el
  vertical de lobby local.

Mientras no se aprueben expresamente, la conducta seguirá enviándose como
política local de score `10.000` y las funciones externas permanecerán fuera
del alcance.

## 17. Orden recomendado consolidado

```text
P0  Validar un cliente Windows contra bots y capturar tráfico real
P0  Repetir con dos clientes en la misma PC si el hardware lo permite
P0  Corregir incompatibilidades encontradas en lobby/perfil/historial
P0  Persistir lobbies activos y asociar cada launch a un servidor único
P0  Robustecer cierres, desconexiones, reconexiones y duplicados
P1  Ampliar perfil, 8034/8035 y estadísticas derivadas
P1  Validar showcase contra catálogo/ownership local y completar variantes
P1  Añadir detalle de partida, analítica y observabilidad
P1  Validar/importar catálogo del build objetivo desde `/admin` en Windows
P2  Integrar party, invitaciones, chat y visibilidad social
P2  Añadir espectadores
P2  Añadir matchmaking local
FUTURO  Conducta dinámica y servicios oficiales/externalizados
```

La siguiente acción aprobada debe comenzar por la primera línea: ejecutar la
partida real contra bots, registrar sus resultados en `HANDOFF.md` y convertir
cada incompatibilidad observada en una tarea concreta.
