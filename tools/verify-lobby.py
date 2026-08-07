#!/usr/bin/env python3
"""Stage 4g verification: drives the practice lobby GC messages over HTTP.

Not part of the build; a scratch harness for checking the lobby handlers against
a running API without a Dota client. Run the server first, then:

    python3 tools/verify-lobby.py http://127.0.0.1:5199
"""
import base64
import json
import sys
import urllib.request

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5199"

MSG = {
    4006: "ClientHello", 4004: "ClientWelcome", 4009: "ConnectionStatus",
    4007: "GameServerHello", 4005: "ServerWelcome",
    21: "SOCreate", 22: "SOUpdate", 23: "SODestroy",
    24: "SOCacheSubscribed", 25: "SOCacheUnsubscribed",
    7038: "PracticeLobbyCreate", 7040: "PracticeLobbyLeave", 7041: "PracticeLobbyLaunch",
    7042: "PracticeLobbyList", 7043: "PracticeLobbyListResponse", 7044: "PracticeLobbyJoin",
    7046: "PracticeLobbySetDetails", 7047: "PracticeLobbySetTeamSlot",
    7055: "PracticeLobbyResponse", 7081: "PracticeLobbyKick",
    7113: "PracticeLobbyJoinResponse", 8047: "PracticeLobbyKickFromTeam",
    2579: "GCGenericResult", 4506: "ServerAvailable", 4508: "GameServerInfo",
    4511: "LANServerAvailable", 7034: "ConnectedPlayers", 7088: "PlayerFailedToConnect",
}

RADIANT, DIRE, PLAYER_POOL = 0, 1, 4


def post(path, body, token=None):
    request = urllib.request.Request(
        BASE + path,
        data=json.dumps(body).encode(),
        headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + token} if token else {})},
        method="POST")
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read() or b"{}")


def get(path, token):
    request = urllib.request.Request(BASE + path, headers={"Authorization": "Bearer " + token})
    try:
        with urllib.request.urlopen(request) as response:
            return json.loads(response.read() or b"{}")
    except urllib.error.HTTPError as error:
        return {"status": error.code}


# --- minimal protobuf wire codec -------------------------------------------
def varint(value):
    out = b""
    while True:
        byte = value & 0x7F
        value >>= 7
        out += bytes([byte | (0x80 if value else 0)])
        if not value:
            return out


def field(number, wire, payload):
    return varint((number << 3) | wire) + payload


def fixed64(number, value):
    return field(number, 1, value.to_bytes(8, "little"))


def fixed32(number, value):
    return field(number, 5, value.to_bytes(4, "little"))


def varint_field(number, value):
    return field(number, 0, varint(value))


def bytes_field(number, value):
    return field(number, 2, varint(len(value)) + value)


def string_field(number, value):
    return bytes_field(number, value.encode())


def decode(buffer):
    """Returns {field_number: [values]} with bytes for length-delimited fields."""
    fields, index = {}, 0
    while index < len(buffer):
        key, index = read_varint(buffer, index)
        number, wire = key >> 3, key & 7
        if wire == 0:
            value, index = read_varint(buffer, index)
        elif wire == 1:
            value, index = int.from_bytes(buffer[index:index + 8], "little"), index + 8
        elif wire == 2:
            length, index = read_varint(buffer, index)
            value, index = buffer[index:index + length], index + length
        elif wire == 5:
            value, index = int.from_bytes(buffer[index:index + 4], "little"), index + 4
        else:
            raise ValueError("wire type %d" % wire)
        fields.setdefault(number, []).append(value)
    return fields


def read_varint(buffer, index):
    result, shift = 0, 0
    while True:
        byte = buffer[index]
        index += 1
        result |= (byte & 0x7F) << shift
        if not byte & 0x80:
            return result, index
        shift += 7


# --- server helpers ---------------------------------------------------------
# Event stream cursor per token: the client channel the shim really drains.
CURSORS = {}


def logon(account_id, name):
    response = post("/api/auth/steam/session", {
        "AccountId": account_id, "SteamId": 0, "AppId": 570, "PersonaName": name,
        "ClientInstanceId": name, "ProcessRole": "client", "UseActiveWebUser": False})
    token = response["AccessToken"]
    # Start where the event stream is now: a server that already ran this
    # script still holds the previous run's events for this account.
    CURSORS[token] = get("/api/events?since=0&waitMs=0", token).get("Cursor", "0")
    return token, response["User"]["SteamId"]


