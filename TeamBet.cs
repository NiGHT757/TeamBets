using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace TeamBet;

public class TeamBet : BasePlugin
{
    public override string ModuleName => "TeamBet";

    public override string ModuleVersion => "0.0.2";
    
    public override string ModuleAuthor => "NiGHT";
    
    public static string Directory = string.Empty;
    
    public bool HasTeamPlayersAlive(int team)
    {
        var players = Utilities.GetPlayers();
        foreach (var player in players)
        {
            if (player.TeamNum != team)
                continue;
            
            if(player.PawnIsAlive)
                return true;
        }
        return false;
    }
    
    private class PlayerInfo
    {
        public int Team { get; set; }
        public int Amount { get; set; }
    }
    private Dictionary<uint, PlayerInfo> g_PlayerInfo = new Dictionary<uint, PlayerInfo>();
    
    private bool g_IsBetRestricted = false;
    
    public override void Load(bool hotReload)
    {
        Directory = ModuleDirectory;
        new CFG().CheckConfig(ModuleDirectory);
        
        RegisterListener<Listeners.OnClientDisconnect>(playerSlot =>
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (player == null || player.IsBot)
                return;

            uint index = player.EntityIndex!.Value.Value;
            
            if (g_PlayerInfo.ContainsKey(index))
                g_PlayerInfo.Remove(index);
        });
        
        RegisterEventHandler<EventRoundEnd>((@event, info) =>
        {
            g_IsBetRestricted = true;
            ConVar cvar = ConVar.Find("mp_maxmoney");
            int value = cvar.GetPrimitiveValue<Int32>();
            
            var players = Utilities.GetPlayers().Where(x => !x.IsBot);
            foreach (var player in players)
            {
                uint index = player.EntityIndex!.Value.Value;
                if (!g_PlayerInfo.ContainsKey(index))
                    continue;
                
                if (g_PlayerInfo[index].Team == 0 || g_PlayerInfo[index].Amount == 0 || player.TeamNum <= 1)
                    continue;

                if (g_PlayerInfo[index].Team == @event.Winner)
                {
                    int amount = player.InGameMoneyServices.Account + (g_PlayerInfo[index].Amount * 2) > value ? value : player.InGameMoneyServices.Account + (g_PlayerInfo[index].Amount * 2);
                    
                    player.InGameMoneyServices.Account = amount;
                    player.PrintToChat($"[{ChatColors.Red}TeamBet{ChatColors.Default}] You won {ChatColors.Green}${amount}");
                }
                else
                    player.PrintToChat($"[{ChatColors.Red}TeamBet{ChatColors.Default}] You lost {ChatColors.Red}${g_PlayerInfo[index].Amount}");

                g_PlayerInfo.Remove(index);
            }
            
            return HookResult.Continue;
        });
        
