# D2MAX Store

La tienda de consumo vive dentro de `new_launcher`. Ya no se publica una página
`/store` ni se usa una cookie o un handoff de navegador: el launcher envía el
mismo bearer token que recibió al iniciar sesión en cada llamada. Así la cuenta
que ve el catálogo, el saldo y el inventario es exactamente la cuenta activa del
launcher.

El servidor conserva únicamente la API autenticada y la administración del
catálogo. `ProductId`, ids de inventario y cuenta se resuelven siempre desde la
sesión bearer; el cliente nunca puede cambiar el propietario de una compra.

## Store APIs

- `GET /api/store/catalog` — active products and owned quantities.
- `GET /api/store/catalog/page?page=&pageSize=&search=&category=&hero=&type=` —
  paginated active catalog with server-side search and filters. The response
  also returns the available category and hero filter values.
- `GET /api/store/wallet` — balance, reservations and available balance.
- `GET /api/store/inventory` — durable `CSOEconItem` projection.
- `POST /api/store/purchase` — atomic begin/finalize purchase and inventory grant.
- `POST /api/store/inventory/equip` — validates ownership before publishing an
  equip delta for a supplied hero and slot.
- `GET /api/store/transactions` — wallet and purchase activity.

After a successful REST purchase, the item is persisted and published to the
connected client through the econ Shared Object cache. The server then pushes a
complete account snapshot to repair a missed delta, so the client should render
the item without restarting Dota. A reconnect rebuilds the same inventory from
SQLite.

## Catalog preparation

Run `/admin`, use the Catalog workspace to discover the target Dota installation
and choose the visible localization (Spanish is the default). The importer keeps
the localized display name for the store and the English name separately for
Steam Market matching. The server must be able to read the target
`pak01_dir.vpk`; a remote browser cannot read a VPK from an administrator's PC by
path alone.

The import form has two deliberate modes:

- A normal import is idempotent. Item definitions use `DefIndex` as their stable
  identity, so importing the same VPK repeatedly updates one row and never
  creates duplicate products. Existing local activation, price and synchronized
  market fields are retained.
- `Vaciar antes de importar` first calls the administrator-only clear operation
  and then imports the validated source with the requested default price and
  activation state. `POST /api/admin/store/catalog/clear` removes catalog rows
  and components but never deletes a user's `EconItems` inventory.

The import fallback is `0`, not `$1`. After every import the server queues all
imported item ids for a background Steam Market refresh in batches of 500, so the
HTTP request does not time out on a full Dota catalog. Matched products keep the
exact Steam lowest/median values in cents and the consumer store displays the
available reference as `$X.XX`. Items with no exact market match or no current
listing are cleared to zero/inactive rather than being made purchasable at a
fake price. The manual `Actualizar precios reales Steam` action remains useful
for rechecking stale products.

The current local wallet and purchase ledger intentionally remain whole-dollar
units. The native GC sales response uses the verified Steam cents when present,
while the applied local checkout amount is rounded up to the next dollar and is
shown separately as `Saldo requerido`. This keeps the existing wallet and GC
contracts compatible and does not represent a Steam Wallet charge or official
Valve ownership.

When the launcher logs off, `/api/presence/offline` revokes the active bearer
session. The embedded store receives the same token and therefore becomes
unavailable immediately; a later login supplies a new token without any browser
state to clean up.
