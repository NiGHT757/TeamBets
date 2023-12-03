using System.Reflection;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace TeamBet;

public class TeamBetConfig : BasePluginConfig
{
    [JsonPropertyName("ChatPrefix")] public string? ChatPrefix { get; set; } = "[{LightRed}TeamBet{Default}]";
    [JsonPropertyName("MinBetAmount")] public int MinBetAmount { get; set; } = 1;
    [JsonPropertyName("MaxBetAmount")] public int MaxBetAmount { get; set; } = 16000;
    [JsonPropertyName("YouWonMessage")] public string? YouWonMessage { get; set; } = "{chat-prefix} You won {green}${amount}";
    [JsonPropertyName("YouLostMessage")] public string? YouLostMessage { get; set; } = "{chat-prefix} You lost {red}${amount}";
    [JsonPropertyName("UsageCommandMessage")] public string? UsageCommandMessage { get; set; } = "{red}ERROR:{default} Usage: bet <team> <amount>";
    [JsonPropertyName("AlreadyBetMessage")] public string? AlreadyBetMessage { get; set; } = "{red}ERROR:{default} You already bet, your bet: {team} {Green}${amount}";
    [JsonPropertyName("RestrictedBetMessage")] public string? RestrictedBetMessage { get; set; } = "{red}ERROR:{default} You can't bet right now.";
    [JsonPropertyName("BetAliveMessage")] public string? BetAliveMessage { get; set; } = "{red}ERROR:{default} You can't bet while you're alive.";
    [JsonPropertyName("RestrictedTeamMessage")] public string? RestrictedTeamMessage { get; set; } = "{red}ERROR:{default} You can't bet from this team.";
    [JsonPropertyName("BetAllPlayersDeathMessage")] public string? BetAllPlayersDeathMessage { get; set; } = "{red}ERROR:{default} You can't bet while all players are death";
    [JsonPropertyName("InvalidTeamMessage")] public string? InvalidTeamMessage { get; set; } = "{red}ERROR:{default} Invalid team, use {red}t{default} or {blue}ct{default}.";
    [JsonPropertyName("MaxMoneyMessage")] public string? MaxMoneyMessage { get; set; } = "{red}ERROR:{default} You can't bet, you already have the maximum amount of money.";
    [JsonPropertyName("InvalidAmountMessage")] public string? InvalidAmountMessage { get; set; } = "{red}ERROR:{default} Invalid amount.";
    [JsonPropertyName("InvalidAmountMinMaxMessage")] public string? InvalidAmountMinMaxMessage { get; set; } = "{red}ERROR:{default} Invalid amount, minimum to bet: {green}{MinBetAmount}{default}, maximum: {red}{MaxBetAmount}{default}.";
    [JsonPropertyName("NotEnoughMoneyMessage")] public string? NotEnoughMoneyMessage { get; set; } = "{red}ERROR:{default} You need {red}{money}{default} more to bet {red}{amount}{default}.";
    [JsonPropertyName("YouBetMessage")] public string? YouBetMessage { get; set; } = "{chat-prefix} You bet {green}${amount}{default} on {team} team.";
}

public class TeamBet : BasePlugin, IPluginConfig<TeamBetConfig>
{
    public override string ModuleName => "TeamBet";
    public override string ModuleVersion => "0.0.3";
    public override string ModuleAuthor => "NiGHT";
    
    public TeamBetConfig Config { get; set; } = null!;
    public void OnConfigParsed(TeamBetConfig config)
    {
        Config = config;
        
        if (Config.ChatPrefix != null) Config.ChatPrefix = ModifyColorValue(Config.ChatPrefix);
        if (Config.YouWonMessage != null) Config.YouWonMessage = ModifyColorValue(Config.YouWonMessage);
        if (Config.YouLostMessage != null) Config.YouLostMessage = ModifyColorValue(Config.YouLostMessage);
        if (Config.UsageCommandMessage != null) Config.UsageCommandMessage = ModifyColorValue(Config.UsageCommandMessage);
        if (Config.AlreadyBetMessage != null) Config.AlreadyBetMessage = ModifyColorValue(Config.AlreadyBetMessage);
        if (Config.RestrictedBetMessage != null) Config.RestrictedBetMessage = ModifyColorValue(Config.RestrictedBetMessage);
        if (Config.BetAliveMessage != null) Config.BetAliveMessage = ModifyColorValue(Config.BetAliveMessage);
        if (Config.RestrictedTeamMessage != null) Config.RestrictedTeamMessage = ModifyColorValue(Config.RestrictedTeamMessage);
        if (Config.BetAllPlayersDeathMessage != null) Config.BetAllPlayersDeathMessage = ModifyColorValue(Config.BetAllPlayersDeathMessage);
        if (Config.InvalidTeamMessage != null) Config.InvalidTeamMessage = ModifyColorValue(Config.InvalidTeamMessage);
        if (Config.MaxMoneyMessage != null) Config.MaxMoneyMessage = ModifyColorValue(Config.MaxMoneyMessage);
        if (Config.InvalidAmountMessage != null) Config.InvalidAmountMessage = ModifyColorValue(Config.InvalidAmountMessage);
        if (Config.InvalidAmountMinMaxMessage != null) Config.InvalidAmountMinMaxMessage = ModifyColorValue(Config.InvalidAmountMinMaxMessage);
        if (Config.NotEnoughMoneyMessage != null) Config.NotEnoughMoneyMessage = ModifyColorValue(Config.NotEnoughMoneyMessage);
        if (Config.YouBetMessage != null) Config.YouBetMessage = ModifyColorValue(Config.YouBetMessage);
    }
    
    private static bool HasTeamPlayersAlive(int team)
    {
        var players = Utilities.GetPlayers();
        return players.Where(player => player.TeamNum == team).Any(player => player.PawnIsAlive);
    }
    