        RegisterEventHandler<EventRoundStart>((@event, info) =>
        {
            g_IsBetRestricted = false;

            var players = Utilities.GetPlayers().Where(x => !x.IsBot);

            foreach (var player in players)
            {
                uint index = player.EntityIndex!.Value.Value;
                if (g_PlayerInfo.ContainsKey(index))
                    g_PlayerInfo.Remove(index);
            }
            
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerChat>((@event, info) =>
        {
            var player = Utilities.GetPlayerFromUserid(@event.Userid);
            if (player == null || !player.IsValid)
                return HookResult.Continue;
            
            string text = @event.Text;
            if (text.StartsWith("!") || text.StartsWith("/") || text.StartsWith("@") || text == "")
                return HookResult.Continue;

            string[] args = text.Split(' ');
            
            // ******* FIRST ARG ******** //
            string firstArg = args.ElementAtOrDefault(0);
            if (firstArg == null)
                return HookResult.Continue;
            
            if (firstArg == "bet")
            {
                uint index = player.EntityIndex!.Value.Value;
                string[] teams = {"None", "Spec", $"{ChatColors.Red}T{ChatColors.Default}", $"{ChatColors.Blue}CT{ChatColors.Default}"};

                if(g_PlayerInfo.ContainsKey(index))
                {
                    player.PrintToChat($" {ChatColors.Red}ERROR:{ChatColors.Default} You already bet, your bet: {teams[g_PlayerInfo[index].Team]} {ChatColors.Green}${g_PlayerInfo[index].Amount}");
                    return HookResult.Continue;
                }

                if (g_IsBetRestricted)
                {
                    player.PrintToChat($" {ChatColors.Red}ERROR:{ChatColors.Default} You can't bet right now.");
                    return HookResult.Continue;
                }
                
                if (player.PawnIsAlive)
                {
                    player.PrintToChat($" {ChatColors.Red}ERROR:{ChatColors.Default} You can't bet while you're alive.");
                    return HookResult.Continue;
                }
                
                if (player.TeamNum <= 1)
                {
                    player.PrintToChat($" {ChatColors.Red}ERROR:{ChatColors.Default} You can't bet from this team.");
                    return HookResult.Continue;
                }

                if (!HasTeamPlayersAlive(2) || !HasTeamPlayersAlive(3))
                {
                    player.PrintToChat($" {ChatColors.Red}ERROR:{ChatColors.Default} You can't bet while all players are death.");
                    return HookResult.Continue;
                }
                
                // ******** SECOND ARG ******** //
                string secondArg = args.ElementAtOrDefault(1);
                switch (secondArg)
                {
                    case "t":
                        break;
                    case "ct":
                        break;
                    default:
                        player.PrintToChat($" {ChatColors.Red}ERROR:{ChatColors.Default} Invalid team, use {ChatColors.Red}t{ChatColors.Default} or {ChatColors.Blue}ct{ChatColors.White}.");
                        return HookResult.Continue;
                }
                
                // ******** THIRD ARG ******** //
                string thirdArg = args.ElementAtOrDefault(2);
                
                ConVar cvar = ConVar.Find("mp_maxmoney");
                int value = cvar.GetPrimitiveValue<Int32>(), playerMoney = player.InGameMoneyServices.Account;
                
                if (playerMoney == value)
                {
                    player.PrintToChat($" {ChatColors.Red}ERROR:{ChatColors.Default} You can't bet, you already have the maximum amount of money.");
                    return HookResult.Continue;
                }

                if (playerMoney <= 0)
                {
                    player.PrintToChat($" {ChatColors.Red}ERROR:{ChatColors.Default} You don't have enough money to bet.");
                    return HookResult.Continue;
                }
                
                int betAmount = 0;
                if (thirdArg == "all")
                    betAmount = playerMoney;
                else
                {
                    if (!int.TryParse(thirdArg, out int amount))
                    {
                        player.PrintToChat($" {ChatColors.Red}ERROR:{ChatColors.Default} Invalid amount.");
                        return HookResult.Continue;
                    }

                    betAmount = amount;
                }

                if (betAmount < CFG.config.MinBetAmount || betAmount > CFG.config.MaxBetAmount)
                {
                    player.PrintToChat($" {ChatColors.Red}ERROR:{ChatColors.Default} Invalid amount, minimum to bet: {ChatColors.Green}{CFG.config.MinBetAmount}{ChatColors.Default}, maximum: {ChatColors.Red}{CFG.config.MaxBetAmount}{ChatColors.Default}.");
                    return HookResult.Continue;
                }
                
                if (playerMoney < betAmount)
                {
                    player.PrintToChat($" {ChatColors.Red}ERROR:{ChatColors.Default} You don't have enough money to bet that amount.");
                    return HookResult.Continue;
                }
                
                g_PlayerInfo.Add(index, new PlayerInfo
                {
                    Team = secondArg == "t" ? 2 : 3,
                    Amount = betAmount
                });
                
                player.InGameMoneyServices.Account -= betAmount;
                player.PrintToChat($"[{ChatColors.Red}TeamBet{ChatColors.Default}] You bet {ChatColors.Green}${betAmount}{ChatColors.Default} on {teams[g_PlayerInfo[index].Team]} team.");
                return HookResult.Continue;
            }

            return HookResult.Continue;
        });
    }
}