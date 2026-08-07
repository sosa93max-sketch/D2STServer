#!/usr/bin/env python3
"""Stage 4h verification: drives the chat GC messages over HTTP.

Not part of the build; a scratch harness for checking the chat handlers against
a running API without a Dota client. Needs a freshly started server (channels
are in memory and a previous run leaves its members behind). Run:

    python3 tools/verify-chat.py http://127.0.0.1:5199
"""
import base64
import json
import sys
import urllib.error
import urllib.request

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5199"

MSG = {
    4006: "ClientHello", 4004: "ClientWelcome", 4009: "ConnectionStatus",
    7009: "JoinChatChannel", 7010: "JoinChatChannelResponse",
    7013: "OtherJoinedChannel", 7014: "OtherLeftChannel",
    7060: "RequestChatChannelList", 7061: "RequestChatChannelListResponse",
    7272: "LeaveChatChannel", 7273: "ChatMessage",
    7403: "ChatGetUserList", 7404: "ChatGetUserListResponse",
    8048: "ChatGetMemberCount", 8049: "ChatGetMemberCountResponse",
    8084: "PrivateChatInvite", 8088: "PrivateChatKick", 8089: "PrivateChatPromote",
    8090: "PrivateChatDemote", 8091: "PrivateChatResponse",
    8092: "PrivateChatInfoRequest", 8093: "PrivateChatInfoResponse",
}

REGIONAL, CUSTOM, PRIVATE = 0, 1, 17


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


def bytes_field(number, value):
    return field(number, 2, varint(len(value)) + value)


def string_field(number, value):
    return bytes_field(number, value.encode())


def decode(buffer):
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
CURSORS = {}


def logon(account_id, name):
    response = post("/api/auth/steam/session", {
        "AccountId": account_id, "SteamId": 0, "AppId": 570, "PersonaName": name,
        "ClientInstanceId": name, "ProcessRole": "client", "UseActiveWebUser": False})
    token = response["AccessToken"]
    CURSORS[token] = get("/api/events?since=0&waitMs=0", token).get("Cursor", "0")
    return token, response["User"]["SteamId"]


def exchange(token, message_type, body=b""):
    response = post("/api/gamecoordinator/exchange", {
        "AppId": 570, "MessageType": message_type, "BodyBase64": base64.b64encode(body).decode(),
        "SourceJobId": 7, "SteamId": 0, "GameServer": False}, token)
    return response["Handled"], [(m["MessageType"], base64.b64decode(m["PayloadBase64"])) for m in response["Messages"]]


def poll_queue(token):
    response = post("/api/gamecoordinator/poll", {"AppId": 570, "SteamId": 0, "GameServer": False}, token)
    return [(m["MessageType"], base64.b64decode(m["PayloadBase64"])) for m in response["Messages"]]


def poll_events(token):
    envelope = get("/api/events?since=%s&waitMs=0" % CURSORS.get(token, "0"), token)
    CURSORS[token] = envelope.get("Cursor", CURSORS.get(token, "0"))
    return [(event["MessageType"], base64.b64decode(event["PayloadBase64"]))
            for event in envelope.get("Events", []) if event["Type"] == "gc_message"]


def poll(token):
    return poll_queue(token) + poll_events(token)


def names(messages):
    return [MSG.get(t, str(t)) for t, _ in messages]


def of_type(messages, message_type):
    return [body for kind, body in messages if kind == message_type]


checks = []


def check(label, condition, detail=""):
    checks.append(condition)
    print(("PASS " if condition else "FAIL ") + label + ((" -- " + str(detail)) if detail else ""))


alice_token, alice = logon(90301, "Alice")
bob_token, bob = logon(90302, "Bob")

# --- default channels -------------------------------------------------------
exchange(alice_token, 4006, varint_field(1, 3756))
# The auto-join is pushed, not returned: a live client is fed through the event
# stream, which is the channel the shim's event pump drains.
joins = of_type(poll(alice_token), 7010)
check("the welcome puts the client in the auto-join channel", len(joins) == 1)
if joins:
    join = decode(joins[0])
    check("the auto-join reply names the configured channel", join.get(2, [b""])[0] == b"D2MAX", join)
    check("the auto-join reply is marked gc_initiated", join.get(8, [0])[0] == 1, join)
    check("the auto-join reply carries the welcome message", b"D2MAX" in join.get(10, [b""])[0], join)
