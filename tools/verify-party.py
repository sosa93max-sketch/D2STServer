#!/usr/bin/env python3
"""Stage 4f verification: drives the party GC messages over HTTP.

Not part of the build; a scratch harness for checking the party handlers against
a running API without a Dota client. Run the server first, then:

    python3 tools/verify-party.py http://127.0.0.1:5199
"""
import base64
import json
import sys
import urllib.request

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5199"

MSG = {
    4501: "InviteToParty", 4502: "InvitationCreated", 4503: "PartyInviteResponse",
    4504: "KickFromParty", 4505: "LeaveParty", 4006: "ClientHello", 4004: "ClientWelcome",
    4009: "ConnectionStatus", 21: "SOCreate", 22: "SOUpdate", 23: "SODestroy",
    24: "SOCacheSubscribed", 25: "SOCacheUnsubscribed", 8262: "ReadyCheckRequest",
    8263: "ReadyCheckResponse", 8264: "ReadyCheckAck", 7588: "SetPartyLeader",
}


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


def varint_field(number, value):
    return field(number, 0, varint(value))


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


def poll(token):
    """Everything the server pushed to this player, over both channels: a live
    client is fed gc_message events, the poll queue holds the rest."""
    response = post("/api/gamecoordinator/poll", {"AppId": 570, "SteamId": 0, "GameServer": False}, token)
    messages = [(m["MessageType"], base64.b64decode(m["PayloadBase64"])) for m in response["Messages"]]

    envelope = get("/api/events?since=%s&waitMs=0" % CURSORS.get(token, "0"), token)
    CURSORS[token] = envelope.get("Cursor", CURSORS.get(token, "0"))
    return messages + [(event["MessageType"], base64.b64decode(event["PayloadBase64"]))
                       for event in envelope.get("Events", []) if event["Type"] == "gc_message"]


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


def subscribed_object(messages):
    """The single object carried by the first SOCacheSubscribed of a batch.

    The batch is not only Shared Objects: a player logging on is also announced
    to the default chat channels it shares with the others.
    """
    message = next(entry for entry in messages if entry[0] == 24)
    return decode(decode(decode(message[1])[2][0])[2][0])


checks = []


def check(label, condition, detail=""):
    checks.append(condition)
    print(("PASS " if condition else "FAIL ") + label + ((" -- " + str(detail)) if detail else ""))


alice_token, alice = logon(90001, "Alice")
bob_token, bob = logon(90002, "Bob")
carol_token, carol = logon(90003, "Carol")

for token in (alice_token, bob_token, carol_token):
    exchange(token, 4006, varint_field(1, 6783))
    poll(token)

# invite bob
handled, replies = exchange(alice_token, 4501, fixed64(1, bob))
created = decode(replies[0][1])
check("4501 answers 4502", handled and replies[0][0] == 4502, names(replies))
check("invite names a group", created.get(1, [0])[0] != 0, created)
check("target is not reported offline", 3 not in created, created)

alice_pushed = poll(alice_token)
check("inviter is subscribed to the party cache", ("SOCacheSubscribed", 2, created[1][0]) in owners(alice_pushed), owners(alice_pushed))

bob_pushed = poll(bob_token)
invite_caches = [o for o in owners(bob_pushed) if o[1] == 4]
check("invitee receives the invite cache", len(invite_caches) == 1, owners(bob_pushed))
invite_fields = subscribed_object(bob_pushed)
check("invite carries the sender's name", invite_fields.get(3, [b""])[0] == b"Alice", invite_fields)

# bob accepts
exchange(bob_token, 4503, varint_field(1, created[1][0]) + varint_field(2, 1))
check("party update reaches the inviter", ("SOUpdate", 2, created[1][0]) in owners(poll(alice_token)))
bob_pushed = poll(bob_token)
check("invitee is unsubscribed from the invite and subscribed to the party",
      ("SOCacheUnsubscribed", 4, invite_fields[8][0]) in owners(bob_pushed)
      and ("SOCacheSubscribed", 2, created[1][0]) in owners(bob_pushed), owners(bob_pushed))

