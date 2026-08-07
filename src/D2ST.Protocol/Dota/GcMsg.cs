namespace D2ST.Protocol.Dota;

/// <summary>
/// Game Coordinator message ids, taken from the generated Valve enums so they
/// cannot drift from the wire protocol.
/// </summary>
public static class GcMsg
{
    public const uint ClientWelcome = (uint)EGCBaseClientMsg.kEMsgGCClientWelcome;
    public const uint ClientHello = (uint)EGCBaseClientMsg.kEMsgGCClientHello;
    public const uint ClientConnectionStatus = (uint)EGCBaseClientMsg.kEMsgGCClientConnectionStatus;
    public const uint ServerWelcome = (uint)EGCBaseClientMsg.kEMsgGCServerWelcome;
    public const uint ServerHello = (uint)EGCBaseClientMsg.kEMsgGCServerHello;
    public const uint ServerConnectionStatus = (uint)EGCBaseClientMsg.kEMsgGCServerConnectionStatus;
    public const uint PingRequest = (uint)EGCBaseClientMsg.kEMsgGCPingRequest;
    public const uint PingResponse = (uint)EGCBaseClientMsg.kEMsgGCPingResponse;

    public const uint SoCacheSubscribed = (uint)ESOMsg.kESOMsgCacheSubscribed;
    public const uint SoCacheUnsubscribed = (uint)ESOMsg.kESOMsgCacheUnsubscribed;
    public const uint SoCreate = (uint)ESOMsg.kESOMsgCreate;
    public const uint SoUpdate = (uint)ESOMsg.kESOMsgUpdate;
    public const uint SoDestroy = (uint)ESOMsg.kESOMsgDestroy;
    public const uint SoUpdateMultiple = (uint)ESOMsg.kESOMsgUpdateMultiple;
    public const uint SoCacheSubscriptionRefresh = (uint)ESOMsg.kESOMsgCacheSubscriptionRefresh;

    public const uint InviteToParty = (uint)EGCBaseMsg.kEMsgGCInviteToParty;
    public const uint InvitationCreated = (uint)EGCBaseMsg.kEMsgGCInvitationCreated;
    public const uint PartyInviteResponse = (uint)EGCBaseMsg.kEMsgGCPartyInviteResponse;
    public const uint KickFromParty = (uint)EGCBaseMsg.kEMsgGCKickFromParty;
    public const uint LeaveParty = (uint)EGCBaseMsg.kEMsgGCLeaveParty;

    public const uint SetPartyLeader = (uint)EDOTAGCMsg.kEMsgClientToGCSetPartyLeader;
    public const uint CancelPartyInvites = (uint)EDOTAGCMsg.kEMsgClientToGCCancelPartyInvites;
    public const uint PartyMemberSetCoach = (uint)EDOTAGCMsg.kEMsgGCPartyMemberSetCoach;
    public const uint ClientToGCPingData = (uint)EDOTAGCMsg.kEMsgClientToGCPingData;
    public const uint PartyReadyCheckRequest = (uint)EDOTAGCMsg.kEMsgPartyReadyCheckRequest;
    public const uint PartyReadyCheckResponse = (uint)EDOTAGCMsg.kEMsgPartyReadyCheckResponse;
    public const uint PartyReadyCheckAcknowledge = (uint)EDOTAGCMsg.kEMsgPartyReadyCheckAcknowledge;

    public const uint PracticeLobbyCreate = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbyCreate;
    public const uint PracticeLobbyLeave = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbyLeave;
    public const uint PracticeLobbyLaunch = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbyLaunch;
    public const uint PracticeLobbyList = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbyList;
    public const uint PracticeLobbyListResponse = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbyListResponse;
    public const uint PracticeLobbyJoin = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbyJoin;
    public const uint PracticeLobbySetDetails = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbySetDetails;
    public const uint PracticeLobbySetTeamSlot = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbySetTeamSlot;

    /// <summary>
    /// Reply to a create or a details change. It carries a
    /// <c>CMsgPracticeLobbyJoinResponse</c>: the GC answers both with the same
    /// result body, and only the message id tells the client which request it
    /// belongs to.
    /// </summary>
    public const uint PracticeLobbyResponse = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbyResponse;
    public const uint PracticeLobbyJoinResponse = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbyJoinResponse;
    public const uint PracticeLobbyKick = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbyKick;
    public const uint PracticeLobbyKickFromTeam = (uint)EDOTAGCMsg.kEMsgGCPracticeLobbyKickFromTeam;

    public const uint JoinChatChannel = (uint)EDOTAGCMsg.kEMsgGCJoinChatChannel;
    public const uint JoinChatChannelResponse = (uint)EDOTAGCMsg.kEMsgGCJoinChatChannelResponse;
    public const uint LeaveChatChannel = (uint)EDOTAGCMsg.kEMsgGCLeaveChatChannel;
    public const uint ChatMessage = (uint)EDOTAGCMsg.kEMsgGCChatMessage;
    public const uint OtherJoinedChatChannel = (uint)EDOTAGCMsg.kEMsgGCOtherJoinedChannel;
    public const uint OtherLeftChatChannel = (uint)EDOTAGCMsg.kEMsgGCOtherLeftChannel;
    public const uint RequestChatChannelList = (uint)EDOTAGCMsg.kEMsgGCRequestChatChannelList;
    public const uint RequestChatChannelListResponse = (uint)EDOTAGCMsg.kEMsgGCRequestChatChannelListResponse;
    public const uint ChatGetUserList = (uint)EDOTAGCMsg.kEMsgDOTAChatGetUserList;
    public const uint ChatGetUserListResponse = (uint)EDOTAGCMsg.kEMsgDOTAChatGetUserListResponse;
    public const uint ChatGetMemberCount = (uint)EDOTAGCMsg.kEMsgDOTAChatGetMemberCount;
    public const uint ChatGetMemberCountResponse = (uint)EDOTAGCMsg.kEMsgDOTAChatGetMemberCountResponse;
    public const uint PrivateChatInvite = (uint)EDOTAGCMsg.kEMsgClientToGCPrivateChatInvite;
    public const uint PrivateChatKick = (uint)EDOTAGCMsg.kEMsgClientToGCPrivateChatKick;
    public const uint PrivateChatPromote = (uint)EDOTAGCMsg.kEMsgClientToGCPrivateChatPromote;
    public const uint PrivateChatDemote = (uint)EDOTAGCMsg.kEMsgClientToGCPrivateChatDemote;
    public const uint ToClientPrivateChatResponse = (uint)EDOTAGCMsg.kEMsgGCToClientPrivateChatResponse;
    public const uint PrivateChatInfoRequest = (uint)EDOTAGCMsg.kEMsgClientToGCPrivateChatInfoRequest;
    public const uint ToClientPrivateChatInfoResponse = (uint)EDOTAGCMsg.kEMsgGCToClientPrivateChatInfoResponse;

