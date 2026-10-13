// Fake api.rec.net for Rec Room Android. Returns valid stub responses for all API endpoints.
// usage: fakeserver [port] [pfx] [pfxPassword]
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

class EndpointHandler
{
    public string Method { get; set; }
    public string Pattern { get; set; }
    public Func<string, object> Handler { get; set; }
}

class ErrorResponse
{
    public string error { get; set; }
    public string error_type { get; set; }
}

class SuccessResponse
{
    public bool success { get; set; }
}

int port = args.Length > 0 ? int.Parse(args[0]) : 443;
string pfx = args.Length > 1 ? args[1] : "certs/server.pfx";
string pw = args.Length > 2 ? args[2] : "fake";

if (!File.Exists(pfx))
{
    Log("ERROR: Certificate file not found!");
    return;
}

var cert = X509CertificateLoader.LoadPkcs12FromFile(pfx, pw);
var listener = new TcpListener(IPAddress.Any, port);
listener.Start();
Log($"listening on :{port}, cert {cert.Subject}");

RegisterEndpoints();

while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    _ = Task.Run(() => Handle(client));
}

void RegisterEndpoints()
{
    // Account endpoints
    Register("POST", "/account/bulk", HandleAccountBulk);
    Register("GET", "/account/me", HandleAccountMe);
    Register("POST", "/account/recoverpassword", HandleRecoverPassword);
    Register("POST", "/account/me/changepassword", HandleChangePassword);
    Register("GET", "/account/me/haspassword", HandleHasPassword);
    Register("POST", "/account/me/createlogintoken", HandleCreateLoginToken);
    Register("POST", "/account/me/remoteauth", HandleRemoteAuth);
    Register("GET", "/account/search", HandleAccountSearch);
    Register("GET", "/account/me/changepassword", HandleChangePassword);
    
    // Room endpoints
    Register("GET", "/room/", HandleRoomList);
    Register("GET", "/room/{0}", HandleRoomDetails);
    Register("GET", "/room/{0}/instances", HandleRoomInstances);
    
    // Social endpoints
    Register("GET", "/relationships/", HandleRelationships);
    Register("GET", "/relationships/mutualfriends", HandleMutualFriends);
    Register("GET", "/messages", HandleMessages);
    Register("POST", "/messages", HandleMessagesSend);
    
    // Economy endpoints
    Register("GET", "/items/purchaseInfos", HandlePurchaseInfos);
    Register("POST", "/items/bulkpurchase", HandleBulkPurchase);
    Register("GET", "/consumables/v1/all", HandleConsumablesAll);
    Register("POST", "/consumables/v1/consume", HandleConsumablesConsume);
    Register("GET", "/roomcurrencies/v1/currencies", HandleRoomCurrencies);
    Register("POST", "/roomcurrencies/v1/awardCurrency/bulk", HandleAwardCurrency);
    
    // Inventions endpoints
    Register("GET", "/invention/", HandleInventionList);
    Register("GET", "/inventions/v1/featured", HandleInventionFeatured);
    Register("GET", "/inventions/v2/mine", HandleInventionMine);
    Register("POST", "/inventions/v4/publish", HandleInventionPublish);
    Register("POST", "/inventions/v9/save", HandleInventionSave);
    
    // Player endpoints
    Register("GET", "/player/", HandlePlayer);
    Register("POST", "/player/heartbeat", HandlePlayerHeartbeat);
    Register("GET", "/player/login", HandlePlayerLogin);
    Register("POST", "/player/logout", HandlePlayerLogout);
    Register("GET", "/player/connection-info", HandleConnectionInfo);
    
    // API endpoints
    Register("GET", "/api/", HandleApiList);
    Register("GET", "/api/config/", HandleApiConfig);
    Register("GET", "/api/inventions/", HandleApiInventions);
    Register("GET", "/api/avatar/", HandleApiAvatar);
    Register("GET", "/api/customAvatarItems/", HandleCustomAvatarItems);
    Register("GET", "/api/items/", HandleApiItems);
    Register("GET", "/api/consumables/", HandleApiConsumables);
    Register("GET", "/api/rooms/", HandleApiRooms);
    Register("GET", "/api/relationships/", HandleApiRelationships);
    Register("GET", "/api/messages/", HandleApiMessages);
    Register("GET", "/api/invention/", HandleApiInvention);
    Register("GET", "/api/avatar/", HandleApiAvatar);
    Register("GET", "/api/checklist/", HandleApiChecklist);
    Register("GET", "/api/communityboard/", HandleApiCommunityBoard);
    Register("GET", "/api/chatreport/", HandleApiChatReport);
    Register("GET", "/api/equipment/", HandleApiEquipment);
    Register("GET", "/api/freegifts/", HandleApiFreegifts);
    Register("GET", "/api/gameconfigs/", HandleApiGameConfigs);
    Register("GET", "/api/gamerewards/", HandleApiGameRewards);
    Register("GET", "/api/incentivizedreferrals/", HandleApiIncentivizedReferrals);
    Register("GET", "/api/itemWishlists/", HandleApiItemWishlists);
    Register("GET", "/api/keepsakes/", HandleApiKeepsakes);
    Register("GET", "/api/playerevents/", HandleApiPlayerEvents);
    Register("GET", "/api/recipesEvents/", HandleApiRecipesEvents);
    Register("GET", "/api/storefronts/", HandleApiStorefronts);
    Register("GET", "/api/subscriptionseasons/", HandleApiSubscriptionSeasons);
    Register("GET", "/api/roomkeys/", HandleApiRoomKeys);
    Register("GET", "/api/rooms/", HandleApiRooms);
    Register("GET", "/api/quickPlay/", HandleApiQuickPlay);
    Register("GET", "/api/royale/", HandleApiRoyale);
    Register("GET", "/api/versioncheck/", HandleApiVersionCheck);
    Register("GET", "/api/purchasableXpBoosts/", HandleApiPurchasableXpBoosts);
    Register("GET", "/api/playstationplus/", HandleApiPlayStationPlus);
    Register("GET", "/api/playeritems/", HandleApiPlayerItems);
    Register("GET", "/api/playerevents/", HandleApiPlayerEvents);
    Register("GET", "/api/storefronts/", HandleApiStorefronts);
    Register("GET", "/api/timedCurrencyGrants/", HandleApiTimedCurrencyGrants);
    Register("GET", "/api/timedRewardTracks/", HandleApiTimedRewardTracks);
    Register("GET", "/api/timedRewardTrees/", HandleApiTimedRewardTrees);
    Register("GET", "/api/ugcPurchasables/", HandleApiUgcPurchasables);
    Register("GET", "/api/activities/", HandleApiActivities);
    Register("GET", "/api/banappeal/", HandleApiBanAppeal);
    Register("GET", "/api/playerCheer/", HandleApiPlayerCheer);
    Register("GET", "/api/playerReporting/", HandleApiPlayerReporting);
    Register("GET", "/api/announcements/", HandleApiAnnouncements);
    Register("GET", "/api/ageverification/", HandleApiAgeVerification);
    Register("GET", "/api/apple/", HandleApiApple);
    Register("GET", "/api/makerai/", HandleApiMakerAI);
    Register("GET", "/api/influencerpartnerprogram/", HandleApiInfluencerPartner);
    Register("GET", "/api/offlineinvite/", HandleApiOfflineInvite);
    Register("GET", "/api/octoberTricks/", HandleApiOctoberTricks);
    Register("GET", "/api/oktoberfeast/", HandleApiOktoberfeast);
    Register("GET", "/api/sanitize/", HandleApiSanitize);
    Register("GET", "/api/screensharereports/", HandleApiScreenShareReports);
    Register("GET", "/api/testcasemanagement/", HandleApiTestCaseManagement);
    Register("GET", "/api/clubreporting/", HandleApiClubReporting);
    Register("GET", "/api/progressionEvents/", HandleApiProgressionEvents);
    Register("GET", "/api/timeLimitedEvents/", HandleApiTimeLimitedEvents);
    Register("GET", "/api/event/", HandleApiEvent);
    Register("GET", "/api/event/", HandleApiEvent);
    Register("GET", "/api/player/", HandleApiPlayer);
    Register("GET", "/api/roomconsumables/", HandleApiRoomConsumables);
    Register("GET", "/api/progressionEvents/", HandleApiProgressionEvents);
    Register("GET", "/api/progressionEvents/", HandleApiProgressionEvents);
    Register("GET", "/api/ugcPurchasables/", HandleApiUgcPurchasables);
    Register("GET", "/api/versioncheck/", HandleApiVersionCheck);
    Register("GET", "/api/testcasemanagement/", HandleApiTestCaseManagement);
    Register("GET", "/api/testcasemanagement/", HandleApiTestCaseManagement);
    Register("GET", "/api/storefronts/", HandleApiStorefronts);
    Register("GET", "/api/roomkeys/", HandleApiRoomKeys);
    Register("GET", "/api/rooms/", HandleApiRooms);
    Register("GET", "/api/quickPlay/", HandleApiQuickPlay);
    Register("GET", "/api/royale/", HandleApiRoyale);
    Register("GET", "/api/versioncheck/", HandleApiVersionCheck);
    Register("GET", "/api/ugcPurchasables/", HandleApiUgcPurchasables);
    Register("GET", "/api/items/", HandleApiItems);
    Register("GET", "/api/consumables/", HandleApiConsumables);
    Register("GET", "/api/avatar/", HandleApiAvatar);
    Register("GET", "/api/rooms/", HandleApiRooms);
    Register("GET", "/api/relationships/", HandleApiRelationships);
    Register("GET", "/api/messages/", HandleApiMessages);
    Register("GET", "/api/invention/", HandleApiInvention);
    Register("GET", "/api/avatar/", HandleApiAvatar);
    Register("GET", "/api/checklist/", HandleApiChecklist);
    Register("GET", "/api/communityboard/", HandleApiCommunityBoard);
    Register("GET", "/api/chatreport/", HandleApiChatReport);
    Register("GET", "/api/equipment/", HandleApiEquipment);
    Register("GET", "/api/freegifts/", HandleApiFreegifts);
    Register("GET", "/api/gameconfigs/", HandleApiGameConfigs);
    Register("GET", "/api/gamerewards/", HandleApiGameRewards);
    Register("GET", "/api/incentivizedreferrals/", HandleApiIncentivizedReferrals);
    Register("GET", "/api/itemWishlists/", HandleApiItemWishlists);
    Register("GET", "/api/keepsakes/", HandleApiKeepsakes);
    Register("GET", "/api/playerevents/", HandleApiPlayerEvents);
    Register("GET", "/api/recipesEvents/", HandleApiRecipesEvents);
    Register("GET", "/api/storefronts/", HandleApiStorefronts);
    Register("GET", "/api/subscriptionseasons/", HandleApiSubscriptionSeasons);
    
    // Config endpoints
    Register("GET", "/config/", HandleConfig);
    Register("GET", "/config/LoadingScreenTipData", HandleConfigLoadingScreenTipData);
    Register("GET", "/configuration/system.runtime.remoting", HandleConfigRuntime);
    
    // Data endpoints
    Register("GET", "/data/{0}", HandleData);
    Register("GET", "/data/event", HandleDataEvent);
    Register("GET", "/data/events", HandleDataEvents);
    
    // Club endpoints
    Register("POST", "/club/create", HandleClubCreate);
    Register("GET", "/club/search", HandleClubSearch);
    Register("GET", "/club/mine/created", HandleClubMineCreated);
    Register("GET", "/club/mine/member", HandleClubMineMember);
    
    // User endpoints
    Register("GET", "/account/{0}/bio", HandleAccountBio);
    Register("GET", "/account/{0}/clubs", HandleAccountClubs);
    Register("GET", "/accountprivacysettings/recenthistoryvisibility", HandleAccountPrivacy);
    Register("GET", "/accountprivacysettings/{0}", HandleAccountPrivacy);
    Register("GET", "/cachedlogin/current", HandleCachedLoginCurrent);
    Register("GET", "/cachedlogin/forplatformid/{0}/{1}", HandleCachedLoginPlatformId);
    Register("GET", "/cachedlogin/forplatformids", HandleCachedLoginPlatformIds);
    Register("GET", "/cachedlogin/migrate", HandleCachedLoginMigrate);
    Register("GET", "/player/gameserverregionpings", HandlePlayerRegionPings);
    Register("GET", "/player/photonregionpings", HandlePlayerPhotonPings);
    Register("GET", "/player/qos", HandlePlayerQOS);
    Register("GET", "/player/statusvisibility", HandlePlayerStatusVisibility);
    Register("GET", "/player/verifylock", HandlePlayerVerifyLock);
    Register("GET", "/player/vrmovementmode", HandlePlayerVRMovementMode);
    Register("GET", "/player_room_data/{0}", HandlePlayerRoomData);
    Register("GET", "/player/", HandlePlayer);
    
    // Room asset endpoints
    Register("GET", "/roomasset/", HandleRoomAsset);
    
    // Room consumables
    Register("GET", "/roomconsumables/v1/roomConsumable", HandleRoomConsumables);
    Register("GET", "/api/roomconsumables/", HandleRoomConsumables);
    
    // Progression events
    Register("GET", "/progressionEvents/", HandleProgressionEvents);
    Register("GET", "/progressionEvents/active", HandleProgressionEventsActive);
    
    // Storefronts
    Register("GET", "/storefronts/", HandleStorefronts);
    Register("GET", "/storefronts/v1/objectives", HandleStorefrontsObjectives);
    Register("GET", "/storefronts/v1/purchasableitems", HandleStorefrontsPurchasable);
    Register("GET", "/storefronts/v1/buyRoomKey", HandleStorefrontsBuyRoomKey);
    Register("GET", "/storefronts/v2/balance", HandleStorefrontsBalance);
    Register("GET", "/storefronts/v2/buyTier", HandleStorefrontsBuyTier);
    Register("GET", "/storefronts/v2/buyElite", HandleStorefrontsBuyElite);
    Register("GET", "/storefronts/v1/trialInvention/duration", HandleStorefrontsTrialInvention);
    Register("GET", "/storefronts/v3/buyInvention", HandleStorefrontsBuyInvention);
    Register("GET", "/storefronts/v1/adcarouselitems", HandleStorefrontsAds);
    Register("GET", "/storefronts/v1/PurchaseRoomKeyWithCurrency", HandleStorefrontsPurchaseRoomKey);
    
    // Room keys
    Register("GET", "/api/roomkeys/", HandleRoomKeys);
    Register("GET", "/api/roomkeys/v1/", HandleRoomKeys);
    Register("GET", "/api/roomkeys/v1/mine", HandleRoomKeysMine);
    Register("GET", "/api/roomkeys/v1/owns", HandleRoomKeysOwns);
    Register("GET", "/api/roomkeys/v1/owns/bulk", HandleRoomKeysOwnsBulk);
    Register("GET", "/api/roomkeys/v1/create", HandleRoomKeysCreate);
    Register("GET", "/api/roomkeys/v1/awardbulk", HandleRoomKeysAwardBulk);
    Register("GET", "/api/roomkeys/v1/revoke", HandleRoomKeysRevoke);
    
    // Rooms
    Register("GET", "/api/rooms/", HandleApiRooms);
    Register("GET", "/rooms/", HandleRooms);
    Register("GET", "/rooms/base", HandleRoomsBase);
    Register("GET", "/rooms/bulk", HandleRoomsBulk);
    Register("GET", "/rooms/autocomplete_search", HandleRoomsAutoComplete);
    Register("GET", "/rooms/hot", HandleRoomsHot);
    Register("GET", "/rooms/topcreators", HandleRoomsTopCreators);
    Register("GET", "/rooms/search", HandleRoomsSearch);
    Register("GET", "/rooms/recommendations", HandleRoomsRecommendations);
    Register("GET", "/rooms/createdby/me", HandleRoomsCreatedByMe);
    Register("GET", "/rooms/visitedby/me", HandleRoomsVisitedByMe);
    Register("GET", "/rooms/favoritedby/me", HandleRoomsFavoritedByMe);
    Register("GET", "/rooms/cheeredby/me", HandleRoomsCheeredByMe);
    Register("GET", "/rooms/contributedby/{0}", HandleRoomsContributedBy);
    Register("GET", "/rooms/contributedby/me", HandleRoomsContributedByMe);
    Register("GET", "/rooms/ownedby/me", HandleRoomsOwnedByMe);
    Register("GET", "/rooms/ownedby/{0}", HandleRoomsOwnedBy);
    Register("GET", "/rooms/fromcreators", HandleRoomsFromCreators);
    Register("GET", "/rooms/createdby/{0}", HandleRoomsCreatedBy);
    Register("GET", "/rooms/visitedby/{0}", HandleRoomsVisitedBy);
    Register("GET", "/rooms/magic_door", HandleRoomsMagicDoor);
    Register("GET", "/rooms/isrecroomapproved", HandleRoomsIsRecRoomApproved);
    Register("GET", "/rooms/contests/winners", HandleRoomsContestsWinners);
    Register("GET", "/rooms/contestwinners", HandleRoomsContestsWinners);
    Register("GET", "/rooms/moderatedby/me", HandleRoomsModeratedByMe);
    Register("GET", "/rooms/mostactivetoday", HandleRoomsMostActiveToday);
    Register("GET", "/rooms/curated_playlists", HandleRoomsCuratedPlaylists);
    Register("GET", "/rooms/requiring/", HandleRoomsRequiring);
    Register("GET", "/rooms/rro_ids", HandleRoomsRroIds);
    Register("GET", "/rooms/{0}", HandleRoomsDetail);
    Register("GET", "/rooms/{0}/accessibility", HandleRoomsAccessibility);
    Register("GET", "/rooms/{0}/allow_new_users", HandleRoomsAllowNewUsers);
    Register("GET", "/rooms/{0}/assets", HandleRoomsAssets);
    Register("GET", "/rooms/{0}/assets/{1}/hidden", HandleRoomsAssetsHidden);
    Register("GET", "/rooms/{0}/assets/{1}/saves", HandleRoomsAssetsSaves);
    Register("GET", "/rooms/{0}/automute", HandleRoomsAutomute);
    Register("GET", "/rooms/{0}/bans", HandleRoomsBans);
    Register("GET", "/rooms/{0}/bans/bulk", HandleRoomsBansBulk);
    Register("GET", "/rooms/{0}/bans/history", HandleRoomsBansHistory);
    Register("GET", "/rooms/{0}/bans/import", HandleRoomsBansImport);
    Register("GET", "/rooms/{0}/bans/{1}", HandleRoomsBansDetail);
    Register("GET", "/rooms/{0}/bans/{1}/isBanned", HandleRoomsBansIsBanned);
    Register("GET", "/rooms/{0}/boosts/count", HandleRoomsBoostsCount);
    Register("GET", "/rooms/{0}/boosts/counts", HandleRoomsBoostsCounts);
    Register("GET", "/rooms/{0}/boosts/{1}/count", HandleRoomsBoostsCount1);
    Register("GET", "/rooms/{0}/clone", HandleRoomsClone);
    Register("GET", "/rooms/{0}/cloning", HandleRoomsCloning);
    Register("GET", "/rooms/{0}/comments", HandleRoomsComments);
    Register("GET", "/rooms/{0}/creator", HandleRoomsCreator);
    Register("GET", "/rooms/{0}/description", HandleRoomsDescription);
    Register("GET", "/rooms/{0}/entities/publish", HandleRoomsEntitiesPublish);
    Register("GET", "/rooms/{0}/entities/staged", HandleRoomsEntitiesStaged);
    Register("GET", "/rooms/{0}/entities/staged/subroom/{1}", HandleRoomsSubroomEntities);
    Register("GET", "/rooms/{0}/image", HandleRoomsImage);
    Register("GET", "/rooms/{0}/interactionby/me", HandleRoomsInteractionMe);
    Register("GET", "/rooms/{0}/interactionby/me/cheer", HandleRoomsCheerMe);
    Register("GET", "/rooms/{0}/interactionby/me/favorite", HandleRoomsFavoriteMe);
    Register("GET", "/rooms/{0}/leaderboards/{1}", HandleRoomsLeaderboards);
    Register("GET", "/rooms/{0}/loadscreen", HandleRoomsLoadscreen);
    Register("GET", "/rooms/{0}/max_player_calculation_mode", HandleRoomsMaxPlayerCalculationMode);
    Register("GET", "/rooms/{0}/min_level", HandleRoomsMinLevel);
    Register("GET", "/rooms/{0}/name", HandleRoomsName);
    Register("GET", "/rooms/{0}/permissions", HandleRoomsPermissions);
    Register("GET", "/rooms/{0}/playerdata/me", HandleRoomsPlayerDataMe);
    Register("GET", "/rooms/{0}/promo_external", HandleRoomsPromoExternal);
    Register("GET", "/rooms/{0}/promo_images", HandleRoomsPromoImages);
    Register("GET", "/rooms/{0}/publishState", HandleRoomsPublishState);
    Register("GET", "/rooms/{0}/requestManualModeration", HandleRoomsManualModeration);
    Register("GET", "/rooms/{0}/roles", HandleRoomsRoles);
    Register("GET", "/rooms/{0}/roles/{1}", HandleRoomsRoleDetail);
    Register("GET", "/rooms/{0}/roles/{1}/invite", HandleRoomsRoleInvite);
    Register("GET", "/rooms/{0}/similar", HandleRoomsSimilar);
    Register("GET", "/rooms/{0}/snapshots/{1}/loadinfo", HandleRoomsSnapshotsLoadinfo);
    Register("GET", "/rooms/{0}/subrooms", HandleRoomsSubrooms);
    Register("GET", "/rooms/{0}/subrooms/{1}", HandleRoomsSubroomDetail);
    Register("GET", "/rooms/{0}/subrooms/{1}/accessibility", HandleRoomsSubroomAccessibility);
    Register("GET", "/rooms/{0}/subrooms/{1}/autostage", HandleRoomsSubroomAutostage);
    Register("GET", "/rooms/{0}/subrooms/{1}/clone", HandleRoomsSubroomClone);
    Register("GET", "/rooms/{0}/subrooms/{1}/data", HandleRoomsSubroomData);
    Register("GET", "/rooms/{0}/subrooms/{1}/datasaves/{2}/requestAutomatedModeration", HandleRoomsSubroomDatasaveModeration);
    Register("GET", "/rooms/{0}/subrooms/{1}/default_matchmaking_policy", HandleRoomsSubroomDefaultMatchmaking);
    Register("GET", "/rooms/{0}/subrooms/{1}/maxplayers", HandleRoomsSubroomMaxplayers);
    Register("GET", "/rooms/{0}/subrooms/{1}/modify", HandleRoomsSubroomModify);
    Register("GET", "/rooms/{0}/subrooms/{1}/move", HandleRoomsSubroomMove);
    Register("GET", "/rooms/{0}/subrooms/{1}/name", HandleRoomsSubroomName);
    Register("GET", "/rooms/{0}/subrooms/{1}/permissions", HandleRoomsSubroomPermissions);
    Register("GET", "/rooms/{0}/subrooms/{1}/publish_save", HandleRoomsSubroomPublishSave);
    Register("GET", "/rooms/{0}/subrooms/{1}/saves/no_unity_assets", HandleRoomsSubroomSaves);
    Register("GET", "/rooms/{0}/subrooms/{1}/saves/{2}", HandleRoomsSubroomSavesDetail);
    Register("GET", "/rooms/{0}/tags", HandleRoomsTags);
    Register("GET", "/rooms/{0}/v2/modify", HandleRoomsV2Modify);
    Register("GET", "/rooms/{0}/v2/restrictions", HandleRoomsV2Restrictions);
    Register("GET", "/rooms/{0}/voice_chat_encryption", HandleRoomsVoiceEncryption);
    Register("GET", "/rooms/{0}/warning", HandleRoomsWarning);
    
    // Inventions
    Register("GET", "/invention/", HandleInvention);
    Register("GET", "/api/inventions/", HandleApiInventions);
    Register("GET", "/api/inventions/v1/featured", HandleInventionsFeatured);
    Register("GET", "/api/inventions/v1/details", HandleInventionsDetails);
    Register("GET", "/api/inventions/v1/fromcreators", HandleInventionsFromCreators);
    Register("GET", "/api/inventions/v1/versions", HandleInventionsVersions);
    Register("GET", "/api/inventions/v1/toptoday", HandleInventionsTopToday);
    Register("GET", "/api/inventions/v1/fulllineageowner", HandleInventionsFullLineageOwner);
    Register("GET", "/api/inventions/v1/dormskinsfromids", HandleInventionsDormSkins);
    Register("GET", "/api/inventions/v1/settags", HandleInventionsSetTags);
    Register("GET", "/api/inventions/v1/report", HandleInventionsReport);
    Register("GET", "/api/inventions/v1/specialTags", HandleInventionsSpecialTags);
    Register("GET", "/api/inventions/v1/tagfilters", HandleInventionsTagFilters);
    Register("GET", "/api/inventions/v1/cheer", HandleInventionsCheer);
    Register("GET", "/api/inventions/v2/search", HandleInventionsSearch);
    Register("GET", "/api/inventions/v2/metadata", HandleInventionsMetadata);
    Register("GET", "/api/inventions/v2/mine", HandleInventionsMine);
    Register("GET", "/api/inventions/v2/update", HandleInventionsUpdate);
    Register("GET", "/api/inventions/v2/updateprice", HandleInventionsUpdatePrice);
    Register("GET", "/api/inventions/v2/unpublish", HandleInventionsUnpublish);
    Register("GET", "/api/inventions/v2/accessibility", HandleInventionsAccessibility);
    Register("GET", "/api/inventions/v2/delete", HandleInventionsDelete);
    Register("GET", "/api/inventions/v2/batch", HandleInventionsBatch);
    Register("GET", "/api/inventions/v8/addversion", HandleInventionsAddVersion);
    Register("GET", "/api/inventions/v9/save", HandleInventionsSave);
    Register("GET", "/api/inventions/v1/featureddormskins", HandleInventionsFeaturedDormSkins);
    
    // Avatar
    Register("GET", "/api/avatar/v1/defaultbaseavataritems", HandleAvatarDefaultBase);
    Register("GET", "/api/avatar/v1/defaultunlocked", HandleAvatarDefaultUnlocked);
    Register("GET", "/api/avatar/v1/lockeditems", HandleAvatarLockedItems);
    Register("GET", "/api/avatar/v1/lockeditems/bulk", HandleAvatarLockedItemsBulk);
    Register("GET", "/api/avatar/v4/items", HandleAvatarItems);
    Register("GET", "/avatar/", HandleAvatar);
    Register("GET", "/econ/customAvatarItems/", HandleEconCustomAvatarItems);
    
    // Messages
    Register("GET", "/api/messages/v1/friendOnlineStatus", HandleMessagesFriendOnlineStatus);
    Register("GET", "/api/messages/v1/sendMultiple", HandleMessagesSendMultiple);
    Register("GET", "/api/messages/v2/get", HandleMessagesGet);
    Register("GET", "/api/messages/v2/send", HandleMessagesSend);
    Register("GET", "/api/messages/v3/delete", HandleMessagesDelete);
    Register("GET", "/messages", HandleMessages);
    Register("GET", "/messages/v1/sendMultiple", HandleMessagesV1SendMultiple);
    
    // Player
    Register("GET", "/api/players/v1/playerPhotoTaggingSetting", HandlePlayersPlayerPhotoTagging);
    Register("GET", "/api/players/v1/progression/xpEarnedToday", HandlePlayersXpEarned);
    Register("GET", "/api/players/v4/current/contact", HandlePlayersCurrentContact);
    Register("GET", "/api/playerevents/", HandlePlayerEvents);
    Register("GET", "/api/playerevents/v1", HandlePlayerEvents1);
    Register("GET", "/api/playerevents/v1/all", HandlePlayerEventsAll);
    Register("GET", "/api/playerevents/v1/all/{0}", HandlePlayerEventsAll1);
    Register("GET", "/api/playerevents/v1/broadcast", HandlePlayerEventsBroadcast);
    Register("GET", "/api/playerevents/v1/bulk", HandlePlayerEventsBulk);
    Register("GET", "/api/playerevents/v1/bulkInvite", HandlePlayerEventsBulkInvite);
    Register("GET", "/api/playerevents/v1/club/{0}", HandlePlayerEventsClub);
    Register("GET", "/api/playerevents/v1/clubs", HandlePlayerEventsClubs);
    Register("GET", "/api/playerevents/v1/deleteResponse", HandlePlayerEventsDeleteResponse);
    Register("GET", "/api/playerevents/v1/report", HandlePlayerEventsReport);
    Register("GET", "/api/playerevents/v1/respond", HandlePlayerEventsRespond);
    Register("GET", "/api/playerevents/v1/room/{0}", HandlePlayerEventsRoom);
    Register("GET", "/api/playerevents/v1/search", HandlePlayerEventsSearch);
    Register("GET", "/api/playerevents/v1/searchlive", HandlePlayerEventsSearchlive);
    Register("GET", "/api/playerevents/v1/tagfilters", HandlePlayerEventsTagFilters);
    Register("GET", "/api/playerevents/v1/{0}", HandlePlayerEvents0);
    Register("GET", "/api/playerevents/v1/{0}/responses", HandlePlayerEventsResponses);
    Register("GET", "/api/playerevents/v2", HandlePlayerEventsV2);
    Register("GET", "/api/playerevents/v2/delete/{0}", HandlePlayerEventsV2Delete);
    Register("GET", "/api/playerevents/v2/{0}", HandlePlayerEventsV20);
    Register("GET", "/api/playerevents/v2/{0}/accessibility", HandlePlayerEventsV2Accessibility);
    Register("GET", "/api/playerevents/v2/{0}/club", HandlePlayerEventsV2Club);
    Register("GET", "/api/playerevents/v2/{0}/description", HandlePlayerEventsV2Description);
    Register("GET", "/api/playerevents/v2/{0}/image", HandlePlayerEventsV2Image);
    Register("GET", "/api/playerevents/v2/{0}/multiinstance", HandlePlayerEventsV2Multi);
    Register("GET", "/api/playerevents/v2/{0}/name", HandlePlayerEventsV2Name);
    Register("GET", "/api/playerevents/v2/{0}/room", HandlePlayerEventsV2Room);
    Register("GET", "/api/playerevents/v2/{0}/tags", HandlePlayerEventsV2Tags);
    Register("GET", "/api/playerevents/v2/{0}/time", HandlePlayerEventsV2Time);
    Register("GET", "/api/playerevents/v2/delete/{0}", HandlePlayerEventsV2Delete1);
    Register("GET", "/api/playerevents/", HandlePlayerEvents);
    
    // Player items
    Register("GET", "/api/playeritems/v1/isavataritemowned/bulk", HandlePlayerItemsIsAvatarOwnedBulk);
    Register("GET", "/api/playeritems/v1/isavataritemowned/{0}/{1}", HandlePlayerItemsIsAvatarOwned);
    Register("GET", "/api/playeritems/v1/isequipmentitemowned/bulk", HandlePlayerItemsIsEquipmentOwnedBulk);
    Register("GET", "/api/playeritems/v1/isequipmentitemowned/{0}/{1}", HandlePlayerItemsIsEquipmentOwned);
    
    // Relationships
    Register("GET", "/api/relationships/v1/addfriendwithcode", HandleRelationshipsAddFriendCode);
    Register("GET", "/api/relationships/v1/bulkignoreplatformusers", HandleRelationshipsBulkIgnore);
    Register("GET", "/api/relationships/v1/favorite", HandleRelationshipsFavorite);
    Register("GET", "/api/relationships/v1/unfavorite", HandleRelationshipsUnfavorite);
    Register("GET", "/api/relationships/v2/acceptfriendrequest", HandleRelationshipsAcceptRequest);
    Register("GET", "/api/relationships/v2/addfriend", HandleRelationshipsAddFriend);
    Register("GET", "/api/relationships/v2/get", HandleRelationshipsGet);
    Register("GET", "/api/relationships/v2/removefriend", HandleRelationshipsRemoveFriend);
    Register("GET", "/api/relationships/v2/sendfriendrequest", HandleRelationshipsSendRequest);
    Register("GET", "/api/relationships/v2/updateVoiceVolume", HandleRelationshipsVoiceVolume);
    Register("GET", "/api/relationships/", HandleRelationships);
    Register("GET", "/relationships/", HandleRelationships);
    Register("GET", "/relationships/mutualfriends", HandleRelationshipsMutualFriends);
    Register("GET", "/relationships/sendfriendintroductions", HandleRelationshipsSendIntroductions);
    
    // Consumables
    Register("GET", "/api/consumables/v1/consume", HandleConsumablesConsume);
    Register("GET", "/api/consumables/v1/transfer", HandleConsumablesTransfer);
    Register("GET", "/api/consumables/v1/ugc/create", HandleConsumablesUgcCreate);
    Register("GET", "/api/consumables/v1/updateActive", HandleConsumablesUpdateActive);
    Register("GET", "/api/consumables/v2/getUnlocked", HandleConsumablesGetUnlocked);
    Register("GET", "/api/consumables/", HandleConsumables);
    Register("GET", "/consumables/", HandleConsumables);
    
    // Inventory
    Register("GET", "/api/equipment/v1/update", HandleEquipmentUpdate);
    Register("GET", "/api/equipment/v2/getUnlocked", HandleEquipmentGetUnlocked);
    
    // Room currency
    Register("GET", "/api/roomcurrencies/v1/updateCurrency", HandleRoomCurrenciesUpdate);
    Register("GET", "/api/roomcurrencies/v1/getPurchaseOffersBatch", HandleRoomCurrenciesGetPurchaseOffersBatch);
    Register("GET", "/api/roomcurrencies/v1/deletePurchaseOffer", HandleRoomCurrenciesDeletePurchaseOffer);
    Register("GET", "/api/roomcurrencies/v1/updatePurchaseOffer", HandleRoomCurrenciesUpdatePurchaseOffer);
    Register("GET", "/api/roomcurrencies/v1/createCurrency", HandleRoomCurrenciesCreateCurrency);
    Register("GET", "/api/roomcurrencies/v1/createPurchaseOffer", HandleRoomCurrenciesCreatePurchaseOffer);
    Register("GET", "/api/roomcurrencies/v1/getBalance", HandleRoomCurrenciesGetBalance);
    Register("GET", "/api/roomcurrencies/v1/getAllBalances", HandleRoomCurrenciesGetAllBalances);
    Register("GET", "/api/roomcurrencies/v1/currencies", HandleRoomCurrenciesGetCurrencies);
    Register("GET", "/api/roomcurrencies/v1/awardCurrency/bulk", HandleRoomCurrenciesAwardCurrency);
    Register("GET", "/api/roomcurrencies/v1/getBalance", HandleRoomCurrenciesGetBalance);
    Register("GET", "/api/roomcurrencies/", HandleRoomCurrencies);
    Register("GET", "/api/roomEarningsDistributions/", HandleRoomEarningsDistributions);
    Register("GET", "/api/roomEarningsDistributions/v1/earningsDistribution", HandleRoomEarningsDistribution);
    Register("GET", "/api/roomEarningsDistributions", HandleRoomEarningsDistributions);
    
    // Quick Play
    Register("GET", "/api/quickPlay/v1/getandclear", HandleQuickPlayGetAndClear);
    
    // Royale
    Register("GET", "/api/royale/v1/current", HandleRoyaleCurrent);
    Register("GET", "/api/royale/v2/matchcomplete", HandleRoyaleMatchComplete);
    
    // Room Earnings
    Register("GET", "/api/roomconsumables/v1/roomConsumable", HandleRoomConsumables);
    Register("GET", "/api/roomconsumables/v1/roomConsumable/awardBulk", HandleRoomConsumablesAwardBulk);
    Register("GET", "/api/roomconsumables/v1/roomConsumable/{0}/purchase/currency", HandleRoomConsumablesPurchaseCurrency);
    Register("GET", "/api/roomconsumables/v1/roomConsumable/{0}/purchase/tokens", HandleRoomConsumablesPurchaseTokens);
    Register("GET", "/api/roomconsumables/", HandleRoomConsumables);
    
    // Inventory
    Register("GET", "/econ/roomInventory/v2/award", HandleRoomInventoryAward);
    Register("GET", "/econ/roomInventory", HandleRoomInventory);
    
    // Community Board
    Register("GET", "/api/communityboard/v2/current", HandleCommunityBoardCurrent);
    
    // Checklist
    Register("GET", "/api/checklist/v1/complete", HandleChecklistComplete);
    Register("GET", "/api/checklist/v1/current", HandleChecklistCurrent);
    
    // Chat Report
    Register("GET", "/api/chatreport/createChatReport", HandleChatReportCreate);
    
    // Reports
    Register("GET", "/api/roomEarningsDistributions/", HandleRoomEarningsDistributions);
    Register("GET", "/api/roomEarningsDistributions/v1/earningsDistribution", HandleRoomEarningsDistribution);
    
    // Ban appeal
    Register("GET", "/api/banappeal/generateCode", HandleBanAppealCode);
    
    // Cheer
    Register("GET", "/api/PlayerCheer/v1/SetSelectedCheer", HandlePlayerCheerSelect);
    Register("GET", "/api/PlayerCheer/v1/create", HandlePlayerCheerCreate);
    
    // Reporting
    Register("GET", "/api/PlayerReporting/v1/deviceId", HandlePlayerReportingDeviceId);
    Register("GET", "/api/PlayerReporting/v1/hile", HandlePlayerReportingHile);
    Register("GET", "/api/PlayerReporting/v1/instantKick", HandlePlayerReportingInstantKick);
    Register("GET", "/api/PlayerReporting/v1/modKick", HandlePlayerReportingModKick);
    Register("GET", "/api/PlayerReporting/v1/moderationBlockDetails", HandlePlayerReportingModBlockDetails);
    Register("GET", "/api/PlayerReporting/v1/referee", HandlePlayerReportingReferee);
    Register("GET", "/api/PlayerReporting/v1/roomModKick", HandlePlayerReportingRoomModKick);
    Register("GET", "/api/PlayerReporting/v1/voteToKickReasons", HandlePlayerReportingVoteKickReasons);
    Register("GET", "/api/PlayerReporting/v3/create", HandlePlayerReportingV3Create);
    Register("GET", "/api/PlayerReporting/v3/voteToKick", HandlePlayerReportingV3VoteKick);
    
    // Age verification
    Register("GET", "/api/ageverification/generateCode", HandleAgeVerificationCode);
    
    // Apple
    Register("GET", "/api/apple/musicpromotion/active", HandleAppleMusicPromotionActive);
    Register("GET", "/api/apple/musicpromotion/code", HandleAppleMusicPromotionCode);
    
    // MakerAI
    Register("GET", "/api/makerai/activatefreetrial", HandleMakerAIFreeTrial);
    Register("GET", "/api/makerai/checkfreetrialeligibility", HandleMakerAICheckEligibility);
    Register("GET", "/api/makerai/freetrialconfig", HandleMakerAIFreeTrialConfig);
    
    // Incentivized referrals
    Register("GET", "/api/incentivizedreferrals/claim", HandleIncentivizedReferralsClaim);
    Register("GET", "/api/incentivizedreferrals/progress", HandleIncentivizedReferralsProgress);
    Register("GET", "/api/incentivizedreferrals/referrals", HandleIncentivizedReferralsReferrals);
    
    // Influencer program
    Register("GET", "/api/influencerpartnerprogram/influencer", HandleInfluencerProgramInfluencer);
    Register("GET", "/api/influencerpartnerprogram/influencers", HandleInfluencerProgramInfluencers);
    Register("GET", "/api/influencerpartnerprogram/myinfluencer", HandleInfluencerProgramMy);
    Register("GET", "/api/influencerpartnerprogram/remove", HandleInfluencerProgramRemove);
    Register("GET", "/api/influencerpartnerprogram/support", HandleInfluencerProgramSupport);
    
    // October tricks
    Register("GET", "/api/octoberTricks2025/collectghost", HandleOctoberTricksCollectGhost);
    
    // Oktoberfest
    Register("GET", "/api/oktoberfeast2025/consume", HandleOktoberfeastConsume);
    
    // Sanitize
    Register("GET", "/api/sanitize/v1/isPure", HandleSanitizeIsPure);
    
    // Screen share reports
    Register("GET", "/api/screensharereports/v1/report", HandleScreenShareReportsReport);
    
    // Test case management
    Register("GET", "/api/testcasemanagement/", HandleTestCaseManagement);
    Register("GET", "/api/testcasemanagement/v1/testcase/", HandleTestCaseV1Testcase);
    Register("GET", "/api/testcasemanagement/v1/testpasssummary", HandleTestCaseV1TestPassSummary);
    Register("GET", "/api/testcasemanagement/v1/testplans", HandleTestCaseV1TestPlans);
    
    // Club reporting
    Register("GET", "/api/clubreporting/v1/report", HandleClubReportingReport);
    
    // Progression events
    Register("GET", "/api/progressionEvents/", HandleProgressionEvents);
    Register("GET", "/api/progressionEvents/active", HandleProgressionEventsActive);
    Register("GET", "/api/progressionEvents/", HandleProgressionEvents);
    
    // Timed events
    Register("GET", "/api/timeLimitedEvents/", HandleTimeLimitedEvents);
    Register("GET", "/api/timedCurrencyGrants/", HandleTimedCurrencyGrants);
    Register("GET", "/api/timedCurrencyGrants/update", HandleTimedCurrencyGrantsUpdate);
    Register("GET", "/api/timedRewardTracks/", HandleTimedRewardTracks);
    Register("GET", "/api/timedRewardTrees/", HandleTimedRewardTrees);
    
    // Storefronts
    Register("GET", "/api/storefronts/v1/buyProgressionEventXpBoost", HandleStorefrontsBuyProgressionBoost);
    Register("GET", "/api/storefronts/v1/buyPurchaseReminder", HandleStorefrontsBuyPurchaseReminder);
    Register("GET", "/api/storefronts/v1/buyForFreeGiftButton", HandleStorefrontsBuyForFreeGiftButton);
    Register("GET", "/api/storefronts/v1/PurchaseRoomKeyWithCurrency", HandleStorefrontsPurchaseRoomKeyWithCurrency);
    Register("GET", "/api/storefronts/v1/buyRoomKey", HandleStorefrontsBuyRoomKey);
    Register("GET", "/api/storefronts/v2/trialInvention", HandleStorefrontsTrialInvention);
    Register("GET", "/api/storefronts/v3/buyInvention", HandleStorefrontsBuyInvention);
    Register("GET", "/api/storefronts/v1/toptoday", HandleStorefrontsToptoday);
    Register("GET", "/api/storefronts/v1/adcarouselitems", HandleStorefrontsAdCarouselItems);
    Register("GET", "/api/storefronts/v1/trialInvention/duration", HandleStorefrontsTrialInventionDuration);
    Register("GET", "/api/storefronts/v2/trialInvention", HandleStorefrontsTrialInvention);
    Register("GET", "/api/storefronts/v2/balance", HandleStorefrontsBalance);
    Register("GET", "/api/storefronts/v2/buyElite", HandleStorefrontsBuyElite);
    Register("GET", "/api/storefronts/v2/buyTier", HandleStorefrontsBuyTier);
    
    // Subscription seasons
    Register("GET", "/api/subscriptionseasons/v1/seasons/current", HandleSubscriptionSeasonsCurrent);
    
    // UGC Purchasables
    Register("GET", "/api/ugcPurchasables/v1/items/bulk", HandleUgcPurchasablesItems);
    Register("GET", "/api/ugcPurchasables/", HandleUgcPurchasables);
    
    // Version check
    Register("GET", "/api/versioncheck/v4", HandleVersionCheckV4);
    Register("GET", "/api/versioncheck/islandedversions", HandleVersionCheckIslanded);
    
    // Purchasable XP Boosts
    Register("GET", "/api/purchasableXpBoosts/", HandlePurchasableXpBoosts);
    Register("GET", "/api/purchasableXpBoosts/activations", HandlePurchasableXpBoostsActivations);
    
    // PlayStation Plus
    Register("GET", "/api/playstationplus/membership", HandlePlayStationPlusMembership);
    Register("GET", "/api/playstationplus/expire", HandlePlayStationPlusExpire);
    
    // Player warnings
    Register("GET", "/api/playerwarnings", HandlePlayerWarnings);
    Register("GET", "/api/playerwarnings/acknowledge", HandlePlayerWarningsAcknowledge);
    
    // Storefronts
    Register("GET", "/api/storefronts/", HandleStorefronts);
    
    // Room Earnings
    Register("GET", "/api/roomEarningsDistributions/", HandleRoomEarningsDistributions);
    
    // Offline invite
    Register("GET", "/api/offlineinvite/v1/send", HandleOfflineInviteSend);
    
    // Distance moved reward tracks
    Register("GET", "/api/distanceMovedRewardTracks/distance", HandleDistanceMovedRewardTracks);
    
    // Custom Avatar Items
    Register("GET", "/api/customAvatarItems/v1/bulk", HandleCustomAvatarItemsBulk);
    Register("GET", "/api/customAvatarItems/v1/design", HandleCustomAvatarItemsDesign);
    Register("GET", "/api/customAvatarItems/v1/featured", HandleCustomAvatarItemsFeatured);
    Register("GET", "/api/customAvatarItems/v1/hot", HandleCustomAvatarItemsHot);
    Register("GET", "/api/customAvatarItems/v1/isCreationAllowedForAccount", HandleCustomAvatarItemsCreationAllowed);
    Register("GET", "/api/customAvatarItems/v1/isCreationEnabled", HandleCustomAvatarItemsCreationEnabled);
    Register("GET", "/api/customAvatarItems/v1/isRenderingEnabled", HandleCustomAvatarItemsRenderingEnabled);
    Register("GET", "/api/customAvatarItems/v1/me", HandleCustomAvatarItemsMe);
    Register("GET", "/api/customAvatarItems/v1/pricingconfig", HandleCustomAvatarItemsPricingConfig);
    Register("GET", "/api/customAvatarItems/v1/search", HandleCustomAvatarItemsSearch);
    Register("GET", "/api/customAvatarItems/", HandleCustomAvatarItems);
    
    // Custom Avatar Items v2
    Register("GET", "/api/customAvatarItems", HandleCustomAvatarItems1);
    Register("GET", "/api/customAvatarItems/v1", HandleCustomAvatarItemsV1);
    
    // Player reputation
    Register("GET", "/api/playerReputation/v2/bulk", HandlePlayerReputationBulk);
    
    // Players progression
    Register("GET", "/api/players/v2/progression/bulk", HandlePlayersProgressionBulk);
    
    // Room consumables
    Register("GET", "/api/roomconsumables/v1/roomConsumable", HandleRoomConsumables);
    Register("GET", "/api/roomconsumables/v1/roomConsumable/awardBulk", HandleRoomConsumablesAwardBulk);
    Register("GET", "/api/roomconsumables/v1/roomConsumable/{0}/purchase/currency", HandleRoomConsumablesPurchaseCurrency);
    Register("GET", "/api/roomconsumables/v1/roomConsumable/{0}/purchase/tokens", HandleRoomConsumablesPurchaseTokens);
    
    // Images
    Register("GET", "/api/images/v6", HandleImagesV6);
    Register("GET", "/api/images/v5/bulk", HandleImagesV5Bulk);
    Register("GET", "/api/images/v5/cheered/bulk", HandleImagesV5CheeredBulk);
    Register("GET", "/api/images/v4/uploadsaved", HandleImagesV4UploadSaved);
    Register("GET", "/api/images/v2/modifyaccessibility", HandleImagesV2ModifyAccessibility);
    Register("GET", "/api/images/v2/named", HandleImagesV2Named);
    Register("GET", "/api/images/v1/cheer", HandleImagesV1Cheer);
    Register("GET", "/api/images/v1/deletesaved", HandleImagesV1DeleteSaved);
    Register("GET", "/api/images/v1/listsaved", HandleImagesV1ListSaved);
    Register("GET", "/api/images/v1/modifydescription", HandleImagesV1ModifyDescription);
    Register("GET", "/api/images/v1/sendlink", HandleImagesV1SendLink);
    Register("GET", "/api/images/v1/slideshow", HandleImagesV1Slideshow);
    Register("GET", "/api/images/", HandleImages);
    
    // Game configs
    Register("GET", "/api/gameconfigs/v1/all", HandleGameConfigsAll);
    
    // Game rewards
    Register("GET", "/api/gamerewards/v1/pending", HandleGameRewardsPending);
    Register("GET", "/api/gamerewards/v1/request", HandleGameRewardsRequest);
    Register("GET", "/api/gamerewards/v1/select", HandleGameRewardsSelect);
    
    // Free gifts
    Register("GET", "/api/freegifts/v1/sendmultiple", HandleFreeGiftsSendMultiple);
    
    // External friend invite
    Register("GET", "/api/externalfriendinvite/v1/createplatforminvite", HandleExternalFriendInviteCreatePlatform);
    Register("GET", "/api/externalfriendinvite/v1/getplatformreferrers", HandleExternalFriendInviteGetPlatformReferrers);
    Register("GET", "/api/externalfriendinvite/v1/gettextmessagereferrers", HandleExternalFriendInviteGetTextMessageReferrers);
    Register("GET", "/api/externalfriendinvite/v1/sendtextmessageinvite", HandleExternalFriendInviteSendTextMessage);
    
    // Item wishlists
    Register("GET", "/api/itemWishlists/v1/isonwishlist/bulk", HandleItemWishlistsIsOnWishlistBulk);
    Register("GET", "/api/itemWishlists/v1/wishlist/add", HandleItemWishlistsAdd);
    Register("GET", "/api/itemWishlists/v1/wishlist/me?False", HandleItemWishlistsMeFalse);
    Register("GET", "/api/itemWishlists/v1/wishlist/me?True", HandleItemWishlistsMeTrue);
    Register("GET", "/api/itemWishlists/v1/wishlist/remove", HandleItemWishlistsRemove);
    Register("GET", "/api/itemWishlists/", HandleItemWishlists);
    
    // Keepsakes
    Register("GET", "/api/keepsakes/categories", HandleKeepsakesCategories);
    Register("GET", "/api/keepsakes/events", HandleKeepsakesEvents);
    Register("GET", "/api/keepsakes/globalconfig", HandleKeepsakesGlobalConfig);
    Register("GET", "/api/keepsakes", HandleKeepsakes);
    
    // Announcements
    Register("GET", "/api/announcement/v1/get", HandleAnnouncementGet);
    Register("GET", "/api/announcements/", HandleApiAnnouncements);
    Register("GET", "/api/announcements/mine", HandleApiAnnouncementsMine);
    Register("GET", "/api/announcements/subscription/mine", HandleApiAnnouncementsSubscription);
    Register("GET", "/api/announcements/v2/mine/unread", HandleApiAnnouncementsV2MineUnread);
    Register("GET", "/api/announcements/v2/mine/unread?sendAnnouncements=true", HandleApiAnnouncementsV2MineUnread1);
    Register("GET", "/api/announcements/v2/subscription/mine/unread", HandleApiAnnouncementsV2SubscriptionUnread);
    Register("GET", "/api/announcements/v2/subscription/mine/unread?sendAnnouncements=true", HandleApiAnnouncementsV2SubscriptionUnread1);
    Register("GET", "/announcements/", HandleAnnouncements);
    Register("GET", "/announcements/club/{0}", HandleAnnouncementsClub);
    Register("GET", "/announcements/club/{0}/{1}", HandleAnnouncementsClub1);
    Register("GET", "/announcements/club/{0}/{1}/read", HandleAnnouncementsClubRead);
    
    // Club
    Register("GET", "/club/", HandleClub);
    Register("GET", "/club/create", HandleClubCreate);
    Register("GET", "/club/{0}", HandleClubDetail);
    Register("GET", "/club/{0}/additionalimage/{1}", HandleClubAdditionalImage);
    Register("GET", "/club/{0}/clubChatEnabled", HandleClubChatEnabled);
    Register("GET", "/club/{0}/clubhouse", HandleClubClubhouse);
    Register("GET", "/club/{0}/details", HandleClubDetails);
    Register("GET", "/club/{0}/hasDisabledClubChat", HandleClubHasDisabledChat);
    Register("GET", "/club/{0}/mainimage", HandleClubMainImage);
    Register("GET", "/club/{0}/members", HandleClubMembers);
    Register("GET", "/club/{0}/members/acceptinvite", HandleClubMembersAcceptInvite);
    Register("GET", "/club/{0}/members/acceptrequest", HandleClubMembersAcceptRequest);
    Register("GET", "/club/{0}/members/acceptrequests", HandleClubMembersAcceptRequests);
    Register("GET", "/club/{0}/members/ban", HandleClubMembersBan);
    Register("GET", "/club/{0}/members/banned", HandleClubMembersBanned);
    Register("GET", "/club/{0}/members/changetype", HandleClubMembersChangeType);
    Register("GET", "/club/{0}/members/declineinvite", HandleClubMembersDeclineInvite);
    Register("GET", "/club/{0}/members/denyrequest", HandleClubMembersDenyRequest);
    Register("GET", "/club/{0}/members/denyrequests", HandleClubMembersDenyRequests);
    Register("GET", "/club/{0}/members/directJoin", HandleClubMembersDirectJoin);
    Register("GET", "/club/{0}/members/invite", HandleClubMembersInvite);
    Register("GET", "/club/{0}/members/invitemembers", HandleClubMembersInviteMembers);
    Register("GET", "/club/{0}/members/leave", HandleClubMembersLeave);
    Register("GET", "/club/{0}/members/remove", HandleClubMembersRemove);
    Register("GET", "/club/{0}/members/requests", HandleClubMembersRequests);
    Register("GET", "/club/{0}/members/requests/search", HandleClubMembersRequestsSearch);
    Register("GET", "/club/{0}/members/requesttojoin", HandleClubMembersRequestToJoin);
    Register("GET", "/club/{0}/members/search", HandleClubMembersSearch);
    Register("GET", "/club/{0}/members/unban", HandleClubMembersUnban);
    Register("GET", "/club/{0}/members/{1}", HandleClubMembersDetail);
    Register("GET", "/club/{0}/minlevel", HandleClubMinLevel);
    Register("GET", "/club/{0}/modify", HandleClubModify);
    Register("GET", "/club/{0}/modifydetails", HandleClubModifyDetails);
    Register("GET", "/club/{0}/permissions/{1}", HandleClubPermissions);
    Register("GET", "/club/{0}/club/{0}", HandleClub1);
    Register("GET", "/club/{0}/mainimage/{0}", HandleClubMainImage1);
    
    // Club events
    Register("GET", "/club_events/{0}", HandleClubEvents);
    
    // Clubhousesearch
    Register("GET", "/clubhousesearch/mostactivenow", HandleClubhouseSearchMostActiveNow);
    
    // Config
    Register("GET", "/config/categories", HandleConfigCategories);
    
    // Connect
    Register("GET", "/connect/deviceauthorization", HandleConnectDeviceAuthorization);
    Register("GET", "/connect/token", HandleConnectToken);
    
    // Room instance
    Register("GET", "/roominstance/{0}/inprogress", HandleRoomInstanceInprogress);
    Register("GET", "/roominstance/{0}/markprivate", HandleRoomInstanceMarkPrivate);
    Register("GET", "/roominstance/{0}/matchpolicy", HandleRoomInstanceMatchPolicy);
    Register("GET", "/roominstance/{0}/refresh", HandleRoomInstanceRefresh);
    Register("GET", "/roominstance/{0}/reportjoinresult", HandleRoomInstanceReportJoinResult);
    Register("GET", "/roominstance/{0}/roomCode", HandleRoomInstanceRoomCode);
    
    // Room interaction
    Register("GET", "/roominteraction/{0}", HandleRoomInteraction);
    
    // RoomieAI
    Register("GET", "/roomieai/chat", HandleRoomieAIChat);
    Register("GET", "/roomieai/describeimage", HandleRoomieAIDescribeImage);
    Register("GET", "/roomieai/describeimagefile", HandleRoomieAIDescribeImageFile);
    Register("GET", "/roomieai/session/spend", HandleRoomieAISessionSpend);
    Register("GET", "/roomieai/user/access", HandleRoomieAIUserAccess);
    Register("GET", "/roomieai/user/fact/", HandleRoomieAIUserFact);
    Register("GET", "/roomieai/user/facts", HandleRoomieAIUserFacts);
    Register("GET", "/roomieai/user/processconversation", HandleRoomieAIUserProcessConversation);
    
    // Player items
    Register("GET", "/api/playeritems/", HandlePlayerItems);
    
    // Subscription seasons
    Register("GET", "/api/subscriptionseasons/v1/seasons/current", HandleSubscriptionSeasonsCurrent);
    
    // Storefronts
    Register("GET", "/api/storefronts/v1/", HandleStorefrontsV1);
    
    // Test case management
    Register("GET", "/api/testcasemanagement/v1/testcase/", HandleTestCaseV1Testcase);
    Register("GET", "/api/testcasemanagement/v1/testpasssummary", HandleTestCaseV1TestPassSummary);
    Register("GET", "/api/testcasemanagement/v1/testplans", HandleTestCaseV1TestPlans);
    
    // Activities
    Register("GET", "/api/activities/charades/v1/words/{0}", HandleActivitiesCharadesWords);
    
    // UGC
    Register("GET", "/inventionsbycreators/{0}/{1}/{2}", HandleInventionsByCreators);
    
    // Player
    Register("GET", "/player/avoidjuniors", HandlePlayerAvoidJuniors);
    Register("GET", "/player/exclusivelogin", HandlePlayerExclusiveLogin);
    Register("GET", "/player/heartbeat", HandlePlayerHeartbeat);
    Register("GET", "/player/logout", HandlePlayerLogout);
    Register("GET", "/player/notifydisconnect", HandlePlayerNotifyDisconnect);
    Register("GET", "/player/statusvisibility", HandlePlayerStatusVisibility);
    Register("GET", "/player/verifylock", HandlePlayerVerifyLock);
    Register("GET", "/player/vrmovementmode", HandlePlayerVRMovementMode);
    Register("GET", "/player/", HandlePlayer);
    
    // Room data
    Register("GET", "/player_room_data/{0}", HandlePlayerRoomData);
    
    // Images
    Register("GET", "/api/images/", HandleImages);
    
    // UGC
    Register("GET", "/api/ugcPurchasables/v1/items/bulk", HandleUgcPurchasablesItems);
    Register("GET", "/api/ugcPurchasables/", HandleUgcPurchasables);
    
    // Room keys
    Register("GET", "/api/roomkeys/", HandleRoomKeys);
    Register("GET", "/api/roomkeys/v1/", HandleRoomKeys);
    Register("GET", "/api/roomkeys/v1/mine", HandleRoomKeysMine);
    Register("GET", "/api/roomkeys/v1/owns", HandleRoomKeysOwns);
    Register("GET", "/api/roomkeys/v1/owns/bulk", HandleRoomKeysOwnsBulk);
    Register("GET", "/api/roomkeys/v1/create", HandleRoomKeysCreate);
    Register("GET", "/api/roomkeys/v1/awardbulk", HandleRoomKeysAwardBulk);
    Register("GET", "/api/roomkeys/v1/revoke", HandleRoomKeysRevoke);
    
    // Storefronts
    Register("GET", "/api/storefronts/", HandleStorefronts);
    
    // Room consumables
    Register("GET", "/api/roomconsumables/", HandleRoomConsumables);
    Register("GET", "/api/roomconsumables/v1/roomConsumable", HandleRoomConsumables);
    Register("GET", "/api/roomconsumables/v1/roomConsumable/awardBulk", HandleRoomConsumablesAwardBulk);
    Register("GET", "/api/roomconsumables/v1/roomConsumable/{0}/purchase/currency", HandleRoomConsumablesPurchaseCurrency);
    Register("GET", "/api/roomconsumables/v1/roomConsumable/{0}/purchase/tokens", HandleRoomConsumablesPurchaseTokens);
    
    // Progression events
    Register("GET", "/api/progressionEvents/", HandleProgressionEvents);
    Register("GET", "/api/progressionEvents/active", HandleProgressionEventsActive);
    Register("GET", "/api/progressionEvents/", HandleProgressionEvents);
    
    // Timed events
    Register("GET", "/api/timeLimitedEvents/", HandleTimeLimitedEvents);
    Register("GET", "/api/timedCurrencyGrants/", HandleTimedCurrencyGrants);
    Register("GET", "/api/timedCurrencyGrants/update", HandleTimedCurrencyGrantsUpdate);
    Register("GET", "/api/timedRewardTracks/", HandleTimedRewardTracks);
    Register("GET", "/api/timedRewardTrees/", HandleTimedRewardTrees);
    
    // Subscription seasons
    Register("GET", "/api/subscriptionseasons/v1/seasons/current", HandleSubscriptionSeasonsCurrent);
    
    // Purchasable XP Boosts
    Register("GET", "/api/purchasableXpBoosts/", HandlePurchasableXpBoosts);
    Register("GET", "/api/purchasableXpBoosts/activations", HandlePurchasableXpBoostsActivations);
    
    // PlayStation Plus
    Register("GET", "/api/playstationplus/membership", HandlePlayStationPlusMembership);
    Register("GET", "/api/playstationplus/expire", HandlePlayStationPlusExpire);
    
    // Player warnings
    Register("GET", "/api/playerwarnings", HandlePlayerWarnings);
    Register("GET", "/api/playerwarnings/acknowledge", HandlePlayerWarningsAcknowledge);
    
    // Storefronts
    Register("GET", "/api/storefronts/", HandleStorefronts);
}

