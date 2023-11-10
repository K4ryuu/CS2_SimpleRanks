using Newtonsoft.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Attributes;
using Nexd.MySQL;
using System.Reflection;
using MySqlConnector;
using System.Linq.Expressions;
using CounterStrikeSharp.API.Modules.Admin;

namespace K4ryuuSimpleRanks
{
	public class Rank
	{
		public int Exp { get; set; }
		public string Color { get; set; } = "Default";
	}

	public class User
	{
		public int Points { get; set; }
		public string Rank { get; set; } = "None";
		public string RankColor { get; set; } = $"{ChatColors.Default}";
		public int RankPoints { get; set; } = -1;
		public bool SpawnedThisRound { get; set; } = false;
	}

	public class PlayerCache<T> : Dictionary<int, T>
	{
		public T this[CCSPlayerController controller]
		{
			get { return (T)this[controller.UserId!.Value]; }
			set { this[controller.UserId!.Value] = value; }
		}

		public T GetFromIndex(int index)
		{
			return (T)this[index - 1];
		}

		public bool ContainsPlayer(CCSPlayerController player)
		{
			return base.ContainsKey(player.UserId!.Value);
		}

		public bool RemovePlayer(CCSPlayerController player)
		{
			return base.Remove(player.UserId!.Value);
		}
	}

	public enum CHANGE_MODE
	{
		SET = 0,
		GIVE,
		REMOVE
	}

	[MinimumApiVersion(27)]
	public class SimpleRanks : BasePlugin
	{
		MySqlDb? MySql = null;
		private Dictionary<int, (int killStreak, DateTime lastKillTime)> playerKillStreaks = new Dictionary<int, (int, DateTime)>();
		public static Dictionary<string, Rank> ranks = new Dictionary<string, Rank>();
		internal static PlayerCache<User> PlayerSummaries = new PlayerCache<User>();
		public override string ModuleName => "Simple Ranks";
		public override string ModuleVersion => "v2.0.2";
		public override string ModuleAuthor => "K4ryuu";

