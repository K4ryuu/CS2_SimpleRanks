using Newtonsoft.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Attributes;

namespace K4ryuuSimpleRanks
{
	public class Rank
	{
		public int Exp { get; set; }
		public string Color { get; set; } = "Default";
	}

	[MinimumApiVersion(5)]
	public class SimpleRanks : BasePlugin
	{
		private Dictionary<int, (int killStreak, DateTime lastKillTime)> playerKillStreaks = new Dictionary<int, (int, DateTime)>();
		public static Dictionary<string, Rank> ranks = new Dictionary<string, Rank>();
		public static string Directory = string.Empty;
		public override string ModuleName => "Simple Ranks";
		public override string ModuleVersion => "v1.3.2";
		public override string ModuleAuthor => "K4ryuu";

		public override void Load(bool hotReload)
		{
			Directory = ModuleDirectory;

			for (int i = 0; i <= Server.MaxPlayers; i++)
			{
				playerKillStreaks[i] = (0, DateTime.MinValue);

				if (hotReload)
				{
					CCSPlayerController targetController = Utilities.GetPlayerFromIndex(i);

					if (targetController.IsValidPlayer())
						_ = Queries.InsertUserAsync(targetController);
				}
			}

			new CFG().CheckConfig(ModuleDirectory);

			LoadRanksFromConfig();
			SetupGameEvents();

			_ = Queries.CreateTable();

			Log($"{ModuleName} [{ModuleVersion}] by {ModuleAuthor} has been loaded.");
		}