void Register(string method, string pattern, Func<string, object> handler)
{
    _handlers.Add(new EndpointHandler { Method = method, Pattern = pattern, Handler = handler });
}

static void Handle(TcpClient client)
{
    string who = client.Client.RemoteEndPoint?.ToString() ?? "?";
    Log($"[{who}] TCP connect");
    try
    {
        using var ssl = new SslStream(client.GetStream(), false);
        await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = cert,
            ClientCertificateRequired = false,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        });
        Log($"[{who}] TLS ok: {ssl.SslProtocol} {ssl.NegotiatedCipherSuite} sni={ssl.TargetHostName}");

        var buf = new byte[64 * 1024];
        int n = await ssl.ReadAsync(buf);
        string req = Encoding.Latin1.GetString(buf, 0, n);
        Log($"[{who}] REQUEST ({n} bytes):\n{req}");

        string response = ProcessRequest(req);
        
        if (response != null)
        {
            var resp = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {response.Length}\r\nConnection: close\r\n\r\n{response}";
            await ssl.WriteAsync(Encoding.ASCII.GetBytes(resp));
            await ssl.FlushAsync();
        }
        else
        {
            var resp = $"HTTP/1.1 404 Not Found\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{{}}";
            await ssl.WriteAsync(Encoding.ASCII.GetBytes(resp));
            await ssl.FlushAsync();
        }
    }
    catch (Exception e)
    {
        Log($"[{who}] ERROR {e.GetType().Name}: {e.Message}");
    }
    finally { client.Dispose(); }
}