channel_id = decode(joins[0]).get(3, [0])[0] if joins else 0

exchange(bob_token, 4006, varint_field(1, 3756))
poll(bob_token)

handled, replies = exchange(alice_token, 7060, b"")
listed = decode(replies[0][1]).get(1, []) if replies else []
channels = [(decode(entry).get(1, [b""])[0].decode(), decode(entry).get(2, [0])[0]) for entry in listed]
check("7060 lists the three configured channels",
      handled and [name for name, _ in channels] == ["D2MAX", "Trade", "LFG"], channels)
check("the list counts the members of each channel",
      dict(channels).get("D2MAX") == 2 and dict(channels).get("Trade") == 0, channels)


# --- joining, chatting, leaving --------------------------------------------
poll(alice_token)
handled, replies = exchange(bob_token, 7009, string_field(2, "Trade") + varint_field(4, CUSTOM))
join = decode(replies[0][1])
check("7009 answers 7010 with JOIN_SUCCESS", handled and replies[0][0] == 7010 and join.get(1, [0])[0] == 0, join)
trade_id = join.get(3, [0])[0]
check("the joiner is the only member of the channel it opened", len(join.get(5, [])) == 1, join)
check("nobody else hears about a channel they are not in", poll(alice_token) == [])

handled, replies = exchange(alice_token, 7009, string_field(2, "Trade") + varint_field(4, CUSTOM))
check("a second player joining is told about the first",
      len(decode(replies[0][1]).get(5, [])) == 2, decode(replies[0][1]))
others = poll(bob_token)
joined = decode(of_type(others, 7013)[0]) if of_type(others, 7013) else {}
check("the member already there gets 7013 naming the joiner",
      joined.get(2, [b""])[0] == b"Alice" and joined.get(1, [0])[0] == trade_id, names(others))

handled, replies = exchange(alice_token, 7273, varint_field(2, trade_id) + string_field(4, "hello"))
check("7273 is not answered to its sender", handled and replies == [], names(replies))
lines = of_type(poll(bob_token), 7273)
body = decode(lines[0]) if lines else {}
check("the chat line reaches the other member",
      body.get(4, [b""])[0] == b"hello" and body.get(3, [b""])[0] == b"Alice", body)
check("the server stamps the author and a timestamp on the line",
      body.get(1, [0])[0] != 0 and body.get(5, [0])[0] != 0, body)
check("the sender gets no copy of its own line, which its client already drew",
      of_type(poll(alice_token), 7273) == [])

exchange(bob_token, 7273, varint_field(2, channel_id + 9999) + string_field(4, "nowhere"))
check("a line in a channel that does not exist publishes nothing", poll(alice_token) == [])

handled, replies = exchange(alice_token, 7403, fixed64(1, trade_id))
members = decode(replies[0][1]).get(2, [])
check("7403 answers 7404 with both members", handled and len(members) == 2, names(replies))

handled, replies = exchange(alice_token, 8048, string_field(1, "Trade") + varint_field(2, CUSTOM))
count = decode(replies[0][1])
check("8048 answers 8049 with the member count", handled and count.get(3, [0])[0] == 2, count)

handled, replies = exchange(alice_token, 7272, varint_field(1, trade_id))
check("7272 is not answered", handled and replies == [], names(replies))
left = of_type(poll(bob_token), 7014)
check("the remaining member gets 7014 naming the leaver",
      left and decode(left[0]).get(2, [0])[0] == alice, names(poll(bob_token)))

# --- a channel nobody configured dies with its last member ------------------
exchange(bob_token, 7009, string_field(2, "Scratch") + varint_field(4, CUSTOM))
listed = decode(exchange(alice_token, 7060, b"")[1][0][1]).get(1, [])
check("a channel a player opened is listed while it has members",
      any(decode(entry).get(1, [b""])[0] == b"Scratch" for entry in listed))