		private void LoadRanksFromConfig()
		{
			string ranksFilePath = Path.Join(ModuleDirectory, "ranks.jsonc");

			try
			{
				if (File.Exists(ranksFilePath))
				{
					using (FileStream fs = new FileStream(ranksFilePath, FileMode.Open, FileAccess.Read))
					using (StreamReader sr = new StreamReader(fs))
					{
						string jsonContent = Regex.Replace(sr.ReadToEnd(), @"/\*(.*?)\*/|//(.*)", string.Empty, RegexOptions.Multiline);
						ranks = JsonConvert.DeserializeObject<Dictionary<string, Rank>>(jsonContent)!;
					}
				}
				else
					Log("Ranks configuration file not found. Ranks will not be loaded.");
			}
			catch (Exception ex)
			{
				Log("An error occurred: " + ex.Message);
			}
		}
		private void SetupGameEvents()
		{
			/*TODO: This is work in progress. Need to find a way to block original msg and it works tho.

			RegisterEventHandler<EventPlayerChat>((@event, info) =>
			{
				CCSPlayerController playerController = new CCSPlayerController(NativeAPI.GetEntityFromIndex(@event.Userid));

				var (colorCode, rankName) = Queries.GetRankInfo(playerController, ranks);

				if (rankName == "None")
				{
					return HookResult.Continue;
				}

				string text = @event.Text;

				if (@event.Teamonly)
				{
					CCSPlayerController targetController;

					for (int targetIndex = 0; targetIndex <= Server.MaxPlayers; targetIndex++)
					{
						targetController = new CCSPlayerController(NativeAPI.GetEntityFromIndex(targetIndex));

						if (targetController.TeamNum == playerController.TeamNum)
						{
							targetController.PrintToChat($" {colorCode}[{rankName}] {playerController.PlayerName}: {text}");
						}
					}
				}
				else
					Server.PrintToChatAll($" {colorCode}[{rankName}] {playerController.PlayerName}: {ChatColors.Default}{text}");

				return HookResult.Stop;
			}, HookMode.Pre);*/
			RegisterEventHandler<EventHostageRescued>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValidPlayer() && CFG.config.HostageRescuePoints > 0 && IsPointsAllowed())
				{
					_ = Queries.AddPointsAsync(playerController, CFG.config.HostageRescuePoints);
					playerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.HostageRescuePoints} XP for rescuing a hostage.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageKilled>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValidPlayer() && CFG.config.HostageKillPoints > 0 && IsPointsAllowed())
				{
					_ = Queries.RemovePointsAsync(playerController, CFG.config.HostageKillPoints);
					playerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.HostageKillPoints} XP for killing the hostage.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageHurt>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValidPlayer() && CFG.config.HostageHurtPoints > 0 && IsPointsAllowed())
				{
					_ = Queries.RemovePointsAsync(playerController, CFG.config.HostageHurtPoints);
					playerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.HostageHurtPoints} XP for hurting the hostage.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDropped>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValidPlayer() && CFG.config.BombDropPoints > 0 && IsPointsAllowed())
				{
					_ = Queries.RemovePointsAsync(playerController, CFG.config.BombDropPoints);

					Server.NextFrame(() =>
					{
						playerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.BombDropPoints} XP for dropping the bomb.");
					});
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPickup>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValidPlayer() && CFG.config.BombPickupPoints > 0 && IsPointsAllowed())
				{
					_ = Queries.AddPointsAsync(playerController, CFG.config.BombPickupPoints);
					playerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.BombPickupPoints} XP for picking up the bomb.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDefused>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValidPlayer() && CFG.config.DefusePoints > 0 && IsPointsAllowed())
				{
					_ = Queries.AddPointsAsync(playerController, CFG.config.DefusePoints);
					playerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.DefusePoints} XP for defusing the bomb.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundMvp>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValidPlayer() && CFG.config.MVPPoints > 0 && IsPointsAllowed())
				{
					_ = Queries.AddPointsAsync(playerController, CFG.config.MVPPoints);
					playerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.MVPPoints} XP for being the MVP.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundEnd>((@event, info) =>
			{
				CsTeam winnerTeam = (CsTeam)@event.Winner;

				if (!IsPointsAllowed())
					return HookResult.Continue;

				for (int playerIndex = 0; playerIndex <= Server.MaxPlayers; playerIndex++)
				{
					CCSPlayerController playerController = Utilities.GetPlayerFromUserid(playerIndex);

					if (playerController.IsValidPlayer())
					{
						CsTeam playerTeam = (CsTeam)playerController.TeamNum;

						if (playerTeam != CsTeam.None && playerTeam != CsTeam.Spectator)
						{
							if (playerTeam == winnerTeam)
							{
								if (CFG.config.RoundWinPoints > 0)
								{
									_ = Queries.AddPointsAsync(playerController, CFG.config.RoundWinPoints);
									playerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.RoundWinPoints} XP for winning the round.");
								}
							}
							else if (CFG.config.RoundLosePoints > 0)
							{
								_ = Queries.RemovePointsAsync(playerController, CFG.config.RoundLosePoints);
								playerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.RoundLosePoints} XP for losing the round.");
							}
						}
					}
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPlanted>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValidPlayer() && CFG.config.PlantPoints > 0 && IsPointsAllowed())
				{
					_ = Queries.AddPointsAsync(playerController, CFG.config.PlantPoints);
					playerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.PlantPoints} XP for planting the bomb.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerDeath>((@event, info) =>
			{
				if (!IsPointsAllowed())
					return HookResult.Continue;

				CCSPlayerController victimController = @event.Userid;
				CCSPlayerController killerController = @event.Attacker;
				CCSPlayerController assisterController = @event.Assister;

				// Decrease victim points
				if (!victimController.IsValid)
					return HookResult.Continue;

				if (!victimController.IsBot)
				{
					if (victimController.UserId == killerController.UserId)
					{
						if (CFG.config.SuicidePoints > 0)
						{
							_ = Queries.RemovePointsAsync(victimController, CFG.config.SuicidePoints);
							victimController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.SuicidePoints} XP for suicide.");
						}
					}
					else
					{
						if (CFG.config.DeathPoints > 0)
						{
							_ = Queries.RemovePointsAsync(victimController, CFG.config.DeathPoints);
							victimController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.DeathPoints} XP for dying.");
						}
					}
				}

				if ((!CFG.config.PointsForBots && victimController.IsBot) || victimController.UserId == killerController.UserId)
					return HookResult.Continue;

				// Increase killer points
				if (killerController.IsValidPlayer())
				{
					if (!CFG.config.FFAMode && killerController.TeamNum == victimController.TeamNum)
					{
						if (CFG.config.TeamKillPoints > 0)
						{
							_ = Queries.RemovePointsAsync(killerController, CFG.config.TeamKillPoints);
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.TeamKillPoints} XP for killing a teammate.");
						}
					}
					else
					{
						int pointChange = 0;

						pointChange += CFG.config.KillPoints;
						killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.KillPoints} XP for killing an enemy.");

						if (@event.Headshot && CFG.config.HeadshotPoints > 0)
						{
							pointChange += CFG.config.HeadshotPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.HeadshotPoints} XP for headshot.");
						}

						int penetrateCount = @event.Penetrated;
						if (penetrateCount > 0 && CFG.config.PenetratedPoints > 0)
						{
							int calculatedPoints = @event.Penetrated * CFG.config.PenetratedPoints;
							pointChange += calculatedPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {calculatedPoints} XP for NoScope {penetrateCount} objects before killing the target.");
						}

						if (@event.Noscope && CFG.config.NoScopePoints > 0)
						{
							pointChange += CFG.config.NoScopePoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.NoScopePoints} XP for NoScope.");
						}

						if (@event.Thrusmoke && CFG.config.ThrusmokePoints > 0)
						{
							pointChange += CFG.config.ThrusmokePoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.ThrusmokePoints} XP killing thru smoke.");
						}

						if (@event.Attackerblind && CFG.config.BlindKillPoints > 0)
						{
							pointChange += CFG.config.BlindKillPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.BlindKillPoints} XP for killing while being flashed.");
						}

						if (@event.Distance >= CFG.config.LongDistance && CFG.config.LongDistanceKillPoints > 0)
						{
							pointChange += CFG.config.LongDistanceKillPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.LongDistanceKillPoints} XP for long distance kill.");
						}

						int attackerIndex = killerController.UserId ?? 0;
						if (playerKillStreaks.ContainsKey(attackerIndex))
						{
							// Check if the player got a kill within the last 5 seconds
							if (playerKillStreaks[attackerIndex].killStreak > 0 && DateTime.Now - playerKillStreaks[attackerIndex].lastKillTime <= TimeSpan.FromSeconds(CFG.config.SecondsBetweenKills))
							{
								playerKillStreaks[attackerIndex] = (playerKillStreaks[attackerIndex].killStreak + 1, DateTime.Now);
								int killStreak = playerKillStreaks[attackerIndex].killStreak;

								// Award points for the kill streak
								int points = 0;
								string killStreakMessage = "";

								switch (killStreak)
								{
									case 2:
										points = CFG.config.DoubleKillPoints;
										killStreakMessage = "DoubleKill";
										break;
									case 3:
										points = CFG.config.TripleKillPoints;
										killStreakMessage = "TripleKill";
										break;
									case 4:
										points = CFG.config.DominationPoints;
										killStreakMessage = "Domination";
										break;
									case 5:
										points = CFG.config.RampagePoints;
										killStreakMessage = "Rampage";
										break;
									case 6:
										points = CFG.config.MegaKillPoints;
										killStreakMessage = "MegaKill";
										break;
									case 7:
										points = CFG.config.OwnagePoints;
										killStreakMessage = "Ownage";
										break;
									case 8:
										points = CFG.config.UltraKillPoints;
										killStreakMessage = "UltraKill";
										break;
									case 9:
										points = CFG.config.KillingSpreePoints;
										killStreakMessage = "KillingSpree";
										break;
									case 10:
										points = CFG.config.MonsterKillPoints;
										killStreakMessage = "MonsterKill";
										break;
									case 11:
										points = CFG.config.UnstoppablePoints;
										killStreakMessage = "Unstoppable";
										break;
									case 12:
										points = CFG.config.GodLikePoints;
										killStreakMessage = "GodLike";
										break;
									default:
										// Handle other cases or reset the kill streak
										ResetKillStreak(attackerIndex);
										break;
								}

								if (points > 0)
								{
									pointChange += points;
									killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {points} XP for a {killStreakMessage}!");
								}
							}
							else
							{
								// No kill streak, reset the count
								ResetKillStreak(attackerIndex);
							}

							if (pointChange > 0)
								_ = Queries.AddPointsAsync(killerController, pointChange);
						}
					}
				}

				// Increase assister points
				if (assisterController != null)
				{
					int assisterIndex = assisterController.UserId ?? -1;

					if (assisterIndex != -1)
					{
						int pointChange = 0;

						if (CFG.config.AssistPoints > 0)
						{
							pointChange += CFG.config.AssistPoints;
							assisterController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.AssistPoints} XP for assisting in a kill.");
						}

						if (@event.Assistedflash && CFG.config.AsssistFlashPoints > 0)
						{
							pointChange += CFG.config.AsssistFlashPoints;
							assisterController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.AsssistFlashPoints} XP for assisting with a flash.");
						}

						if (pointChange > 0)
							_ = Queries.AddPointsAsync(assisterController, pointChange);
					}
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (CFG.config.DisableSpawnMessage || !player.PlayerPawn.IsValid && IsPointsAllowed())
					return HookResult.Continue;

				player.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}The server is using {ChatColors.Gold}SimpleRanks {ChatColors.White}plugin. Type {ChatColors.Red}!rank {ChatColors.White}to get more information!");

				return HookResult.Continue;
			});
		}

		[ConsoleCommand("rank", "Check the current rank and points")]
		public async void OnCommandCheckRank(CCSPlayerController? player, CommandInfo command)
		{
			if (player == null || player.PlayerPawn == null || !player.PlayerPawn.IsValid)
				return;

			string steamId = player.SteamID.ToString();

			int playerVariable = await Queries.GetPointsAsync(steamId);

			var (colorCode, rankName) = await Queries.GetRankInfoAsync(player);

			Utilities.ReplyToCommand(player, $" {CFG.config.ChatPrefix} {colorCode}{player.PlayerName} {ChatColors.White}has {ChatColors.Red}{playerVariable} {ChatColors.White}points and is currently {colorCode}{rankName}");
			return;
		}

		[ConsoleCommand("ranktop", "Check the current rank and points")]
		public async void OnCommandCheckRankTop(CCSPlayerController? player, CommandInfo command)
		{
			if (player == null || player.PlayerPawn == null || !player.PlayerPawn.IsValid)
				return;

			List<(string PlayerName, int Points, string Rank)>? topPlayers = await Queries.GetTopPlayersAsync();

			if (topPlayers != null && topPlayers.Count > 0)
			{
				player.PrintToChat($" {CFG.config.ChatPrefix} Top 5 Players:");

				for (int i = 0; i < topPlayers.Count; i++)
				{
					player.PrintToChat($" {ChatColors.Gold}{i + 1}. {ChatColors.Blue}[{topPlayers[i].Rank}] {ChatColors.Gold}{topPlayers[i].PlayerName} - {ChatColors.Blue}{topPlayers[i].Points} points");
				}
			}
			else
			{
				player.PrintToChat($" {CFG.config.ChatPrefix} No players found in the top 5.");
			}
		}

		[GameEventHandler]
		public HookResult OnClientConnect(EventPlayerConnectFull @event, GameEventInfo info)
		{
			CCSPlayerController playerController = @event.Userid;

			if (playerController.IsValidPlayer())
			{
				_ = Queries.InsertUserAsync(playerController);
			}

			return HookResult.Continue;
		}

		public static void Log(string message)
		{
			string logFile = Path.Join(Directory, "logs.txt");
			using (StreamWriter writer = File.AppendText(logFile))
			{
				writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message);
			}

			Console.WriteLine(message);
		}

		public bool IsPointsAllowed()
		{
			int clients = 0;

			for (int i = 0; i <= Server.MaxPlayers; i++)
			{
				CCSPlayerController player = Utilities.GetPlayerFromIndex(i);

				if (player != null && player.IsValid && !player.IsBot && player.PlayerPawn != null)
					clients++;
			}

			return (!K4ryuu.GameRules().WarmupPeriod || CFG.config.WarmupPoints) && (CFG.config.MinPlayers <= clients);
		}
		private void ResetKillStreak(int playerIndex)
		{
			playerKillStreaks[playerIndex] = (1, DateTime.Now);
		}
	}
}