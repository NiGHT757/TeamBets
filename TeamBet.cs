using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace TeamBet;

public class TeamBetConfig : BasePluginConfig
{
    [JsonPropertyName("MinBetAmount")] public int MinBetAmount { get; set; } = 1;
    [JsonPropertyName("MaxBetAmount")] public int MaxBetAmount { get; set; } = 16000;
}

[MinimumApiVersion(100)]
public class TeamBet : BasePlugin, IPluginConfig<TeamBetConfig>
{
    public override string ModuleName => "TeamBet";
    public override string ModuleVersion => "0.0.5";
    public override string ModuleAuthor => "NiGHT";
    
    public required TeamBetConfig Config { get; set; }
    public void OnConfigParsed(TeamBetConfig config)
    {
        Config = config;
    }
    
    private static bool HasTeamPlayersAlive(int team)
    {
        return Utilities.GetPlayers().Where(player => player.TeamNum == team).Any(player => player.PawnIsAlive);
    }
    
    private class PlayerInfo
    {
        public int Team { get; set; }
        public int Amount { get; set; }
    }
    
    private readonly Dictionary<uint, PlayerInfo> _gPlayerInfo = new();
    private bool _gIsBetRestricted;
    
    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnClientDisconnect>(playerSlot =>
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (player == null || !player.IsValid || player.IsBot)
                return;

            var index = player.Index;

            _gPlayerInfo.Remove(index);
        });
        
        RegisterEventHandler<EventRoundEnd>((@event, _) =>
        {
            _gIsBetRestricted = true;
            var cvar = ConVar.Find("mp_maxmoney");
            if(cvar == null)
                return HookResult.Continue;
            
            var value = cvar.GetPrimitiveValue<int>();
            
            var players = _gPlayerInfo.ToList();
            
            foreach (var list in players)
            {
                var index = list.Key;
                var player = Utilities.GetPlayerFromIndex((int)index);
                
                if (player == null || !player.IsValid || _gPlayerInfo[index].Team == 0 || _gPlayerInfo[index].Amount == 0 || player.TeamNum <= 1)
                    continue;
                
                if (_gPlayerInfo[index].Team == @event.Winner)
                {
                    if(player.InGameMoneyServices == null)
                        continue;
                    
                    var amount = player.InGameMoneyServices.Account + (_gPlayerInfo[index].Amount * 2) > value ? value : player.InGameMoneyServices.Account + (_gPlayerInfo[index].Amount * 2);
                    
                    player.InGameMoneyServices.Account = amount;
                    player.PrintToChat(Localizer["YouWonMessage"].Value.Replace("{amount}", amount.ToString()));
                }
                else
                    player.PrintToChat(Localizer["YouLostMessage"].Value.Replace("{amount}", _gPlayerInfo[index].Amount.ToString()));
            }
            
            _gPlayerInfo.Clear();
            return HookResult.Continue;
        });
        
        RegisterEventHandler<EventRoundStart>((_, _) =>
        {
            _gIsBetRestricted = false;
            
            _gPlayerInfo.Clear();
            return HookResult.Continue;
        });
    }
    
    [ConsoleCommand("bet", "bet <team> <amount>")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnBetCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        if (command.ArgCount < 3)
        {
            player.PrintToChat(Localizer["UsageCommandMessage"]);
            return;
        }

        var index = player.Index;
        string[] teams = {"None", "Spec", $"{ChatColors.Red}T{ChatColors.Default}", $"{ChatColors.Blue}CT{ChatColors.Default}"};

        if(_gPlayerInfo.TryGetValue(index, out var pInfo))
        {
            player.PrintToChat(Localizer["AlreadyBetMessage"].Value.Replace("{team}", teams[pInfo.Team]).Replace("{amount}", $"{pInfo.Amount}"));
            return;
        }

        if (_gIsBetRestricted)
        {
            player.PrintToChat(Localizer["RestrictedBetMessage"]);
            return;
        }
        
        if (player.PawnIsAlive)
        {
            player.PrintToChat(Localizer["BetAliveMessage"]);
            return;
        }
        
        if (player.TeamNum <= 1)
        {
            player.PrintToChat(Localizer["RestrictedTeamMessage"]);
            return;
        }

        if (!HasTeamPlayersAlive(2) || !HasTeamPlayersAlive(3))
        {
            player.PrintToChat(Localizer["BetAllPlayersDeathMessage"]);
            return;
        }
        
        // ******** SECOND ARG ******** //
        var secondArg = command.GetArg(1);
        switch (secondArg)
        {
            case "t":
                break;
            case "ct":
                break;
            default:
                player.PrintToChat(Localizer["InvalidTeamMessage"]);
                return;
        }
        
        // ******** THIRD ARG ******** //
        var thirdArg = command.GetArg(2);
        
        var cvar = ConVar.Find("mp_maxmoney");
        if(cvar == null || player.InGameMoneyServices == null)
            return;
        
        int value = cvar.GetPrimitiveValue<int>(), playerMoney = player.InGameMoneyServices.Account;
        if (playerMoney >= value)
        {
            player.PrintToChat(Localizer["MaxMoneyMessage"]);
            return;
        }
        
        int betAmount;
        if (thirdArg == "all")
            betAmount = playerMoney;
        else
        {
            if (!int.TryParse(thirdArg, out var amount))
            {
                player.PrintToChat(Localizer["InvalidAmountMinMaxMessage"].Value.Replace("{MinBetAmount}", $"{Config.MinBetAmount}").Replace("{MaxBetAmount}", $"{Config.MaxBetAmount}"));
                return;
            }

            betAmount = amount;
        }

        if (betAmount < Config.MinBetAmount || betAmount > Config.MaxBetAmount)
        {
            player.PrintToChat(Localizer["InvalidAmountMinMaxMessage"].Value.Replace("{MinBetAmount}", $"{Config.MinBetAmount}").Replace("{MaxBetAmount}", $"{Config.MaxBetAmount}"));
            return;
        }
        
        if (playerMoney < betAmount)
        {
            player.PrintToChat(Localizer["NotEnoughMoneyMessage"].Value.Replace("{money}", $"{betAmount-playerMoney}").Replace("{amount}", $"{betAmount}"));
            return;
        }
        
        _gPlayerInfo.Add(index, new PlayerInfo
        {
            Team = secondArg == "t" ? 2 : 3,
            Amount = betAmount
        });
        
        player.InGameMoneyServices.Account -= betAmount;
        player.PrintToChat(Localizer["YouBetMessage"].Value.Replace("{team}", teams[_gPlayerInfo[index].Team]).Replace("{amount}", $"{betAmount}"));
    }
}