    public const uint ClientToGCGetProfileCard = (uint)EDOTAGCMsg.kEMsgClientToGCGetProfileCard;
    public const uint ClientToGCGetProfileCardResponse = (uint)EDOTAGCMsg.kEMsgClientToGCGetProfileCardResponse;

    public const uint MatchmakingStatsRequest = (uint)EDOTAGCMsg.kEMsgGCMatchmakingStatsRequest;
    public const uint MatchmakingStatsResponse = (uint)EDOTAGCMsg.kEMsgGCMatchmakingStatsResponse;
    public const uint GetWeekendTourneySchedule = (uint)EDOTAGCMsg.kEMsgDOTAGetWeekendTourneySchedule;
    public const uint WeekendTourneySchedule = (uint)EDOTAGCMsg.kEMsgDOTAWeekendTourneySchedule;
    public const uint MyTeamInfoRequest = (uint)EDOTAGCMsg.kEMsgClientToGCMyTeamInfoRequest;
    public const uint ToClientTeamsInfo = (uint)EDOTAGCMsg.kEMsgGCToClientTeamsInfo;
    public const uint RequestGuildData = (uint)EDOTAGCMsg.kEMsgGCRequestGuildData;
    public const uint GuildData = (uint)EDOTAGCMsg.kEMsgGCGuildData;
    public const uint EmoticonDataRequest = (uint)EDOTAGCMsg.kEMsgClientToGCEmoticonDataRequest;
    public const uint ToClientEmoticonData = (uint)EDOTAGCMsg.kEMsgGCToClientEmoticonData;
    public const uint GetEventPoints = (uint)EDOTAGCMsg.kEMsgDOTAGetEventPoints;
    public const uint GetEventPointsResponse = (uint)EDOTAGCMsg.kEMsgDOTAGetEventPointsResponse;

    public const uint RequestStoreSalesData = (uint)EGCItemMsg.kEMsgGCRequestStoreSalesData;
    public const uint RequestStoreSalesDataResponse = (uint)EGCItemMsg.kEMsgGCRequestStoreSalesDataResponse;

    public const uint SetItemPositions = (uint)EGCItemMsg.kEMsgGCSetItemPositions;
    public const uint UseItemRequest = (uint)EGCItemMsg.kEMsgGCUseItemRequest;
    public const uint UseItemResponse = (uint)EGCItemMsg.kEMsgGCUseItemResponse;
    public const uint EquipItems = (uint)EGCItemMsg.kEMsgClientToGCEquipItems;
    public const uint EquipItemsResponse = (uint)EGCItemMsg.kEMsgClientToGCEquipItemsResponse;
    public const uint SetItemStyle = (uint)EGCItemMsg.kEMsgClientToGCSetItemStyle;
    public const uint SetItemStyleResponse = (uint)EGCItemMsg.kEMsgClientToGCSetItemStyleResponse;
    public const uint UnlockItemStyle = (uint)EGCItemMsg.kEMsgClientToGCUnlockItemStyle;
    public const uint UnlockItemStyleResponse = (uint)EGCItemMsg.kEMsgClientToGCUnlockItemStyleResponse;
    public const uint UnlockCrate = (uint)EGCItemMsg.kEMsgClientToGCUnlockCrate;
    public const uint UnlockCrateResponse = (uint)EGCItemMsg.kEMsgClientToGCUnlockCrateResponse;
    public const uint UnpackBundle = (uint)EGCItemMsg.kEMsgClientToGCUnpackBundle;
    public const uint UnpackBundleResponse = (uint)EGCItemMsg.kEMsgClientToGCUnpackBundleResponse;
    public const uint StorePurchaseInit = (uint)EGCItemMsg.kEMsgGCStorePurchaseInit;
    public const uint StorePurchaseInitResponse = (uint)EGCItemMsg.kEMsgGCStorePurchaseInitResponse;
    public const uint StorePurchaseCancel = (uint)EGCItemMsg.kEMsgGCStorePurchaseCancel;
    public const uint StorePurchaseCancelResponse = (uint)EGCItemMsg.kEMsgGCStorePurchaseCancelResponse;

    public const uint RedeemItem = (uint)EDOTAGCMsg.kEMsgDOTARedeemItem;
    public const uint RedeemItemResponse = (uint)EDOTAGCMsg.kEMsgDOTARedeemItemResponse;
    public const uint PurchaseItemWithEventPoints = (uint)EDOTAGCMsg.kEMsgPurchaseItemWithEventPoints;
    public const uint PurchaseItemWithEventPointsResponse = (uint)EDOTAGCMsg.kEMsgPurchaseItemWithEventPointsResponse;
}