scratch_id = decode(exchange(bob_token, 7009, string_field(2, "Scratch") + varint_field(4, CUSTOM))[1][0][1]).get(3, [0])[0]
exchange(bob_token, 7272, varint_field(1, scratch_id))
listed = decode(exchange(alice_token, 7060, b"")[1][0][1]).get(1, [])
check("it is gone once its last member leaves",
      not any(decode(entry).get(1, [b""])[0] == b"Scratch" for entry in listed))
PARTY = 2
exchange(bob_token, 7009, string_field(2, "Party_123") + varint_field(4, PARTY))
listed = decode(exchange(alice_token, 7060, b"")[1][0][1]).get(1, [])
check("a party channel the client opened is not offered to everybody else",
      not any(decode(entry).get(1, [b""])[0] == b"Party_123" for entry in listed),
      [decode(entry).get(1, [b""])[0] for entry in listed])
check("a configured channel survives being empty",
      any(decode(entry).get(1, [b""])[0] == b"Trade" for entry in listed))

# --- private chat -----------------------------------------------------------
handled, replies = exchange(alice_token, 7009, string_field(2, "secret") + varint_field(4, PRIVATE))
check("opening a private chat succeeds", handled and decode(replies[0][1]).get(1, [0])[0] == 0)
handled, replies = exchange(bob_token, 7009, string_field(2, "secret") + varint_field(4, PRIVATE))
check("an uninvited player is refused with PRIVATE_CHAT_NO_PERMISSION",
      decode(replies[0][1]).get(1, [0])[0] == 11, decode(replies[0][1]))

handled, replies = exchange(bob_token, 8084, string_field(1, "secret") + varint_field(2, 90301))
check("a non-admin may not invite", decode(replies[0][1]).get(2, [0])[0] == 4, decode(replies[0][1]))
handled, replies = exchange(alice_token, 8084, string_field(1, "secret") + varint_field(2, 90302))
check("8084 answers 8091 with SUCCESS", handled and replies[0][0] == 8091 and decode(replies[0][1]).get(2, [0])[0] == 0)
handled, replies = exchange(bob_token, 7009, string_field(2, "secret") + varint_field(4, PRIVATE))
check("the invited player may now enter", decode(replies[0][1]).get(1, [0])[0] == 0)

handled, replies = exchange(alice_token, 8092, string_field(1, "secret"))
info = decode(replies[0][1])
check("8092 answers 8093 with both members and the creator",
      handled and len(info.get(2, [])) == 2 and info.get(3, [0])[0] == 90301, info)

handled, replies = exchange(alice_token, 8090, string_field(1, "secret") + varint_field(2, 90301))
check("the last admin may not be demoted", decode(replies[0][1]).get(2, [0])[0] == 8, decode(replies[0][1]))
handled, replies = exchange(alice_token, 8089, string_field(1, "secret") + varint_field(2, 90302))
check("8089 promotes a member", decode(replies[0][1]).get(2, [0])[0] == 0)
handled, replies = exchange(alice_token, 8088, string_field(1, "secret") + varint_field(2, 90302))
check("an admin may not be kicked", decode(replies[0][1]).get(2, [0])[0] == 14, decode(replies[0][1]))
exchange(alice_token, 8090, string_field(1, "secret") + varint_field(2, 90302))
handled, replies = exchange(alice_token, 8088, string_field(1, "secret") + varint_field(2, 90302))
check("8088 kicks a plain member", decode(replies[0][1]).get(2, [0])[0] == 0, decode(replies[0][1]))
handled, replies = exchange(bob_token, 7009, string_field(2, "secret") + varint_field(4, PRIVATE))
check("the kicked player is locked out again", decode(replies[0][1]).get(1, [0])[0] == 11)

# --- the HTTP view ----------------------------------------------------------
snapshot = get("/api/gamecoordinator/chat/channels", alice_token)
by_name = {channel["Name"]: channel for channel in snapshot}
check("the HTTP view lists the configured channels as configured",
      by_name.get("D2MAX", {}).get("Configured") is True and by_name["D2MAX"]["MaxMembers"] == 500, by_name.get("D2MAX"))
check("the HTTP view shows who is in a channel",
      [member["PersonaName"] for member in by_name.get("D2MAX", {}).get("Members", [])] == ["Alice", "Bob"],
      by_name.get("D2MAX"))

print("\n%d/%d checks passed" % (sum(checks), len(checks)))
sys.exit(0 if all(checks) else 1)
