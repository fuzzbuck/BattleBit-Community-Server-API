using System.Diagnostics;
using System.Numerics;
using BattleBitAPI;
using BattleBitAPI.Common;
using BattleBitAPI.Server;

namespace CommunityServerAPI;

class Program
{
    static void Main(string[] args)
    {
        var port = 29294;
        var listener = new ServerListener<_Player, _GameServer>();
        listener.Start(port);
        
        Console.WriteLine("listening on port " + port);

        Thread.Sleep(-1);
    }
}

class _GameServer : GameServer<_Player>
{
    // get player count on each team
    public int GetTeamCount(Team team)
    {
        var count = 0;
        foreach (var item in AllPlayers)
        {
            if (item.IsAlive && item.Team == team)
            {
                count++;
            }
        }
        return count;
    }
    
    // find the team with the lowest amount of players
    public Team FindLowestTeam()
    {
        var a = 0;
        var b = 0;
        foreach (var item in AllPlayers)
        {
            if (item.Team == Team.TeamA)
            {
                a++;
            }
            else if (item.Team == Team.TeamB)
            {
                b++;
            }
        }
        
        if (a < b)
            return Team.TeamA;
        
        return Team.TeamB;
    }
    public override async Task OnTick()
    {
        RoundSettings.MaxTickets = MaxPlayers / 2;
        
        if (RoundSettings.State == GameState.WaitingForPlayers)
            ForceStartGame();

        RoundSettings.TeamATickets = GetTeamCount(Team.TeamA);
        RoundSettings.TeamBTickets = GetTeamCount(Team.TeamA);
        
        await Task.Delay(1000);
    }

    public override async Task<PlayerStats> OnGetPlayerStats(ulong steamID, PlayerStats officialStats)
    {
        return new PlayerStats
        {
            Progress = new PlayerStats.PlayerProgess
            {
                EXP = UInt32.MaxValue - 1000000
            }
        };
    }

    public override async Task OnPlayerConnected(_Player player)
    {
        player.Team = FindLowestTeam();
    }

    public override async Task OnPlayerSpawned(_Player player)
    {
        player.Message("Welcome to THE CLONE. Each player you kill respawns as you, on your team. The last remaining team wins.");
    }

    public override async Task OnAPlayerKilledAnotherPlayer(OnPlayerKillArguments<_Player> args)
    {
        args.Victim.Team = args.Killer.Team;
        args.Victim.SpawnPlayer(
            args.Killer.CurrentLoadout,
            args.Killer.CurrentWearings,
            args.VictimPosition, 
            Vector3.Zero,
            PlayerStand.Proning,
            0
            );
        args.Victim.Message("You have been cloned by " + args.Killer.Name + ".");
    }
    
}

class _Player : Player<_Player>
{
    
}