		public override void Load(bool hotReload)
		{
			new CFG().CheckConfig(ModuleDirectory);

			MySql = new MySqlDb(CFG.config.DatabaseHost!, CFG.config.DatabaseUser!, CFG.config.DatabasePassword!, CFG.config.DatabaseName!, CFG.config.DatabasePort);
			MySql.ExecuteNonQueryAsync(@"CREATE TABLE IF NOT EXISTS `k4ranks` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steam_id` VARCHAR(255) NOT NULL, `name` VARCHAR(255) DEFAULT NULL, `points` INT NOT NULL DEFAULT 0, UNIQUE (`steam_id`));");

			List<CCSPlayerController> players = new List<CCSPlayerController>();
			foreach (CCSPlayerController player in players)
			{
				if (!player.IsValidPlayer())
					continue;

				LoadPlayerData(player);
			}

			LoadRanksFromConfig();
			SetupGameEvents();

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
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, CFG.config.HostageRescuePoints, "Hostage Rescued");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageKilled>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.REMOVE, CFG.config.HostageKillPoints, "Hostage Killed");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageHurt>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.REMOVE, CFG.config.HostageHurtPoints, "Hostage Hurt");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDropped>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.REMOVE, CFG.config.BombDropPoints, "Bomb Dropped");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPickup>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, CFG.config.BombPickupPoints, "Bomb Pickup");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDefused>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, CFG.config.DefusePoints, "Bomb Defused");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundMvp>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, CFG.config.MVPPoints, "Round MVP");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundEnd>((@event, info) =>
			{
				CsTeam winnerTeam = (CsTeam)@event.Winner;

				if (!IsPointsAllowed())
					return HookResult.Continue;

				List<CCSPlayerController> players = new List<CCSPlayerController>();
				foreach (CCSPlayerController player in players)
				{
					if (!player.IsValidPlayer() || !PlayerSummaries[player].SpawnedThisRound)
						continue;

					CsTeam playerTeam = (CsTeam)player.TeamNum;

					if (playerTeam != CsTeam.None && playerTeam != CsTeam.Spectator)
					{
						if (playerTeam == winnerTeam)
						{
							ModifyClientPoints(player, CHANGE_MODE.GIVE, CFG.config.RoundWinPoints, "Round Win");
						}
						else
						{
							ModifyClientPoints(player, CHANGE_MODE.REMOVE, CFG.config.RoundLosePoints, "Round Lose");
						}
					}

					PlayerSummaries[player].SpawnedThisRound = false;
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPlanted>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, CFG.config.PlantPoints, "Bomb Plant");
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
						ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, CFG.config.SuicidePoints, "Suicide");
					}
					else
					{
						ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, CFG.config.DeathPoints, "Die");
					}
				}

				if ((!CFG.config.PointsForBots && victimController.IsBot) || victimController.UserId == killerController.UserId)
					return HookResult.Continue;

				// Increase killer points
				if (killerController.IsValidPlayer())
				{
					if (!CFG.config.FFAMode && killerController.TeamNum == victimController.TeamNum)
					{
						ModifyClientPoints(killerController, CHANGE_MODE.REMOVE, CFG.config.TeamKillPoints, "TeamKill");
					}
					else
					{
						if (!PlayerSummaries.ContainsPlayer(killerController))
							LoadPlayerData(killerController);

						int pointChange = 0;

						if (CFG.config.HeadshotPoints > 0)
						{
							pointChange += CFG.config.KillPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points}[+{CFG.config.KillPoints} Kill]");
							PlayerSummaries[killerController].Points += CFG.config.KillPoints;
						}

						if (@event.Headshot && CFG.config.HeadshotPoints > 0)
						{
							pointChange += CFG.config.HeadshotPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points}[+{CFG.config.HeadshotPoints} Headshot]");
							PlayerSummaries[killerController].Points += CFG.config.HeadshotPoints;
						}

						int penetrateCount = @event.Penetrated;
						if (penetrateCount > 0 && CFG.config.PenetratedPoints > 0)
						{
							int calculatedPoints = @event.Penetrated * CFG.config.PenetratedPoints;
							pointChange += calculatedPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points}[+{calculatedPoints} Penetration]");
							PlayerSummaries[killerController].Points += calculatedPoints;
						}

						if (@event.Noscope && CFG.config.NoScopePoints > 0)
						{
							pointChange += CFG.config.NoScopePoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points}[+{CFG.config.NoScopePoints} NoScope]");
							PlayerSummaries[killerController].Points += CFG.config.NoScopePoints;
						}

						if (@event.Thrusmoke && CFG.config.ThrusmokePoints > 0)
						{
							pointChange += CFG.config.ThrusmokePoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points}[+{CFG.config.ThrusmokePoints} ThruSmoke]");
							PlayerSummaries[killerController].Points += CFG.config.ThrusmokePoints;
						}

						if (@event.Attackerblind && CFG.config.BlindKillPoints > 0)
						{
							pointChange += CFG.config.BlindKillPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points}[+{CFG.config.BlindKillPoints} Blind Kill]");
							PlayerSummaries[killerController].Points += CFG.config.BlindKillPoints;
						}

						if (@event.Distance >= CFG.config.LongDistance && CFG.config.LongDistanceKillPoints > 0)
						{
							pointChange += CFG.config.LongDistanceKillPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points}[+{CFG.config.LongDistanceKillPoints} Long Distance]");
							PlayerSummaries[killerController].Points += CFG.config.LongDistanceKillPoints;
						}

						string lowerCaseWeaponName = @event.Weapon.ToLower();

						switch (lowerCaseWeaponName)
						{
							case var _ when lowerCaseWeaponName.Contains("hegrenade") || lowerCaseWeaponName.Contains("tagrenade") || lowerCaseWeaponName.Contains("firebomb") || lowerCaseWeaponName.Contains("molotov") || lowerCaseWeaponName.Contains("incgrenade") || lowerCaseWeaponName.Contains("flashbang") || lowerCaseWeaponName.Contains("smokegrenade") || lowerCaseWeaponName.Contains("frag") || lowerCaseWeaponName.Contains("bumpmine"):
								{
									pointChange += CFG.config.GrenadeKillPoints;
									killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points}[+{CFG.config.GrenadeKillPoints} Grenade Kill]");
									PlayerSummaries[killerController].Points += CFG.config.GrenadeKillPoints;

									break;
								}
							case var _ when lowerCaseWeaponName.Contains("cord") || lowerCaseWeaponName.Contains("bowie") || lowerCaseWeaponName.Contains("butterfly") || lowerCaseWeaponName.Contains("karambit") || lowerCaseWeaponName.Contains("skeleton") || lowerCaseWeaponName.Contains("m9_bayonet") || lowerCaseWeaponName.Contains("bayonet") || lowerCaseWeaponName.Contains("t") || lowerCaseWeaponName.Contains("knifegg") || lowerCaseWeaponName.Contains("stiletto") || lowerCaseWeaponName.Contains("ursus") || lowerCaseWeaponName.Contains("tactical") || lowerCaseWeaponName.Contains("push") || lowerCaseWeaponName.Contains("widowmaker") || lowerCaseWeaponName.Contains("outdoor") || lowerCaseWeaponName.Contains("canis"):
								{
									pointChange += CFG.config.KnifeKillPoints;
									killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points}[+{CFG.config.KnifeKillPoints} Knife Kill]");
									PlayerSummaries[killerController].Points += CFG.config.KnifeKillPoints;

									break;
								}
							case "taser":
								{
									pointChange += CFG.config.TaserKillPoints;
									killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points}[+{CFG.config.TaserKillPoints} Taser Kill]");
									PlayerSummaries[killerController].Points += CFG.config.TaserKillPoints;

									break;
								}
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
									killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points}[+{points} {killStreakMessage}]");
									PlayerSummaries[killerController].Points += points;
								}
							}
							else
							{
								// No kill streak, reset the count
								ResetKillStreak(attackerIndex);
							}
						}

						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` + {pointChange}) WHERE `steam_id` = {killerController.SteamID};");
					}
				}

				if (assisterController.IsValidPlayer())
				{
					if (!PlayerSummaries.ContainsPlayer(assisterController))
						LoadPlayerData(assisterController);

					int pointChange = 0;

					if (CFG.config.AssistPoints > 0)
					{
						pointChange += CFG.config.AssistPoints;
						assisterController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[assisterController].Points}[+{CFG.config.AssistPoints} Assist]");
						PlayerSummaries[assisterController].Points += CFG.config.AssistPoints;
					}

					if (@event.Assistedflash && CFG.config.AsssistFlashPoints > 0)
					{
						pointChange += CFG.config.AsssistFlashPoints;
						assisterController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[assisterController].Points}[+{CFG.config.AsssistFlashPoints} Flash Assist]");
						PlayerSummaries[assisterController].Points += CFG.config.AsssistFlashPoints;
					}

					MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` + {pointChange}) WHERE `steam_id` = {assisterController.SteamID};");
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (!player.IsValidPlayer())
					return HookResult.Continue;

				PlayerSummaries[player].SpawnedThisRound = true;

				if (CFG.config.DisableSpawnMessage)
					return HookResult.Continue;

				if (!PlayerSummaries.ContainsPlayer(player!))
					LoadPlayerData(player!);

				player.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Green}The server is using {ChatColors.Gold}SimpleRanks {ChatColors.Green}plugin. Type {ChatColors.Red}!rank {ChatColors.Green}to get more information!");

				return HookResult.Continue;
			});
		}

		[ConsoleCommand("rank", "Check the current rank and points")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandCheckRank(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			player!.PrintToChat($" {CFG.config.ChatPrefix} {PlayerSummaries[player!].RankColor}{player!.PlayerName} {ChatColors.White}has {ChatColors.Red}{PlayerSummaries[player].Points} {ChatColors.White}points and is currently {PlayerSummaries[player].RankColor}{PlayerSummaries[player].Rank}");
		}

		[ConsoleCommand("resetmyrank", "Resets the player's own points to zero")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandResetMyRank(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (!PlayerSummaries.ContainsPlayer(player!))
				LoadPlayerData(player!);

			MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = 0 WHERE `steam_id` = {player!.SteamID};");

			Server.PrintToChatAll($" {CFG.config.ChatPrefix} {ChatColors.Red}{player.PlayerName} has reset their rank and points.");
		}

		[ConsoleCommand("ranktop", "Check the top 5 players by points")]
		[CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public async void OnCommandCheckRankTop(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			MySqlQueryResult result = await MySql!.Table("k4ranks").ExecuteQueryAsync("SELECT `points`, `name` FROM `k4ranks` ORDER BY `points` DESC LIMIT 5;");

			if (result.Count > 0)
			{
				player!.PrintToChat($" {CFG.config.ChatPrefix} Top 5 Players:");

				for (int i = 0; i < result.Count; i++)
				{
					int pointChcek = result.Get<int>(i, "points");
					string playerRank = "None";

					foreach (var kvp in ranks)
					{
						string level = kvp.Key;
						Rank rank = kvp.Value;

						if (pointChcek >= rank.Exp)
						{
							playerRank = level;
						}
						else
							break;
					}
					player.PrintToChat($" {ChatColors.Gold}{i + 1}. {ChatColors.Blue}[{playerRank}] {ChatColors.Gold}{result.Get<string>(i, "name")} - {ChatColors.Blue}{result.Get<int>(i, "points")} points");
				}
			}
			else
			{
				player!.PrintToChat($" {CFG.config.ChatPrefix} No players found in the top 5.");
			}
		}

		[ConsoleCommand("resetrank", "Resets the targeted player's points to zero")]
		[CommandHelper(minArgs: 1, usage: "[SteamID64]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4ranks/admin")]
		public void OnCommandResetOtherRank(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			List<CCSPlayerController> players = Utilities.GetPlayers();
			foreach (CCSPlayerController target in players)
			{
				if (target.IsValidPlayer() && target.SteamID.ToString() == Regex.Replace(command.ArgByIndex(1), @"['"",\s]", ""))
				{
					if (!PlayerSummaries.ContainsPlayer(target!))
						LoadPlayerData(target!);

					MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = 0 WHERE `steam_id` = {target.SteamID};");

					Server.PrintToChatAll($" {CFG.config.ChatPrefix} {ChatColors.Red}{target.PlayerName}'s rank and points has been reset by {player!.PlayerName}.");
					Log($"{player.PlayerName} has reset {target.PlayerName}'s points.");

					PlayerSummaries[player].Points = 0;
					CheckNewRank(player);

					return;
				}
			}
		}

		[ConsoleCommand("setpoints", "Sets the targeted player's points to the given value")]
		[CommandHelper(minArgs: 2, usage: "[SteamID64] <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4ranks/admin")]
		public void OnCommandSetPoints(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (int.TryParse(command.ArgByIndex(2), out int parsedInt))
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController target in players)
				{
					if (target.IsValidPlayer() && target.SteamID.ToString() == Regex.Replace(command.ArgByIndex(1), @"['"",\s]", ""))
					{
						if (!PlayerSummaries.ContainsPlayer(target!))
							LoadPlayerData(target!);

						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = {parsedInt} WHERE `steam_id` = {target.SteamID};");

						Server.PrintToChatAll($" {CFG.config.ChatPrefix} {ChatColors.Red}{target.PlayerName}'s points has been set to {parsedInt} by {player!.PlayerName}.");
						Log($"{player.PlayerName} has set {target.PlayerName}'s points to {parsedInt}.");

						PlayerSummaries[player].Points = parsedInt;
						CheckNewRank(player);

						return;
					}
				}
			}
			else
			{
				player!.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Red}The given amount is invalid.");
				return;
			}
		}

		[ConsoleCommand("givepoints", "Gives points to the targeted player")]
		[CommandHelper(minArgs: 2, usage: "[SteamID64] <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4ranks/admin")]
		public void OnCommandGivePoints(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (int.TryParse(command.ArgByIndex(2), out int parsedInt))
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController target in players)
				{
					if (target.IsValidPlayer() && target.SteamID.ToString() == Regex.Replace(command.ArgByIndex(1), @"['"",\s]", ""))
					{
						if (!PlayerSummaries.ContainsPlayer(target!))
							LoadPlayerData(target!);

						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` + {parsedInt}) WHERE `steam_id` = {target.SteamID};");

						Server.PrintToChatAll($" {CFG.config.ChatPrefix} {ChatColors.Red}{player!.PlayerName} has given {parsedInt} points to {target.PlayerName}.");
						Log($"{player.PlayerName} has given {parsedInt} points to {target.PlayerName}.");

						PlayerSummaries[player].Points += parsedInt;
						CheckNewRank(player);

						return;
					}
				}
			}
			else
			{
				player!.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Red}The given amount is invalid.");
				return;
			}
		}

		[ConsoleCommand("removepoints", "Removes points from the targeted player")]
		[CommandHelper(minArgs: 2, usage: "[SteamID64] <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
		[RequiresPermissions("@k4ranks/admin")]
		public void OnCommandRemovePoints(CCSPlayerController? player, CommandInfo command)
		{
			if (!player.IsValidPlayer())
				return;

			if (int.TryParse(command.ArgByIndex(2), out int parsedInt))
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController target in players)
				{
					if (target.IsValidPlayer() && target.SteamID.ToString() == Regex.Replace(command.ArgByIndex(1), @"['"",\s]", ""))
					{
						if (!PlayerSummaries.ContainsPlayer(target!))
							LoadPlayerData(target!);

						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` - {parsedInt}) WHERE `steam_id` = {target.SteamID};");

						Server.PrintToChatAll($" {CFG.config.ChatPrefix} {ChatColors.Red}{player!.PlayerName} has removed {parsedInt} points from {target.PlayerName}.");
						Log($"{player.PlayerName} has removed {parsedInt} points from {target.PlayerName}.");

						PlayerSummaries[player].Points -= parsedInt;

						if (PlayerSummaries[player].Points < 0)
							PlayerSummaries[player].Points = 0;

						CheckNewRank(player);

						return;
					}
				}
			}
			else
			{
				player!.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Red}The given amount is invalid.");
				return;
			}
		}

		[GameEventHandler]
		public HookResult OnClientConnect(EventPlayerConnectFull @event, GameEventInfo info)
		{
			CCSPlayerController playerController = @event.Userid;

			playerKillStreaks[playerController.UserId ?? 0] = (0, DateTime.MinValue);

			if (playerController.IsValidPlayer())
				LoadPlayerData(playerController);

			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnClientDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
		{
			CCSPlayerController playerController = @event.Userid;

			if (playerController.IsValidPlayer())
				PlayerSummaries.RemovePlayer(playerController);

			return HookResult.Continue;
		}

		public void LoadPlayerData(CCSPlayerController player)
		{
			User newUser = new User
			{
				Points = 0,
				Rank = "None",
				RankColor = $"{ChatColors.Default}",
				RankPoints = -1
			};
			PlayerSummaries[player] = newUser;

			MySqlQueryValue values = new MySqlQueryValue()
										.Add("name", player.PlayerName)
										.Add("steam_id", player.SteamID.ToString());

			MySql!.Table("k4ranks").InsertIfNotExist(values, $"`name` = '{player.PlayerName}'");

			MySqlQueryResult result = MySql!.Table("k4ranks").Where(MySqlQueryCondition.New("steam_id", "=", player.SteamID.ToString())).Select("points");

			PlayerSummaries[player].Points = result.Rows > 0 ? result.Get<int>(0, "points") : 0;

			string newRank = "None";
			Rank? setRank = null;

			foreach (var kvp in ranks)
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (PlayerSummaries[player].Points >= rank.Exp)
				{
					setRank = rank;
					newRank = level;
				}
				else
					break;
			}

			if (setRank == null)
				return;

			if (CFG.config.ScoreboardRanks)
				player.Clan = newRank;

			PlayerSummaries[player].Rank = newRank;
			PlayerSummaries[player].RankPoints = setRank.Exp;

			string modifiedValue = setRank.Color;
			foreach (FieldInfo field in typeof(ChatColors).GetFields())
			{
				string pattern = $"{field.Name}";
				if (setRank.Color.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			PlayerSummaries[player].RankColor = modifiedValue;
		}

		public void ModifyClientPoints(CCSPlayerController player, CHANGE_MODE mode, int amount, string reason)
		{
			if (!player.IsValidPlayer())
				return;

			if (!IsPointsAllowed() || amount == 0)
				return;

			if (!PlayerSummaries.ContainsPlayer(player))
				LoadPlayerData(player);

			switch (mode)
			{
				case CHANGE_MODE.SET:
					{
						player.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Gold}{PlayerSummaries[player].Points}[={amount} {reason}]");
						PlayerSummaries[player].Points = amount;
						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = {amount} WHERE `steam_id` = {player.SteamID};");
						break;
					}
				case CHANGE_MODE.GIVE:
					{
						player.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[player].Points}[+{amount} {reason}]");
						PlayerSummaries[player].Points += amount;
						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` + {amount}) WHERE `steam_id` = {player.SteamID};");
						break;
					}
				case CHANGE_MODE.REMOVE:
					{
						player.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Red}{PlayerSummaries[player].Points}[-{amount} {reason}]");
						PlayerSummaries[player].Points -= amount;

						if (PlayerSummaries[player].Points < 0)
							PlayerSummaries[player].Points = 0;

						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = GREATEST(`points` - {amount}, 0) WHERE `steam_id` = {player.SteamID};");
						break;
					}
				default:
					{
						Log($"Invalid operation at the point modification function: {mode}");
						break;
					}
			}

			string newRank = "None";
			Rank? setRank = null;

			foreach (var kvp in ranks)
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (PlayerSummaries[player].Points >= rank.Exp)
				{
					setRank = rank;
					newRank = level;
				}
				else
					break;
			}

			if (setRank == null || newRank == "None" || newRank == PlayerSummaries[player].Rank)
				return;

			if (CFG.config.ScoreboardRanks)
				player.Clan = newRank;

			Server.PrintToChatAll($" {ChatColors.Red}{CFG.config.ChatPrefix} {ChatColors.Gold}{player.PlayerName} has been {(setRank.Exp > PlayerSummaries[player].RankPoints ? "promoted" : "demoted")} to {newRank}.");

			PlayerSummaries[player].Rank = newRank;
			PlayerSummaries[player].RankPoints = setRank.Exp;

			string modifiedValue = setRank.Color;
			foreach (FieldInfo field in typeof(ChatColors).GetFields())
			{
				string pattern = $"{field.Name}";
				if (setRank.Color.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			PlayerSummaries[player].RankColor = modifiedValue;
		}

		public void CheckNewRank(CCSPlayerController player)
		{
			string newRank = "None";
			Rank? setRank = null;

			foreach (var kvp in ranks)
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (PlayerSummaries[player].Points >= rank.Exp)
				{
					setRank = rank;
					newRank = level;
				}
				else
					break;
			}

			if (setRank == null)
			{
				PlayerSummaries[player].Rank = "None";
				PlayerSummaries[player].RankPoints = 0;
				PlayerSummaries[player].RankColor = $"{ChatColors.Default}";
				return;
			}

			if (CFG.config.ScoreboardRanks)
				player.Clan = newRank;

			PlayerSummaries[player].Rank = newRank;
			PlayerSummaries[player].RankPoints = setRank.Exp;

			string modifiedValue = setRank.Color;
			foreach (FieldInfo field in typeof(ChatColors).GetFields())
			{
				string pattern = $"{field.Name}";
				if (setRank.Color.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			PlayerSummaries[player].RankColor = modifiedValue;
		}

		public void Log(string message)
		{
			string logFile = Path.Join(ModuleDirectory, $"logs-{DateTime.Now.ToString("yyyy-MM-dd")}.txt");
			using (StreamWriter writer = File.AppendText(logFile))
			{
				writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message);
			}

			Console.WriteLine(message);
		}

		public bool IsPointsAllowed()
		{
			return (!K4ryuu.GameRules().WarmupPeriod || CFG.config.WarmupPoints) && (CFG.config.MinPlayers <= Utilities.GetPlayers().Count);
		}

		private void ResetKillStreak(int playerIndex)
		{
			playerKillStreaks[playerIndex] = (1, DateTime.Now);
		}
	}
}