using D2ST.Core.Economy;
using D2ST.GameCoordinator.Econ;

namespace D2ST.Api.Contracts;

// Wire shapes below mirror the DTOs the injected Steamworks shim
// (soulhuntermax/steam_api, Managers/APIClient.cs) serializes, including their
// PascalCase member names, so the two sides stay byte-compatible.

public sealed record VersionResponse(string Version);

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(ulong SteamId, uint AccountId, string Token);

/// <summary>Body of the shim's POST /api/auth/steam/session logon.</summary>
public sealed record SteamSessionRequest(
    uint AccountId,
    ulong SteamId,
    uint AppId,
    string? PersonaName,
    string? ClientInstanceId,
    string? ProcessRole,
    bool UseActiveWebUser);

public sealed record SteamSessionResponse(string AccessToken, string RefreshToken, ApiUser User);

/// <summary>One account as the admin web lists it.</summary>
public sealed record AdminUserResponse(
    uint AccountId,
    string SteamId,
    string Username,
    string? PersonaName,
    bool Online,
    DateTimeOffset CreatedAt,
    bool HasAvatar,
    int Mmr,
    int RankTier,
    int RankStar,
    int RankValue,
    int RankProgress,
    bool IsCalibrated,
    long BalanceDollars,
    long ReservedDollars,
    long AvailableDollars,
    bool DotaPlusActive,
    DateTimeOffset? DotaPlusExpiresAt,
    int DotaPlusDaysRemaining,
    long DotaPlusShards);

public sealed record AdminUsersPageResponse(
    IReadOnlyList<AdminUserResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int OnlineCount);

public sealed record AdminCreateUserRequest(string Username, string Password, string? PersonaName);

public sealed record AdminSetPasswordRequest(string Password);

public sealed record AdminSetPersonaRequest(string PersonaName);

public sealed record AdminSetAvatarRequest(string ContentBase64);

public sealed record AdminAdjustMmrRequest(int Delta);

public sealed record AdminWalletAdjustRequest(long DeltaDollars, string? Reason);

public sealed record AdminMessageResponse(string Message);

/// <summary>
/// A player as the client renders it. Presence members are zeroed for offline
/// players: the client treats a non-zero AppId as "currently playing".
/// </summary>
public sealed record ApiUser(
    uint AccountId,
    ulong SteamId,
    string PersonaName,
    uint AppId,
    ulong LobbyId,
    ulong GameServerSteamId,
    uint GameServerIp,
    ushort GameServerPort,
    bool HasFriend,
    int FriendRelationship,
    int PersonaState,
    int PlayerLevel,
    IReadOnlyDictionary<string, string> RichPresence);

public sealed record PersonaUpdateRequest(string PersonaName);

public sealed record PresenceUpdateRequest(string Key, string? Value);

public sealed record GameServerPresenceUpdateRequest(ulong SteamId, uint Ip, ushort Port);

public sealed record AvatarUpdateRequest(string ContentBase64);

/// <summary>Target of a friend action, by Steam id or by persona/username.</summary>
public sealed record FriendActionRequest(ulong SteamId, string? Identifier);

/// <summary>
/// One pushed event. Members outside the event's own type stay at their
/// defaults; the client only reads the ones its handler for
/// <paramref name="Type"/> cares about.
/// </summary>
public sealed record ApiEvent(
    string Type,
    ulong SteamId,
    uint AccountId,
    string PersonaName,
    uint AppId,
    ulong LobbyId,
    ulong GameServerSteamId,
    uint GameServerIp,
    ushort GameServerPort,
    int PersonaState,
    int ChangeFlags,
    int FriendRelationship,
    string RequestId,
    string GameName,
    ApiLobby? Lobby,
    string PayloadBase64,
    uint MessageType,
    ulong? TargetJobId,
    bool Protobuf,
    ulong RemoteSteamId,
    int Channel,
    string Transport,
    int VirtualPort,
    uint SourceConnectionId,
    uint TargetConnectionId,
    IReadOnlyDictionary<string, string> RichPresence,
    string StatName,
    uint StatValue,
    string AchievementName,
    bool AchievementEarned,
    uint AchievementProgress,
    uint AchievementMaxProgress);

/// <param name="Cursor">Echoed back on the next poll to resume where this batch ended.</param>
public sealed record ApiEventEnvelope(string Cursor, IReadOnlyList<ApiEvent> Events);