def exchange(token, message_type, body=b"", steam_id=0):
    response = post("/api/gamecoordinator/exchange", {
        "AppId": 570, "MessageType": message_type, "BodyBase64": base64.b64encode(body).decode(),
        "SourceJobId": 7, "SteamId": steam_id, "GameServer": False}, token)
    return response["Handled"], [(m["MessageType"], base64.b64decode(m["PayloadBase64"])) for m in response["Messages"]]


def poll_queue(token):
    """The GC poll channel, which only a dedicated server drains in the shim."""
    response = post("/api/gamecoordinator/poll", {"AppId": 570, "SteamId": 0, "GameServer": False}, token)
    return [(m["MessageType"], base64.b64decode(m["PayloadBase64"])) for m in response["Messages"]]


def poll_events(token):
    """The gc_message events, which is how a live client is really fed."""
    envelope = get("/api/events?since=%s&waitMs=0" % CURSORS.get(token, "0"), token)
    CURSORS[token] = envelope.get("Cursor", CURSORS.get(token, "0"))
    return [(event["MessageType"], base64.b64decode(event["PayloadBase64"]))
            for event in envelope.get("Events", []) if event["Type"] == "gc_message"]


def poll(token):
    """Everything the server pushed to this player, over both channels."""
    return poll_queue(token) + poll_events(token)


def names(messages):
    return [MSG.get(t, str(t)) for t, _ in messages]


# owner_soid sits on a different field in each SO message.
OWNER_FIELD = {24: 4, 25: 2, 21: 5, 22: 5, 23: 5}


def owners(messages):
    """(message name, owner type, owner id) of every SO message in the batch."""
    out = []
    for message_type, body in messages:
        soid = decode(body).get(OWNER_FIELD.get(message_type, 0), [])
        owner = decode(soid[0]) if soid else {}
        out.append((MSG.get(message_type, str(message_type)),
                    owner.get(1, [0])[0], owner.get(2, [0])[0]))
    return out


def member(lobby, steam_id):
    return next((m for m in lobby["Members"] if m["SteamId"] == steam_id), None)


def result_of(replies):
    return decode(replies[0][1]).get(1, [0])[0]


checks = []


def check(label, condition, detail=""):
    checks.append(condition)
    print(("PASS " if condition else "FAIL ") + label + ((" -- " + str(detail)) if detail else ""))


host_token, host = logon(90101, "Host")
guest_token, guest = logon(90102, "Guest")
third_token, third = logon(90103, "Third")

for token in (host_token, guest_token, third_token):
    exchange(token, 4006, varint_field(1, 6783))
    poll(token)

# create a lobby with a pass key, a name and a region
details = string_field(2, "Devin test lobby") + varint_field(4, 3) + varint_field(5, 2)
handled, replies = exchange(host_token, 7038, string_field(5, "secret") + bytes_field(7, details))
check("7038 answers 7055 with success", handled and replies[0][0] == 7055 and result_of(replies) == 0, names(replies))

lobby = get("/api/gamecoordinator/lobby", host_token)
check("the lobby holds its host on Radiant slot 1",
      member(lobby, host) == {"SteamId": host, "Name": "Host", "Team": RADIANT, "Slot": 1}, lobby)
check("the details reached the object",
      (lobby["GameName"], lobby["ServerRegion"], lobby["GameMode"], lobby["RequiresPassKey"])
      == ("Devin test lobby", 3, 2, True), lobby)

lobby_id = lobby["LobbyId"]
queued, host_pushed = poll_queue(host_token), poll_events(host_token)
check("the host is subscribed to the lobby cache",
      ("SOCacheSubscribed", 3, lobby_id) in owners(host_pushed), owners(host_pushed))
check("a live client is fed through the event stream, not the poll queue", queued == [], queued)

