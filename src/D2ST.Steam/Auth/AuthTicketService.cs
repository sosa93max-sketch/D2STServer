using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using D2ST.Core.Auth;
using D2ST.Core.Steam;

namespace D2ST.Steam.Auth;

/// <summary>
/// Ticket format: [steamId | appId | handle | 8 random bytes]. The random tail
/// is what stops a client from forging a ticket for another player, since a
/// ticket is only accepted while its handle is in the issued set.
/// </summary>
public sealed class AuthTicketService : IAuthTicketService
{
    private const int TicketSize = 28;

    private readonly ConcurrentDictionary<uint, AuthTicket> _tickets = new();
    private int _handles;

    public AuthTicket Create(SteamSession session, uint appId, ulong steamId, bool gameServer)
    {
        var handle = (uint)Interlocked.Increment(ref _handles);
        var owner = steamId != 0 ? steamId : session.Account.SteamId;
        var ticket = new byte[TicketSize];
        BinaryPrimitives.WriteUInt64LittleEndian(ticket, owner);
        BinaryPrimitives.WriteUInt32LittleEndian(ticket.AsSpan(8), appId);
        BinaryPrimitives.WriteUInt32LittleEndian(ticket.AsSpan(12), handle);
        RandomNumberGenerator.Fill(ticket.AsSpan(16));

        var issued = new AuthTicket(handle, ticket, owner, appId, gameServer);
        _tickets[handle] = issued;
        return issued;
    }

    public byte[] CreateEncryptedAppTicket(SteamSession session, uint appId, byte[] userData)
    {
        // Nothing here is secret from the client that asked for it, and no
        // Valve key exists to encrypt with, so the ticket is the user data
        // behind the same identity header the session ticket uses.
        var ticket = new byte[16 + userData.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(ticket, session.Account.SteamId);
        BinaryPrimitives.WriteUInt32LittleEndian(ticket.AsSpan(8), appId);
        BinaryPrimitives.WriteUInt32LittleEndian(ticket.AsSpan(12), (uint)userData.Length);
        userData.CopyTo(ticket.AsSpan(16));
        return ticket;
    }

    public TicketValidation Validate(byte[] ticket, ulong steamId, uint appId)
    {
        if (!TryRead(ticket, out var issued) ||
            (steamId != 0 && issued.SteamId != steamId) ||
            (appId != 0 && issued.AppId != appId))
        {
            return new TicketValidation(
                TicketValidation.ResultInvalidParam,
                TicketValidation.SessionResponseAuthTicketInvalid,
                OwnerSteamId: 0,
                Success: false);
        }

        // Nobody shares or family-borrows a game here, so the owner is always
        // the player presenting the ticket.
        return new TicketValidation(
            TicketValidation.ResultOk,
            TicketValidation.SessionResponseOk,
            issued.SteamId,
            Success: true);
    }

    public ConnectAuthResult ConnectAndAuthenticate(byte[] authBlob, ulong steamId, uint appId)
    {
        var validation = Validate(authBlob, steamId, appId);
        return validation.Success
            ? new ConnectAuthResult(true, validation.OwnerSteamId, validation.OwnerSteamId, DenyReason: 0, DenyMessage: string.Empty)
            : new ConnectAuthResult(false, steamId, OwnerSteamId: 0, DenyReason: 5, DenyMessage: "Invalid auth ticket");
    }

    public void EndSession(ulong steamId)
    {
        foreach (var entry in _tickets.Where(entry => entry.Value.SteamId == steamId))
        {
            _tickets.TryRemove(entry.Key, out _);
        }
    }

    public void Cancel(uint handle) => _tickets.TryRemove(handle, out _);

    private bool TryRead(byte[] ticket, out AuthTicket issued)
    {
        issued = null!;
        if (ticket.Length < TicketSize)
        {
            return false;
        }

        var handle = BinaryPrimitives.ReadUInt32LittleEndian(ticket.AsSpan(12));
        return _tickets.TryGetValue(handle, out issued!) && issued.Ticket.AsSpan().SequenceEqual(ticket);
    }
}