/// <summary>
/// One GC message in either direction. The body is the protobuf payload only:
/// the shim builds and strips the 8-byte GC header itself, carrying the job id
/// out of band in <paramref name="TargetJobId"/>.
/// </summary>
public sealed record GcMessageDto(
    uint AppId,
    uint MessageType,
    string PayloadBase64,
    ulong? TargetJobId,
    bool Protobuf);

public sealed record GcExchangeRequest(
    uint AppId,
    uint MessageType,
    string? BodyBase64,
    ulong SourceJobId,
    ulong SteamId,
    bool GameServer);

public sealed record GcPollRequest(uint AppId, ulong SteamId, bool GameServer);

public sealed record GcExchangeResponse(bool Handled, IReadOnlyList<GcMessageDto> Messages);

/// <summary>
/// Administrative/test grant for putting an item definition in an account's
/// inventory. Normal ownership should come from a purchase or match flow.
/// </summary>
public sealed record GcGrantItemRequest(ulong SteamId, uint DefIndex, uint Quantity);

public sealed record GcGrantItemResponse(ulong ItemId, uint DefIndex, uint Quantity, ulong CacheVersion);

public sealed record GcInventoryItem(ulong ItemId, uint DefIndex, uint Quantity, uint Style, uint Inventory);

public sealed record GcInventoryResponse(IReadOnlyList<GcInventoryItem> Items, ulong CacheVersion);

public sealed record StoreEquipRequest(
    ulong ItemId,
    uint HeroId,
    uint Slot,
    uint StyleIndex = 255);

public sealed record StoreEquipResponse(
    bool Success,
    int Changed,
    ulong CacheVersion,
    string Code,
    string Message);

public sealed record StorePurchaseLineRequest(uint ProductId, uint Quantity);

public sealed record StorePurchaseRequest(
    IReadOnlyList<StorePurchaseLineRequest>? Lines,
    uint ProductId = 0,
    uint Quantity = 0);

public sealed record StoreCatalogComponentRequest(uint ProductId, uint Quantity);

public sealed record StoreCatalogUpsertRequest(
    uint ProductId,
    uint DefIndex,
    string Name,
    StoreProductType ProductType,
    long PriceDollars,
    string? Category,
    string? Description,
    uint BuildVersion,
    int DotaPlusDays,
    bool Active,
    IReadOnlyList<StoreCatalogComponentRequest>? Components,
    string? MarketHashName = null,
    string? MarketSearchName = null,
    long? MarketLowestPriceCents = null,
    long? MarketMedianPriceCents = null,
    long? MarketVolume = null,
    string? MarketPriceSource = null,
    string? MarketPriceStatus = null,
    DateTimeOffset? MarketPriceUpdatedAt = null,
    IReadOnlyList<string>? Heroes = null);

public sealed record StoreCatalogItemResponse(
    uint ProductId,
    uint DefIndex,
    string Name,
    StoreProductType ProductType,
    long PriceDollars,
    string Category,
    string Description,
    uint BuildVersion,
    int DotaPlusDays,
    bool Active,
    IReadOnlyList<StoreCatalogComponent> Components,
    uint OwnedQuantity,
    string MarketHashName,
    string MarketSearchName,
    long? MarketLowestPriceCents,
    long? MarketMedianPriceCents,
    long? MarketVolume,
    string MarketPriceSource,
    string MarketPriceStatus,
    DateTimeOffset? MarketPriceUpdatedAt,
    IReadOnlyList<string>? Heroes = null);

public sealed record StoreCatalogPageResponse(
    IReadOnlyList<StoreCatalogItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int ActiveCount,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Heroes);

public sealed record StoreCatalogClearResponse(
    int RemovedProducts,
    string Message);

public sealed record MarketPriceSyncRequest(
    bool ActiveOnly = true,
    int MaxItems = 100,
    int MaxAgeMinutes = 60,
    bool UseMedian = false,
    bool DryRun = false,
    IReadOnlyList<uint>? ProductIds = null,
    bool ActivateMatched = false);

public sealed record MarketPriceSyncItemResponse(
    uint ProductId,
    uint DefIndex,
    string Name,
    string Status,
    string MarketHashName,
    long? LowestPriceCents,
    long? MedianPriceCents,
    long? Volume,
    long? AppliedPriceDollars,
    string? Error);

public sealed record MarketPriceSyncResponse(
    int Requested,
    int Processed,
    int Matched,
    int Cached,
    int NoMatch,
    int NoData,
    int Failed,
    bool DryRun,
    IReadOnlyList<MarketPriceSyncItemResponse> Items);

public sealed record DotaCatalogDiscoverRequest(
    string DotaPath,
    string? Search = null,
    int Take = 500,
    string Language = "spanish");