# the browser lists it
_, replies = exchange(guest_token, 7042, b"")
listed = [decode(entry) for entry in decode(replies[0][1]).get(2, [])]
mine = [entry for entry in listed if entry.get(1, [0])[0] == lobby_id]
check("7042 answers 7043 with the lobby", replies[0][0] == 7043 and len(mine) == 1, names(replies))
check("the entry carries name, host and pass key flag",
      mine and mine[0][10][0] == b"Devin test lobby" and mine[0].get(6, [0])[0] == 1, mine)

# joining
_, replies = exchange(guest_token, 7044, varint_field(1, lobby_id) + string_field(3, "wrong"))
check("a wrong pass key is refused", replies[0][0] == 7113 and result_of(replies) == 3, replies)
_, replies = exchange(guest_token, 7044, varint_field(1, 1) + string_field(3, "secret"))
check("an unknown lobby is refused", result_of(replies) == 2, replies)

_, replies = exchange(guest_token, 7044, varint_field(1, lobby_id) + string_field(3, "secret"))
check("7044 answers 7113 with success", replies[0][0] == 7113 and result_of(replies) == 0, replies)
check("the joiner is subscribed to the lobby cache",
      ("SOCacheSubscribed", 3, lobby_id) in owners(poll(guest_token)))
check("the host sees the update", ("SOUpdate", 3, lobby_id) in owners(poll(host_token)))
check("the joiner took the next Radiant slot",
      member(get("/api/gamecoordinator/lobby", host_token), guest)["Slot"] == 2)

# team slots
exchange(guest_token, 7047, varint_field(1, DIRE) + varint_field(2, 3))
check("a member can pick a Dire slot",
      member(get("/api/gamecoordinator/lobby", host_token), guest) ==
      {"SteamId": guest, "Name": "Guest", "Team": DIRE, "Slot": 3})
poll(host_token)
poll(guest_token)

exchange(third_token, 7044, varint_field(1, lobby_id) + string_field(3, "secret"))
poll(host_token)
poll(guest_token)
poll(third_token)
exchange(third_token, 7047, varint_field(1, DIRE) + varint_field(2, 3))
check("a taken slot is refused",
      member(get("/api/gamecoordinator/lobby", host_token), third)["Team"] == RADIANT)
check("refusing published nothing", poll(guest_token) == [])

# only the host may change the settings
exchange(guest_token, 7046, string_field(2, "Hijacked"))
check("a member cannot rename the lobby",
      get("/api/gamecoordinator/lobby", host_token)["GameName"] == "Devin test lobby")
exchange(host_token, 7046, string_field(2, "Renamed"))
check("the host can rename the lobby",
      get("/api/gamecoordinator/lobby", host_token)["GameName"] == "Renamed")
check("the rename reaches the members", ("SOUpdate", 3, lobby_id) in owners(poll(guest_token)))
poll(host_token)
poll(third_token)

# kick from team leaves the player in the lobby, a kick removes it
exchange(host_token, 8047, varint_field(1, 90103))
kicked = member(get("/api/gamecoordinator/lobby", host_token), third)
check("kick from team moves the player to the pool", kicked["Team"] == PLAYER_POOL and kicked["Slot"] == 0, kicked)
exchange(guest_token, 7081, field(3, 0, varint(90103)))
check("a member cannot kick", member(get("/api/gamecoordinator/lobby", host_token), third) is not None)
exchange(host_token, 7081, field(3, 0, varint(90103)))
check("the host can kick", member(get("/api/gamecoordinator/lobby", host_token), third) is None)
check("the kicked player is unsubscribed",
      ("SOCacheUnsubscribed", 3, lobby_id) in owners(poll(third_token)))
check("the kicked player has no lobby", get("/api/gamecoordinator/lobby", third_token).get("status") == 404)
poll(host_token)
poll(guest_token)

# a reconnect gets the lobby back in its welcome
_, welcome = exchange(guest_token, 4006, varint_field(1, 6783))
welcome_caches = [decode(cache) for cache in decode(welcome[1][1]).get(3, [])]
lobby_caches = [cache for cache in welcome_caches if decode(cache[4][0])[1][0] == 3]
check("the welcome carries the lobby cache", len(lobby_caches) == 1, len(welcome_caches))
poll(guest_token)