party = get("/api/gamecoordinator/party", alice_token)
check("party holds both members", party["MemberSteamIds"] == [alice, bob], party)
check("leader is the inviter", party["LeaderSteamId"] == alice, party)

# a reconnect re-publishes the party in the welcome
_, welcome = exchange(alice_token, 4006, varint_field(1, 6783))
welcome_caches = [decode(cache) for cache in decode(welcome[1][1]).get(3, [])]
party_caches = [cache for cache in welcome_caches if decode(cache[4][0])[1][0] == 2]
check("welcome carries the party cache", len(party_caches) == 1, len(welcome_caches))
poll(alice_token)
poll(bob_token)

# ready check
handled, replies = exchange(alice_token, 8262)
check("8262 answers 8263 with success", replies[0][0] == 8263 and decode(replies[0][1]).get(1, [0])[0] == 0, replies)
check("ready check reaches the other member", ("SOUpdate", 2, created[1][0]) in owners(poll(bob_token)))
_, replies = exchange(alice_token, 8262)
check("a second ready check is refused", decode(replies[0][1]).get(1, [0])[0] == 1, replies)
exchange(bob_token, 8264, varint_field(1, 2))
check("the answer reaches the initiator", ("SOUpdate", 2, created[1][0]) in owners(poll(alice_token)))
check("the party records who is ready",
      get("/api/gamecoordinator/party", alice_token)["ReadyCheckFinishTimestamp"] > 0)
poll(bob_token)

# third member, then the leader leaves
exchange(alice_token, 4501, fixed64(1, carol))
carol_invite = subscribed_object(poll(carol_token))
exchange(carol_token, 4503, varint_field(1, carol_invite[1][0]) + varint_field(2, 1))
check("party holds three members",
      get("/api/gamecoordinator/party", bob_token)["MemberSteamIds"] == [alice, bob, carol])
poll(alice_token)
poll(bob_token)
poll(carol_token)

exchange(alice_token, 4505)
check("the leaver is unsubscribed", ("SOCacheUnsubscribed", 2, created[1][0]) in owners(poll(alice_token)))
after = get("/api/gamecoordinator/party", bob_token)
check("the party survives with a new leader", after["MemberSteamIds"] == [bob, carol] and after["LeaderSteamId"] == bob, after)
check("the leaver has no party", get("/api/gamecoordinator/party", alice_token).get("status") == 404)
check("remaining members see the update", ("SOUpdate", 2, created[1][0]) in owners(poll(bob_token)))

# kicking the last other member disbands the two-player party
poll(carol_token)
exchange(bob_token, 4504, fixed64(1, carol))
check("the party is gone", get("/api/gamecoordinator/party", bob_token).get("status") == 404)
check("both are unsubscribed",
      ("SOCacheUnsubscribed", 2, created[1][0]) in owners(poll(bob_token))
      and ("SOCacheUnsubscribed", 2, created[1][0]) in owners(poll(carol_token)))

# a kick from a non-leader, and an invite to yourself, change nothing
exchange(alice_token, 4501, fixed64(1, bob))
bob_invite = subscribed_object(poll(bob_token))
exchange(bob_token, 4503, varint_field(1, bob_invite[1][0]) + varint_field(2, 1))
poll(alice_token)
poll(bob_token)
exchange(bob_token, 4504, fixed64(1, alice))
check("a member cannot kick the leader",
      get("/api/gamecoordinator/party", alice_token)["MemberSteamIds"] == [alice, bob])
check("kicking published nothing", poll(alice_token) == [])
_, replies = exchange(alice_token, 4501, fixed64(1, alice))
check("inviting yourself is refused", decode(replies[0][1]).get(3, [0])[0] == 1, replies)
exchange(alice_token, 7588, fixed64(1, bob))
check("the leader can hand the party over",
      get("/api/gamecoordinator/party", alice_token)["LeaderSteamId"] == bob)

print("\n%d/%d checks passed" % (sum(checks), len(checks)))
sys.exit(0 if all(checks) else 1)