public sealed record DotaCatalogImportRequest(
    string DotaPath,
    long DefaultPriceDollars = 0,
    bool Activate = false,
    uint? BuildVersion = null,
    IReadOnlyList<uint>? DefIndexes = null,
    bool ClearExisting = false,
    string Language = "spanish");

public sealed record DotaCatalogDefinitionResponse(
    uint DefIndex,
    string Name,
    string DisplayName,
    string MarketSearchName,
    string ItemName,
    string Description,
    string Prefab,
    string Slot,
    string Quality,
    string Rarity,
    string ImageInventory,
    IReadOnlyList<string> HeroNames);

public sealed record DotaCatalogDiscoverResponse(
    string DotaPath,
    string PakPath,
    string? SteamInfPath,
    uint ClientVersion,
    int ParsedDefinitionCount,
    int CandidateCount,
    IReadOnlyList<DotaCatalogDefinitionResponse> Items);

public sealed record DotaCatalogImportResponse(
    string DotaPath,
    string PakPath,
    string? SteamInfPath,
    uint ClientVersion,
    int ParsedDefinitionCount,
    int CandidateCount,
    int ImportedCount,
    int UpdatedCount,
    int SkippedCount,
    long DefaultPriceDollars,
    bool Activate,
    string Message,
    int RemovedExistingCount = 0,
    string Language = "spanish",
    int PricesQueued = 0);

public sealed record StoreWalletResponse(
    uint AccountId,
    long BalanceDollars,
    long ReservedDollars,
    long AvailableDollars,
    DateTimeOffset? UpdatedAt);

public sealed record AdminWalletAdjustResponse(
    bool Success,
    string Code,
    string Message,
    StoreWalletResponse Wallet);

public sealed record DotaPlusChallengeResponse(
    uint AccountId,
    uint SlotId,
    uint SequenceId,
    uint TemplateId,
    uint Progress,
    uint Target,
    uint RewardShards,
    int HeroId,
    uint QuestRank,
    uint MaxQuestRank,
    DateTimeOffset CreatedAt);

public sealed record DotaPlusResponse(
    uint AccountId,
    bool Active,
    DateTimeOffset? StartedAt,
    DateTimeOffset? ExpiresAt,
    int DaysRemaining,
    uint PlusStatus,
    long Shards,
    IReadOnlyList<DotaPlusChallengeResponse> Challenges);

public sealed record AdminDotaPlusUpdateRequest(
    bool Enabled,
    int Days,
    string? Reason);

public sealed record AdminDotaPlusUpdateResponse(
    bool Success,
    string Code,
    string Message,
    DotaPlusResponse DotaPlus);

public sealed record AdminDotaPlusShardUpdateRequest(long Delta, string? Reason);

public sealed record AdminDotaPlusShardUpdateResponse(
    bool Success,
    string Code,
    string Message,
    DotaPlusResponse DotaPlus);

public sealed record AdminCatalogPageResponse(
    IReadOnlyList<StoreCatalogItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int ActiveCount);

public sealed record StorePurchaseResponse(
    bool Success,
    string Code,
    string Message,
    ulong TransactionId,
    IReadOnlyList<ulong> ItemIds,
    StoreWalletResponse Wallet,
    IReadOnlyList<GcInventoryItem> Items);

/// <summary>
/// A party as the GC holds it, for inspecting the Shared Object without a Dota
/// client. Parties are created by the GC party messages, never over HTTP.
/// </summary>
public sealed record GcPartyResponse(
    ulong PartyId,
    ulong LeaderSteamId,
    IReadOnlyList<ulong> MemberSteamIds,
    IReadOnlyList<bool> MemberCoachFlags,
    uint ReadyCheckFinishTimestamp);

/// <summary>
/// A GC lobby as the Shared Object holds it, for inspecting it without a Dota
/// client. Lobbies are created by the GC practice lobby messages, never over
/// HTTP.
/// </summary>
public sealed record GcLobbyResponse(
    ulong LobbyId,
    ulong LeaderSteamId,
    string GameName,
    uint GameMode,
    uint ServerRegion,
    string State,
    bool RequiresPassKey,
    string Connect,
    ulong MatchId,
    uint GameStartTime,
    uint GameState,
    bool Lan,
    ulong ServerId,
    IReadOnlyList<GcLobbyMember> Members);

public sealed record GcLobbyMember(ulong SteamId, string Name, int Team, uint Slot);

