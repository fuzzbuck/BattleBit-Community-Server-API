using System.Diagnostics;
using System.Numerics;
using BattleBitAPI;
using BattleBitAPI.Common;
using BattleBitAPI.Server;
using DiscordWebhook;

namespace CommunityServerAPI;

static class UTILS
{
    public static Webhook? webhook;
    
    // change this to 'false' in production to not make everyone admin
    public static bool DEBUG = true;
    
    public static void SendWebhook(String msg)
    {
        webhook?.PostData(new WebhookObject
        {
            username = "battlebit",
            content = msg
        });
    }
}
class Program
{
    static void Main(string[] args)
    {
        var port = 29294;
        var listener = new ServerListener<_Player, _GameServer>();
        listener.Start(port);
        
        listener.OnGameServerConnected += OnGameServerConnected;
        
        Console.WriteLine("listening on port " + port);

        if (args.Length > 0)
        {
            UTILS.webhook = new Webhook(args[1]);
            Console.WriteLine("webhook enabled");
        }
        else
        {
            Console.WriteLine("no discord webhook is provided!");
        }

        Thread.Sleep(-1);
    }

    private static async Task OnGameServerConnected(GameServer<_Player> arg)
    {
        Console.WriteLine("New game server connected from " + arg.GameIP + " on map " + arg.Map + " with " + arg.MaxPlayers + " slots.");
        Console.WriteLine("Gamemode: " + arg.Gamemode);
        Console.WriteLine("Map size: " + arg.MapSize);
        Console.WriteLine("Server name: " + arg.ServerName);
    }
}

class _GameServer : GameServer<_Player>
{
    public static String INSTRUCTIONS =
        "Welcome to THE CLONE. Each player you kill respawns as you, on your team. The last remaining team wins.";
    
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
    
    // find a player by an identifier
    public _Player? FindPlayer(String identifier)
    {
        _Player? player = null;
        foreach (var item in AllPlayers)
        {
            if (item.Name.Contains(identifier))
            {
                player = item;
                break;
            }
        }

        return player;
    }
    public override async Task OnTick()
    {
        RoundSettings.MaxTickets = MaxPlayers / 2;

        var players_needed = 2;

        if (RoundSettings.State == GameState.WaitingForPlayers)
        {
            if (AllPlayers.Count() >= players_needed)
            {
                AnnounceLong(INSTRUCTIONS);
                ForceStartGame();
   
                UTILS.SendWebhook("New game started on map " + Map + " with " + AllPlayers.Count() + " players.");
            }
        }

        RoundSettings.TeamATickets = GetTeamCount(Team.TeamA);
        RoundSettings.TeamBTickets = GetTeamCount(Team.TeamA);
        
        await Task.Delay(1000);
    }

    public override async Task OnGameStateChanged(GameState oldState, GameState newState)
    {
        if (newState == GameState.EndingGame)
        {
            UTILS.SendWebhook("Game ended on map " + Map + " with " + AllPlayers.Count() + " players.");
        }
    }

    public override async Task<PlayerStats> OnGetPlayerStats(ulong steamID, PlayerStats officialStats)
    {
        return new PlayerStats
        {
            Progress = new PlayerStats.PlayerProgess
            {
                // maximum EXP, except subtract a little bit to not cause overflow
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
        player.Message(INSTRUCTIONS);
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

    public override async Task<bool> OnPlayerTypedMessage(_Player player, ChatChannel channel, string msg)
    {
        if (msg.StartsWith("/") && msg.Length > 1)
        {
            // check if player is admin
            if (!player.IsAdmin())
            {
                WarnPlayer(player, "You are not an admin.");
                return false;
            }
            
            // parse command
            var split = msg.Substring(1).Split(' ');
            var cmd = split[0].ToLower();
            var args = split.Skip(1).ToArray();


            if (cmd == "speed")
            {
                // parse args[0] as float
                var to = float.Parse(args[0]);

                player.SetRunningSpeedMultiplier(to);
                player.Message("new running speed multiplier: " + to);
            }

            if (cmd == "dmgtaken")
            {
                var to = float.Parse(args[0]);

                player.SetReceiveDamageMultiplier(to);
                player.Message("new damage taken multiplier: " + to);
            }

            if (cmd == "jump")
            {
                var to = float.Parse(args[0]);

                player.SetJumpMultiplier(to);
                player.Message("new jump height multiplier: " + to);
            }

            if (cmd == "warn" && args.Length >= 2)
            {
                if (args[0] == "all")
                {
                    foreach (var item in AllPlayers)
                    {
                        WarnPlayer(item, String.Join(" ", args.Skip(1).ToArray()));
                    }
                }
                else
                {
                    var to = FindPlayer(args[0]);
                    
                    if (to != null)
                        WarnPlayer(to, String.Join(" ", args.Skip(1).ToArray()));
                }
            }

            if (cmd == "announce" && args.Length >= 1)
            {
                AnnounceLong(String.Join(" ", args.ToArray()));
            }

            return false;
        }

        return true;
    }
}

class _Player : Player<_Player>
{
    private ulong[] admins = new ulong[]
    {
        76561198046149851, // xander
        76561198119895858, // fuzzbuck
        76561198842799146 // boltza
    };

    public bool IsAdmin()
    {
        return UTILS.DEBUG || admins.Contains(SteamID);
    }
}