    private class PlayerInfo
    {
        public int Team { get; set; }
        public int Amount { get; set; }
    }
    
    private Dictionary<uint, PlayerInfo> _gPlayerInfo = new Dictionary<uint, PlayerInfo>();
    private bool _gIsBetRestricted = false;
    
    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnClientDisconnect>(playerSlot =>
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (player == null || !player.IsValid || player.IsBot)
                return;

            var index = player.Index;
            
            if (_gPlayerInfo.ContainsKey(index))
                _gPlayerInfo.Remove(index);
        });
        
        RegisterEventHandler<EventRoundEnd>((@event, info) =>
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
                
                if (!player.IsValid || _gPlayerInfo[index].Team == 0 || _gPlayerInfo[index].Amount == 0 || player.TeamNum <= 1)
                    continue;
                
                if (_gPlayerInfo[index].Team == @event.Winner)
                {
                    if(player.InGameMoneyServices == null)
                        continue;
                    
                    var amount = player.InGameMoneyServices.Account + (_gPlayerInfo[index].Amount * 2) > value ? value : player.InGameMoneyServices.Account + (_gPlayerInfo[index].Amount * 2);
                    
                    player.InGameMoneyServices.Account = amount;
                    player.PrintToChat($"{Config.YouWonMessage?.Replace("{chat-prefix}", Config.ChatPrefix).Replace("{amount}", $"{amount}")}");
                }
                else
                    player.PrintToChat($"{Config.YouLostMessage?.Replace("{chat-prefix}", Config.ChatPrefix).Replace("{amount}", $"{_gPlayerInfo[index].Amount}")}");
            }
            
            _gPlayerInfo.Clear();
            return HookResult.Continue;
        });
        
        RegisterEventHandler<EventRoundStart>((@event, info) =>
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
            player.PrintToChat($"{Config.UsageCommandMessage?.Replace("{chat-prefix}", Config.ChatPrefix)}");
            return;
        }

        var index = player.Index;
        string[] teams = {"None", "Spec", $"{ChatColors.Red}T{ChatColors.Default}", $"{ChatColors.Blue}CT{ChatColors.Default}"};

        if(_gPlayerInfo.TryGetValue(index, out var pInfo))
        {
            player.PrintToChat($"{Config.AlreadyBetMessage?.Replace("{chat-prefix}", Config.ChatPrefix).Replace("{team}", teams[pInfo.Team]).Replace("{amount}", $"{pInfo.Amount}")}");
            return;
        }

        if (_gIsBetRestricted)
        {
            player.PrintToChat($"{Config.RestrictedBetMessage?.Replace("{chat-prefix}", Config.ChatPrefix)}");
            return;
        }
        
        if (player.PawnIsAlive)
        {
            player.PrintToChat($"{Config.BetAliveMessage?.Replace("{chat-prefix}", Config.ChatPrefix)}");
            return;
        }
        
        if (player.TeamNum <= 1)
        {
            player.PrintToChat($"{Config.RestrictedTeamMessage?.Replace("{chat-prefix}", Config.ChatPrefix)}");
            return;
        }

        if (!HasTeamPlayersAlive(2) || !HasTeamPlayersAlive(3))
        {
            player.PrintToChat($"{Config.BetAllPlayersDeathMessage?.Replace("{chat-prefix}", Config.ChatPrefix)}");
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
                player.PrintToChat($"{Config.InvalidTeamMessage?.Replace("{chat-prefix}", Config.ChatPrefix)}");
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
            player.PrintToChat($"{Config.MaxMoneyMessage?.Replace("{chat-prefix}", Config.ChatPrefix)}");
            return;
        }
        
        int betAmount;
        if (thirdArg == "all")
            betAmount = playerMoney;
        else
        {
            if (!int.TryParse(thirdArg, out var amount))
            {
                player.PrintToChat($"{Config.InvalidAmountMessage?.Replace("{chat-prefix}", Config.ChatPrefix)}");
                return;
            }

            betAmount = amount;
        }

        if (betAmount < Config.MinBetAmount || betAmount > Config.MaxBetAmount)
        {
            player.PrintToChat($"{Config.InvalidAmountMinMaxMessage?.Replace("{chat-prefix}", Config.ChatPrefix).Replace("{MinBetAmount}", $"{Config.MinBetAmount}").Replace("{MaxBetAmount}", $"{Config.MaxBetAmount}")}");
            return;
        }
        
        if (playerMoney < betAmount)
        {
            player.PrintToChat($"{Config.NotEnoughMoneyMessage?.Replace("{chat-prefix}", Config.ChatPrefix).Replace("{money}", $"{betAmount-playerMoney}").Replace("{amount}", $"{betAmount}")}");
            return;
        }
        
        _gPlayerInfo.Add(index, new PlayerInfo
        {
            Team = secondArg == "t" ? 2 : 3,
            Amount = betAmount
        });
        
        player.InGameMoneyServices.Account -= betAmount;
        player.PrintToChat($"{Config.YouBetMessage?.Replace("{chat-prefix}", Config.ChatPrefix).Replace("{team}", teams[_gPlayerInfo[index].Team]).Replace("{amount}", $"{betAmount}")}");
    }
    
    private string ModifyColorValue(string msg)
    {
        if (!msg.Contains('{')) return string.IsNullOrEmpty(msg) ? "[TeamBet]" : msg;
        var modifiedValue = msg;
        foreach (var field in typeof(ChatColors).GetFields())
        {
            var pattern = $"{{{field.Name}}}";
            if (msg.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null).ToString(), StringComparison.OrdinalIgnoreCase);
            }
            if (msg.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                modifiedValue = $" {modifiedValue}";
        }
        return modifiedValue;
    }
}