# launching: the host gets the generic-result reply and the lobby goes to
# SERVERSETUP with no server yet, then the game server announces itself
_, replies = exchange(guest_token, 7041)
check("a member cannot launch", get("/api/gamecoordinator/lobby", host_token)["State"] == "Ui")
check("a member launch answers failure", replies[0][0] == 2579 and result_of(replies) == 0, replies)
_, replies = exchange(host_token, 7041)
lobby = get("/api/gamecoordinator/lobby", host_token)
check("the host launches the lobby", lobby["State"] == "Serversetup", lobby)
check("7041 answers the generic result with success",
      replies[0][0] == 2579 and result_of(replies) == 1, names(replies))
check("a launched lobby has no server yet", lobby["Connect"] == "" and lobby["ServerId"] == 0, lobby)
check("the launch reaches the members", ("SOUpdate", 3, lobby_id) in owners(poll(guest_token)))
_, replies = exchange(third_token, 7044, varint_field(1, lobby_id) + string_field(3, "secret"))
check("a launched lobby cannot be joined", result_of(replies) == 1, replies)

# the host's local listen server reports its address (127.0.0.1:27015) and
# availability; the lobby now carries the connect string the members dial
exchange(host_token, 4508, fixed32(2, 0x0100007F) + varint_field(3, 27015))
lobby = get("/api/gamecoordinator/lobby", host_token)
check("game server info installs the connect string",
      lobby["State"] == "Serversetup" and lobby["Connect"] == "127.0.0.1:27015", lobby)
exchange(host_token, 4511, fixed64(1, lobby_id))
lobby = get("/api/gamecoordinator/lobby", host_token)
check("the LAN server is attached", lobby["ServerId"] == host and lobby["Connect"] == "127.0.0.1:27015", lobby)
check("the connect string reaches the members", ("SOUpdate", 3, lobby_id) in owners(poll(guest_token)))

# the server is up: the lobby runs, gets a match id and a start time
exchange(host_token, 4506)
lobby = get("/api/gamecoordinator/lobby", host_token)
check("the server start moves the lobby to RUN",
      lobby["State"] == "Run" and lobby["MatchId"] == lobby_id and lobby["GameStartTime"] > 0, lobby)
check("the running lobby reaches the members", ("SOUpdate", 3, lobby_id) in owners(poll(guest_token)))

# the server reports who is in: the game state follows the server
exchange(host_token, 7034, varint_field(2, 1) + varint_field(8, 2)
        + bytes_field(1, fixed64(1, guest) + varint_field(2, 10)))
lobby = get("/api/gamecoordinator/lobby", host_token)
check("connected players update the game state", lobby["GameState"] == 1, lobby)

# a player who failed to load aborts the launch back to the UI state
exchange(host_token, 7088, bytes_field(1, guest.to_bytes(8, "little")))
check("a failed loader returns the lobby to the UI state",
      get("/api/gamecoordinator/lobby", host_token)["State"] == "Ui")
poll(host_token)

# the host leaving closes the lobby
exchange(host_token, 7040)
check("the lobby is gone", get("/api/gamecoordinator/lobby", host_token).get("status") == 404)
check("everybody is unsubscribed",
      ("SOCacheUnsubscribed", 3, lobby_id) in owners(poll(host_token))
      and ("SOCacheUnsubscribed", 3, lobby_id) in owners(poll(guest_token)))

# a member leaving does not
exchange(host_token, 7038, b"")
second_id = get("/api/gamecoordinator/lobby", host_token)["LobbyId"]
exchange(guest_token, 7044, varint_field(1, second_id))
exchange(third_token, 7044, varint_field(1, second_id))
poll(host_token)
poll(guest_token)
poll(third_token)
exchange(guest_token, 7040)
survivors = get("/api/gamecoordinator/lobby", host_token)
check("the lobby survives a member leaving",
      [m["SteamId"] for m in survivors["Members"]] == [host, third], survivors)
check("the leaver is unsubscribed", ("SOCacheUnsubscribed", 3, second_id) in owners(poll(guest_token)))
check("the leaver has no lobby", get("/api/gamecoordinator/lobby", guest_token).get("status") == 404)
check("the remaining members see the update", ("SOUpdate", 3, second_id) in owners(poll(third_token)))

print("\n%d/%d checks passed" % (sum(checks), len(checks)))
sys.exit(0 if all(checks) else 1)