string ProcessRequest(string request)
{
    var lines = request.Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries);
    if (lines.Length < 1) return null;
    
    var requestLine = lines[0].Split(' ');
    if (requestLine.Length < 2) return null;
    
    string method = requestLine[0].ToUpper();
    string path = requestLine[1];
    
    // Try to match handlers
    foreach (var handler in _handlers)
    {
        if (handler.Method != method) continue;
        
        string pattern = handler.Pattern;
        
        // Simple pattern matching with parameter extraction
        if (MatchPattern(path, pattern))
        {
            // Extract parameters from pattern
            string parameters = ExtractParameters(path, pattern);
            return JsonSerializer.Serialize(handler.Handler(parameters));
        }
    }
    
    return null;
}

bool MatchPattern(string path, string pattern)
{
    if (pattern.Contains("/{0}"))
    {
        var parts = path.Split('/');
        var patternParts = pattern.Split('/');
        
        if (parts.Length != patternParts.Length) return false;
        
        for (int i = 0; i < patternParts.Length; i++)
        {
            if (patternParts[i] == "/{0}")
            {
                // Parameter match, continue
            }
            else if (patternParts[i] != parts[i])
            {
                return false;
            }
        }
        return true;
    }
    
    return path == pattern;
}

string ExtractParameters(string path, string pattern)
{
    if (pattern.Contains("/{0}"))
    {
        var parts = path.Split('/');
        var parameters = new System.Collections.Generic.List<string>();
        
        foreach (var part in parts)
        {
            if (part == "/{0}" || string.IsNullOrEmpty(part)) continue;
            parameters.Add(part);
        }
        
        return string.Join(",", parameters);
    }
    
    return "";
}

static void Log(string s)
{
    var line = $"{DateTime.Now:HH:mm:ss.fff} {s}";
    Console.WriteLine(line);
    File.AppendAllText("fakeserver.log", line + "\n");
}

static List<EndpointHandler> _handlers = new List<EndpointHandler>();