/// <summary>A lobby as the shim's SkyNetLobbyDto.</summary>
public sealed record ApiLobby(
    ulong SteamId,
    uint AppId,
    ulong OwnerSteamId,
    int LobbyType,
    int MaxMembers,
    bool Joinable,
    IReadOnlyDictionary<string, string> LobbyData,
    IReadOnlyList<ApiLobbyMember> Members,
    ApiLobbyGameServer GameServer);

/// <summary>Member data is a list of pairs, not a map, to match the shim.</summary>
public sealed record ApiLobbyMember(ulong SteamId, IReadOnlyList<ApiLobbyMetaData> Data);

public sealed record ApiLobbyMetaData(string Key, string Value);

public sealed record ApiLobbyGameServer(ulong SteamId, uint IP, uint Port);

public sealed record CreateLobbyRequest(
    uint AppId,
    int LobbyType,
    int MaxMembers,
    Dictionary<string, string>? LobbyData);

public sealed record LobbyQueryRequest(
    uint AppId,
    int Distance,
    int SlotsAvailable,
    int ResultCount,
    string? KeyToMatch,
    int ValueToMatch,
    int ComparisonType,
    string? StringValueToMatch,
    IReadOnlyList<LobbyNumericalFilterRequest>? NumericalFilters,
    IReadOnlyList<LobbyStringFilterRequest>? StringFilters,
    IReadOnlyList<LobbyNearValueFilterRequest>? NearValueFilters);

public sealed record LobbyNumericalFilterRequest(string? KeyToMatch, int ValueToMatch, int ComparisonType);

public sealed record LobbyStringFilterRequest(string? KeyToMatch, string? ValueToMatch, int ComparisonType);

public sealed record LobbyNearValueFilterRequest(string? KeyToMatch, int ValueToBeCloseTo);

public sealed record LobbyDataUpdateRequest(string Key, string? Value);

public sealed record LobbyDeleteDataRequest(string Key);

public sealed record LobbyChatRequest(string? MessageBase64);

public sealed record LobbyInviteRequest(ulong InviteeSteamId);

public sealed record LobbyGameServerUpdateRequest(ulong SteamIdGameServer, uint IP, uint Port);

public sealed record LobbySettingsUpdateRequest(bool? Joinable, int? LobbyType, ulong? OwnerSteamId, int? MaxMembers);

public sealed record GameInviteRequest(ulong InviteeSteamId, string? ConnectString);

/// <summary>One relayed P2P datagram (the shim's SkyNetP2PPacketSendDto).</summary>
public sealed record P2PPacketRequest(
    ulong RemoteSteamId,
    string? BufferBase64,
    int SendType,
    int Channel,
    string? Transport,
    int VirtualPort,
    uint SourceConnectionId,
    uint TargetConnectionId);

public sealed record P2PPacketBatchRequest(IReadOnlyList<P2PPacketRequest>? Packets);



// ---- Stage 3: tickets, game servers, storage, stats, leaderboards, workshop ----

public sealed record AuthTicketRequest(uint AppId, ulong SteamId, bool GameServer, int TicketBufferSize);

public sealed record AuthTicketResponse(uint Handle, string TicketBase64, uint TicketSize);

public sealed record EncryptedAppTicketRequest(uint AppId, string? UserDataBase64);

/// <param name="Result">EResult; 1 is k_EResultOK.</param>
public sealed record EncryptedAppTicketResponse(int Result, string TicketBase64);

public sealed record AuthValidateRequest(ulong SteamId, string? TicketBase64, bool GameServer, uint AppId);

public sealed record AuthValidateResponse(
    int BeginAuthSessionResult,
    int AuthSessionResponse,
    ulong OwnerSteamId,
    bool Success);

public sealed record ConnectAuthRequest(uint IpClient, ulong SteamId, string? AuthBlobBase64, uint AppId);

public sealed record ConnectAuthResponse(
    bool Success,
    ulong SteamId,
    ulong OwnerSteamId,
    int DenyReason,
    string DenyMessage);

public sealed record AuthEndSessionRequest(ulong SteamId, bool GameServer);

public sealed record CancelAuthTicketRequest(uint Handle, bool GameServer);

/// <summary>A game server as the shim's SkyNetGameServerDto.</summary>
public sealed record ApiGameServer(
    ulong SteamId,
    uint AppId,
    uint IP,
    int Port,
    int QueryPort,
    uint Flags,
    byte Secure,
    string VersionString,
    string Product,
    string Description,
    string ModDir,
    bool Dedicated,
    int MaxPlayers,
    int BotPlayers,
    string ServerName,
    string MapName,
    bool PasswordProtected,
    uint SpectatorPort,
    string SpectatorServerName,
    string GameTags,
    string GameData,
    string Region,
    bool LoggedOn,
    bool AdvertiseActive,
    IReadOnlyDictionary<string, string> KeyValues,
    IReadOnlyList<ApiGameServerPlayer> Players);

