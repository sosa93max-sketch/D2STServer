# D2MAX Store

The consumer store is served by the same D2STServer process at `/store`.
It uses the local catalog and wallet; it does not charge Steam Wallet or claim
official Valve ownership.

## Launcher handoff

The launcher calls `POST /api/store/handoff` with the active profile's bearer
token. The server returns a relative `/store?ticket=...` path. The ticket is
short-lived and single-use. The store page exchanges it with
`POST /api/store/handoff/exchange`, which sets the `d2st_store_session` HttpOnly
cookie scoped to `/api/store`.

The permanent launcher token is never included in the URL. The store account is
always resolved from the server session, so `ProductId`, item ids and account
ids supplied by a browser cannot change the owner of a purchase.

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
- `POST /api/store/logout` — clears the browser store cookie.

After a successful REST purchase, the item is persisted and published to the
connected client through the econ Shared Object cache. A reconnect rebuilds the
same inventory from SQLite.

## Catalog preparation

Run `/admin`, use the Catalog workspace to discover the target Dota installation,
assign local prices and activate the products intended for the current client
build. New imports are inactive by default. The server must be able to read the
target `pak01_dir.vpk`; a remote browser cannot read a VPK from an administrator's
PC by path alone.

The import form has two deliberate modes:

- A normal import is idempotent. Item definitions use `DefIndex` as their stable
  identity, so importing the same VPK repeatedly updates one row and never
  creates duplicate products. Existing local activation, price and synchronized
  market fields are retained.
- `Vaciar antes de importar` first calls the administrator-only clear operation
  and then imports the validated source with the requested default price and
  activation state. `POST /api/admin/store/catalog/clear` removes catalog rows
  and components but never deletes a user's `EconItems` inventory.

For real reference prices, run `Actualizar precios reales Steam` after the
import. Matched products keep the exact Steam lowest/median values in cents and
the consumer store displays the lowest value as `$X.XX`. The current local
wallet and native protocol intentionally remain whole-dollar units; the applied
checkout price is rounded up to the next dollar and is shown separately as
`Saldo requerido`. This avoids undercharging while keeping compatibility with
the existing wallet and GC contracts.

When the launcher logs off, `/api/presence/offline` revokes the same server
session used by the store cookie and expires that cookie. The store page checks
`/api/store/session` periodically, so an open tab changes to a signed-out state
without requiring a manual refresh.
