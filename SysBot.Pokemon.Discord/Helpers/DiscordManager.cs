using System;
using System.Collections.Generic;
using System.Linq;

namespace SysBot.Pokemon.Discord;

public class DiscordManager(DiscordSettings Config)
{
    public readonly DiscordSettings Config = Config;

    public RemoteControlAccessList BlacklistedServers => Config.ServerBlacklist;

    public RemoteControlAccessList BlacklistedUsers => Config.UserBlacklist;

    public RemoteControlAccessList FavoredRoles => Config.RoleFavored;

    public ulong Owner { get; internal set; }

    public RemoteControlAccessList RolesClone => Config.RoleCanClone;

    public RemoteControlAccessList RolesDump => Config.RoleCanDump;

    public RemoteControlAccessList RolesFixOT => Config.RoleCanFixOT;

    public RemoteControlAccessList RolesRemoteControl => Config.RoleRemoteControl;

    public RemoteControlAccessList RolesSeed => Config.RoleCanSeedCheckorSpecialRequest;

    public RemoteControlAccessList RolesTrade => Config.RoleCanTrade;

    public RemoteControlAccessList RolesEgg => Config.RoleCanEgg;

    public RemoteControlAccessList RolesBatchTrade => Config.RoleCanBatchTrade;

    public RemoteControlAccessList RolesMysteryEgg => Config.RoleCanMysteryEgg;

    public RemoteControlAccessList RolesMysteryMon => Config.RoleCanMysteryMon;

    public RemoteControlAccessList RolesTextView => Config.RoleCanTextView;

    public RemoteControlAccessList RolesEventRequest => Config.RoleCanEventRequest;

    public RemoteControlAccessList RolesBattleReadyList => Config.RoleCanBattleReadyList;

    public RemoteControlAccessList RolesBattleReadyRequest => Config.RoleCanBattleReadyRequest;

    public RemoteControlAccessList RolesHomeReadyRequest => Config.RoleCanHomeReadyRequest;

    public RemoteControlAccessList RolesBatchTradeZip => Config.RoleCanBatchTradeZip;

    public RemoteControlAccessList RolesBatchInfo => Config.RoleCanBatchInfo;

    public RemoteControlAccessList RolesBatchValidate => Config.RoleCanBatchValidate;

    public RemoteControlAccessList RolesAutoOT => Config.RoleCanAutoOT;

    public RemoteControlAccessList RolesUseBatchCommands => Config.RoleCanUseBatchCommands;

    public RemoteControlAccessList RolesOverrideTrainerData => Config.RoleCanOverrideTrainerData;

    public RemoteControlAccessList SudoDiscord => Config.GlobalSudoList;

    public RemoteControlAccessList SudoRoles => Config.RoleSudo;

    public RemoteControlAccessList WhitelistedChannels => Config.ChannelWhitelist;

    public bool CanUseCommandChannel(ulong channel) => (WhitelistedChannels.List.Count == 0 && WhitelistedChannels.AllowIfEmpty) || WhitelistedChannels.Contains(channel);

    public bool CanUseCommandUser(ulong uid) => !BlacklistedUsers.Contains(uid);

    public bool CanUseSudo(ulong uid) => SudoDiscord.Contains(uid);

    public bool CanUseSudo(IEnumerable<string> roles) => roles.Any(SudoRoles.Contains);

    public bool GetHasRoleAccess(string type, IEnumerable<string> roles)
    {
        var set = GetSet(type);
        return set is { AllowIfEmpty: true, List.Count: 0 } || roles.Any(set.Contains);
    }

    public RequestSignificance GetSignificance(IEnumerable<string> roles)
    {
        var result = RequestSignificance.None;
        foreach (var r in roles)
        {
            if (SudoRoles.Contains(r))
                result = RequestSignificance.Favored;
            if (FavoredRoles.Contains(r))
                result = RequestSignificance.Favored;
        }
        return result;
    }

    private RemoteControlAccessList GetSet(string type) => type switch
    {
        nameof(RolesClone) => RolesClone,
        nameof(RolesTrade) => RolesTrade,
        nameof(RolesSeed) => RolesSeed,
        nameof(RolesDump) => RolesDump,
        nameof(RolesFixOT) => RolesFixOT,
        nameof(RolesRemoteControl) => RolesRemoteControl,
        nameof(RolesEgg) => RolesEgg,
        nameof(RolesBatchTrade) => RolesBatchTrade,
        nameof(RolesMysteryEgg) => RolesMysteryEgg,
        nameof(RolesMysteryMon) => RolesMysteryMon,
        nameof(RolesTextView) => RolesTextView,
        nameof(RolesEventRequest) => RolesEventRequest,
        nameof(RolesBattleReadyList) => RolesBattleReadyList,
        nameof(RolesBattleReadyRequest) => RolesBattleReadyRequest,
        nameof(RolesHomeReadyRequest) => RolesHomeReadyRequest,
        nameof(RolesBatchTradeZip) => RolesBatchTradeZip,
        nameof(RolesBatchInfo) => RolesBatchInfo,
        nameof(RolesBatchValidate) => RolesBatchValidate,
        nameof(RolesAutoOT) => RolesAutoOT,
        nameof(RolesUseBatchCommands) => RolesUseBatchCommands,
        nameof(RolesOverrideTrainerData) => RolesOverrideTrainerData,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