public sealed record ApiGameServerPlayer(ulong SteamId, string Name, int Score, float TimePlayedSeconds);

public sealed record GameServerStateRequest(ApiGameServer? Server, string? Token, bool Anonymous);

public sealed record GameServerResult(bool Success, uint PublicIP, byte Secure, ulong SteamId);

public sealed record GameServerPublicIpResponse(uint PublicIP);

public sealed record GameServerUserDataRequest(ulong SteamId, string? PlayerName, uint Score);

public sealed record DisconnectGameServerUserRequest(ulong SteamId);

public sealed record ApiStat(string Name, uint Data);

public sealed record ApiAchievement(string Name, bool Earned, DateTimeOffset Date, uint Progress, uint MaxProgress);

public sealed record ApiStatsEnvelope(
    ulong SteamId,
    IReadOnlyList<ApiStat> Stats,
    IReadOnlyList<ApiAchievement> Achievements,
    int CurrentPlayers);

public sealed record StoreStatsRequest(
    ulong SteamId,
    IReadOnlyList<ApiStat>? Stats,
    IReadOnlyList<ApiAchievement>? Achievements);

public sealed record RemoteStorageUploadRequest(string FileName, string? ContentBase64, uint? SyncPlatforms);

public sealed record ApiRemoteStorageFile(
    string FileName,
    string ContentBase64,
    int Size,
    uint Timestamp,
    string Sha256,
    uint SyncPlatforms,
    int Version);

public sealed record ApiRemoteStorageFileListItem(
    string FileName,
    int Size,
    uint Timestamp,
    string Sha256,
    uint SyncPlatforms,
    int Version);

public sealed record RemoteStorageFileNameRequest(string FileName);

/// <param name="Result">EResult of the share; 1 is k_EResultOK.</param>
public sealed record ApiRemoteStorageShare(ulong Handle, int Result);

public sealed record ApiRemoteStorageQuota(ulong TotalBytes, ulong AvailableBytes);

public sealed record LeaderboardFindRequest(string Name, int SortMethod, int DisplayType);

public sealed record ApiLeaderboard(ulong Id, uint AppId, string Name, int SortMethod, int DisplayType, int EntryCount);

public sealed record LeaderboardEntriesRequest(
    int DataRequest,
    int RangeStart,
    int RangeEnd,
    IReadOnlyList<ulong>? Users);

public sealed record ApiLeaderboardEntry(
    ulong SteamId,
    int GlobalRank,
    int Score,
    IReadOnlyList<int> Details,
    ulong UgcHandle);

public sealed record ApiLeaderboardEntries(ApiLeaderboard Leaderboard, IReadOnlyList<ApiLeaderboardEntry> Entries);

public sealed record LeaderboardScoreUploadRequest(int UploadMethod, int Score, IReadOnlyList<int>? Details);

public sealed record LeaderboardScoreUploadResponse(
    bool Success,
    bool ScoreChanged,
    int Score,
    int GlobalRankNew,
    int GlobalRankPrevious);

public sealed record ApiWorkshopItem(
    ulong PublishedFileId,
    uint CreatorAppId,
    uint ConsumerAppId,
    ulong OwnerSteamId,
    int FileType,
    string Title,
    string Description,
    string Tags,
    string FileName,
    string Metadata,
    string PreviewUrl,
    int Visibility,
    bool Banned,
    bool AcceptedForUse,
    uint TimeCreated,
    uint TimeUpdated,
    long FileSize,
    long TotalFilesSize,
    uint VotesUp,
    uint VotesDown,
    float Score);

public sealed record ApiWorkshopSubscription(
    ulong PublishedFileId,
    DateTimeOffset SubscribedAtUtc,
    bool DisabledLocally,
    ApiWorkshopItem? Item);

public sealed record ApiWorkshopMutation(bool Success, ApiWorkshopSubscription? Subscription);

/// <summary>One chat channel as the GC holds it, for reading the chat without a Dota client.</summary>
public sealed record GcChatChannelResponse(
    ulong ChannelId,
    string Name,
    string Type,
    int MaxMembers,
    bool Configured,
    IReadOnlyList<GcChatMember> Members);

public sealed record GcChatMember(ulong SteamId, string PersonaName);
