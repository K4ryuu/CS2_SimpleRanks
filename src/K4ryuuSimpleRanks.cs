using Newtonsoft.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4ryuuSimpleRanks
{
	public class Rank
	{
		public int Exp { get; set; }
		public string Color { get; set; } = "Default";
	}

	public class SimpleRanks : BasePlugin
	{
		private Dictionary<string, string> configValues = new Dictionary<string, string>();
		private Dictionary<int, (int killStreak, DateTime lastKillTime)> playerKillStreaks = new Dictionary<int, (int, DateTime)>();
		public Dictionary<string, Rank> ranks = new Dictionary<string, Rank>();
		public override string ModuleName => "Simple Ranks";
		public override string ModuleVersion => "v1.0.0";

		public override void Load(bool hotReload)
		{
			for (int i = 0; i <= Server.MaxPlayers; i++)
			{
				playerKillStreaks[i] = (0, DateTime.MinValue);

				if (hotReload)
				{
					CCSPlayerController targetController = new CCSPlayerController(NativeAPI.GetEntityFromIndex(i));
					Queries.InsertUser(targetController.SteamID.ToString());
				}
			}

			new CFG().CheckConfig(ModuleDirectory);

			LoadRanksFromConfig();
			SetupGameEvents();

			Log($"{ModuleName} [{ModuleVersion}] by K4ryuu has been loaded.");
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
						ranks = JsonConvert.DeserializeObject<Dictionary<string, Rank>>(jsonContent);
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

				if (playerController.IsValid && !playerController.IsBot)
				{
					Queries.AddPoints(playerController, CFG.config.HostageRescuePoints, ranks);
					playerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.HostageRescuePoints} XP for rescuing a hostage.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageKilled>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValid && !playerController.IsBot)
				{
					Queries.RemovePoints(playerController, CFG.config.HostageKillPoints, ranks);
					playerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.HostageKillPoints} XP for killing the hostage.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageHurt>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValid && !playerController.IsBot)
				{
					Queries.RemovePoints(playerController, CFG.config.HostageHurtPoints, ranks);
					playerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.HostageHurtPoints} XP for hurting the hostage.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDropped>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValid && !playerController.IsBot)
				{
					Queries.RemovePoints(playerController, CFG.config.BombDropPoints, ranks);

					Server.NextFrame(() =>
					{
						playerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.BombDropPoints} XP for dropping the bomb.");
					});
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPickup>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValid && !playerController.IsBot)
				{
					Queries.AddPoints(playerController, CFG.config.BombPickupPoints, ranks);
					playerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.BombPickupPoints} XP for picking up the bomb.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDefused>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValid && !playerController.IsBot)
				{
					Queries.AddPoints(playerController, CFG.config.DefusePoints, ranks);
					playerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.DefusePoints} XP for defusing the bomb.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundMvp>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValid && !playerController.IsBot)
				{
					Queries.AddPoints(playerController, CFG.config.MVPPoints, ranks);
					playerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.MVPPoints} XP for being the MVP.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundEnd>((@event, info) =>
			{
				CsTeam winnerTeam = (CsTeam)@event.Winner;

				for (int playerIndex = 0; playerIndex <= Server.MaxPlayers; playerIndex++)
				{
					CCSPlayerController playerController = new CCSPlayerController(NativeAPI.GetEntityFromIndex(playerIndex));

					if (playerController.IsValid && !playerController.IsBot)
					{
						CsTeam playerTeam = (CsTeam)playerController.TeamNum;

						if (playerTeam != CsTeam.None && playerTeam != CsTeam.Spectator)
						{
							if (playerTeam == winnerTeam)
							{
								Queries.AddPoints(playerController, CFG.config.RoundWinPoints, ranks);
								playerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.RoundWinPoints} XP for winning the round.");
							}
							else
							{
								Queries.RemovePoints(playerController, CFG.config.RoundLosePoints, ranks);
								playerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.RoundLosePoints} XP for losing the round.");
							}
						}
					}
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPlanted>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValid && !playerController.IsBot)
				{
					Queries.AddPoints(playerController, CFG.config.PlantPoints, ranks);
					playerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.PlantPoints} XP for planting the bomb.");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerDeath>((@event, info) =>
			{
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
						Queries.RemovePoints(victimController, CFG.config.SuicidePoints, ranks);
						victimController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.SuicidePoints} XP for suicide.");
					}
					else
					{
						Queries.RemovePoints(victimController, CFG.config.DeathPoints, ranks);
						victimController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.DeathPoints} XP for dying.");
					}
				}

				if (!CFG.config.PointsForBots && victimController.IsBot)
					return HookResult.Continue;

				// Increase killer points
				if (killerController.IsValid && !killerController.IsBot)
				{
					if (killerController.TeamNum == victimController.TeamNum)
					{
						Queries.RemovePoints(killerController, CFG.config.TeamKillPoints, ranks);
						killerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.LightRed}You have lost {CFG.config.TeamKillPoints} XP for killing a teammate.");

					}
					else
					{
						Queries.AddPoints(killerController, CFG.config.KillPoints, ranks);
						killerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.KillPoints} XP for killing an enemy.");

						if (@event.Headshot)
						{
							Queries.AddPoints(killerController, CFG.config.HeadshotPoints, ranks);
							killerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.HeadshotPoints} XP for headshot.");
						}

						int penetrateCount = @event.Penetrated;
						if (penetrateCount > 0)
						{
							int calculatedPoints = @event.Penetrated * CFG.config.PenetratedPoints;
							Queries.AddPoints(killerController, calculatedPoints, ranks);
							killerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {calculatedPoints} XP for NoScope {penetrateCount} objects before killing the target.");
						}

						if (@event.Noscope)
						{
							Queries.AddPoints(killerController, CFG.config.NoScopePoints, ranks);
							killerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.NoScopePoints} XP for NoScope.");
						}

						if (@event.Thrusmoke)
						{
							Queries.AddPoints(killerController, CFG.config.ThrusmokePoints, ranks);
							killerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.ThrusmokePoints} XP killing thru smoke.");
						}

						if (@event.Attackerblind)
						{
							Queries.AddPoints(killerController, CFG.config.BlindKillPoints, ranks);
							killerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.BlindKillPoints} XP for killing while being flashed.");
						}

						if (@event.Distance >= float.Parse(configValues["LongDistance"]))
						{
							Queries.AddPoints(killerController, CFG.config.LongDistanceKillPoints, ranks);
							killerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.LongDistanceKillPoints} XP for long distance kill.");
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
									Queries.AddPoints(killerController, points, ranks);
									killerController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {points} XP for a {killStreakMessage}!");
								}
							}
							else
							{
								// No kill streak, reset the count
								ResetKillStreak(attackerIndex);
							}
						}
					}
				}

				// Increase assister points
				if (assisterController != null)
				{
					int assisterIndex = assisterController.UserId ?? -1;

					if (assisterIndex != -1)
					{
						Queries.AddPoints(assisterController, CFG.config.AssistPoints, ranks);
						assisterController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.AssistPoints} XP for assisting in a kill.");

						if (@event.Assistedflash)
						{
							Queries.AddPoints(assisterController, CFG.config.AsssistFlashPoints, ranks);
							assisterController.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Lime}You have gained {CFG.config.AsssistFlashPoints} XP for assisting with a flash.");
						}
					}
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (CFG.config.DisableSpawnMessage || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				player.PrintToChat($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.White}The server is using {ChatColors.Gold}SimpleRanks {ChatColors.White}plugin. Type {ChatColors.Red}!rank {ChatColors.White}to get more information!");


				return HookResult.Continue;
			});
		}

		[ConsoleCommand("rank", "Check the current rank and points")]
		public void OnCommandCheckRank(CCSPlayerController? player, CommandInfo command)
		{
			if (player == null || player.PlayerPawn == null || !player.PlayerPawn.IsValid)
				return;

			string steamId = player.SteamID.ToString();

			int playerVariable = Queries.GetPoints(steamId);

			var (colorCode, rankName) = Queries.GetRankInfo(player, ranks);

			Server.PrintToChatAll($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {colorCode}{player.PlayerName} {ChatColors.White}has {ChatColors.Red}{playerVariable} {ChatColors.White}points and is currently {colorCode}{rankName}");
			return;
		}

		[GameEventHandler]
		public HookResult OnClientConnect(EventPlayerConnectFull @event, GameEventInfo info)
		{
			CCSPlayerController playerController = @event.Userid;

			if (playerController != null)
			{
				int playerIndex = playerController.UserId ?? -1;

				if (playerIndex != -1)
					Queries.InsertUser(playerController.SteamID.ToString());
			}

			return HookResult.Continue;
		}

		public void Log(string message)
		{
			string logFile = Path.Join(ModuleDirectory, "logs.txt");
			using (StreamWriter writer = File.AppendText(logFile))
			{
				writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message);
			}

			Console.WriteLine(message);
		}

		private void ResetKillStreak(int playerIndex)
		{
			playerKillStreaks[playerIndex] = (1, DateTime.Now);
		}
	}
}