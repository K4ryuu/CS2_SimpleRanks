using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4ryuuSimpleRanks
{
	public partial class SimpleRanks
	{
		private void SetupGameEvents()
		{
			RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValidPlayer())
					PlayerSummaries.RemovePlayer(playerController);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				playerKillStreaks[playerController.UserId ?? 0] = (0, DateTime.MinValue);

				if (playerController.IsValidPlayer())
					LoadPlayerData(playerController);

				return HookResult.Continue;
			});
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
			RegisterEventHandler<EventRoundStart>((@event, info) =>
			{
				if (!IsPointsAllowed())
					return HookResult.Continue;

				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController player in players)
				{
					if (!player.IsValid || player.IsBot)
						continue;

					if (!PlayerSummaries.ContainsPlayer(player!))
						LoadPlayerData(player!);

					if (CFG.config.DisableSpawnMessage || PlayerSummaries[player].SpawnedThisRound)
						continue;

					player.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.Green}The server is using {ChatColors.Gold}SimpleRanks {ChatColors.Green}plugin. Type {ChatColors.Red}!rank {ChatColors.Green}to get more information!");

					PlayerSummaries[player].SpawnedThisRound = true;
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundEnd>((@event, info) =>
			{
				CsTeam winnerTeam = (CsTeam)@event.Winner;

				if (!IsPointsAllowed())
					return HookResult.Continue;

				List<CCSPlayerController> players = Utilities.GetPlayers();
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

				if (!victimController.IsBot && (CFG.config.PointsForBots || !killerController.IsBot))
				{
					if (victimController.UserId == killerController.UserId)
					{
						ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, CFG.config.SuicidePoints, "Suicide");
					}
					else
					{
						ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, CFG.config.DeathPoints, "Dying");
					}

					if (CFG.config.ScoreboardScoreSync)
						victimController.Score = PlayerSummaries[victimController].Points;
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
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} [+{CFG.config.KillPoints} Kill]");
							PlayerSummaries[killerController].Points += CFG.config.KillPoints;
						}

						if (@event.Headshot && CFG.config.HeadshotPoints > 0)
						{
							pointChange += CFG.config.HeadshotPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Olive}[+{CFG.config.HeadshotPoints} Headshot]");
							PlayerSummaries[killerController].Points += CFG.config.HeadshotPoints;
						}

						int penetrateCount = @event.Penetrated;
						if (penetrateCount > 0 && CFG.config.PenetratedPoints > 0)
						{
							int calculatedPoints = @event.Penetrated * CFG.config.PenetratedPoints;
							pointChange += calculatedPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightBlue}[+{calculatedPoints} Penetration]");
							PlayerSummaries[killerController].Points += calculatedPoints;
						}

						if (@event.Noscope && CFG.config.NoScopePoints > 0)
						{
							pointChange += CFG.config.NoScopePoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightRed}[+{CFG.config.NoScopePoints} NoScope]");
							PlayerSummaries[killerController].Points += CFG.config.NoScopePoints;
						}

						if (@event.Thrusmoke && CFG.config.ThrusmokePoints > 0)
						{
							pointChange += CFG.config.ThrusmokePoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightRed}[+{CFG.config.ThrusmokePoints} ThruSmoke]");
							PlayerSummaries[killerController].Points += CFG.config.ThrusmokePoints;
						}

						if (@event.Attackerblind && CFG.config.BlindKillPoints > 0)
						{
							pointChange += CFG.config.BlindKillPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightRed}[+{CFG.config.BlindKillPoints} Blind Kill]");
							PlayerSummaries[killerController].Points += CFG.config.BlindKillPoints;
						}

						if (@event.Distance >= CFG.config.LongDistance && CFG.config.LongDistanceKillPoints > 0)
						{
							pointChange += CFG.config.LongDistanceKillPoints;
							killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{CFG.config.LongDistanceKillPoints} Long Distance]");
							PlayerSummaries[killerController].Points += CFG.config.LongDistanceKillPoints;
						}

						string lowerCaseWeaponName = @event.Weapon.ToLower();

						switch (lowerCaseWeaponName)
						{
							case var _ when lowerCaseWeaponName.Contains("hegrenade") || lowerCaseWeaponName.Contains("tagrenade") || lowerCaseWeaponName.Contains("firebomb") || lowerCaseWeaponName.Contains("molotov") || lowerCaseWeaponName.Contains("incgrenade") || lowerCaseWeaponName.Contains("flashbang") || lowerCaseWeaponName.Contains("smokegrenade") || lowerCaseWeaponName.Contains("frag") || lowerCaseWeaponName.Contains("bumpmine"):
								{
									if (CFG.config.GrenadeKillPoints > 0)
									{
										pointChange += CFG.config.GrenadeKillPoints;
										killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{CFG.config.GrenadeKillPoints} Grenade Kill]");
										PlayerSummaries[killerController].Points += CFG.config.GrenadeKillPoints;
									}
									break;
								}
							case var _ when lowerCaseWeaponName.Contains("cord") || lowerCaseWeaponName.Contains("bowie") || lowerCaseWeaponName.Contains("butterfly") || lowerCaseWeaponName.Contains("karambit") || lowerCaseWeaponName.Contains("skeleton") || lowerCaseWeaponName.Contains("m9_bayonet") || lowerCaseWeaponName.Contains("bayonet") || lowerCaseWeaponName.Contains("t") || lowerCaseWeaponName.Contains("knifegg") || lowerCaseWeaponName.Contains("stiletto") || lowerCaseWeaponName.Contains("ursus") || lowerCaseWeaponName.Contains("tactical") || lowerCaseWeaponName.Contains("push") || lowerCaseWeaponName.Contains("widowmaker") || lowerCaseWeaponName.Contains("outdoor") || lowerCaseWeaponName.Contains("canis"):
								{
									if (CFG.config.KnifeKillPoints > 0)
									{
										pointChange += CFG.config.KnifeKillPoints;
										killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{CFG.config.KnifeKillPoints} Knife Kill]");
										PlayerSummaries[killerController].Points += CFG.config.KnifeKillPoints;
									}
									break;
								}
							case "taser":
								{
									if (CFG.config.TaserKillPoints > 0)
									{
										pointChange += CFG.config.TaserKillPoints;
										killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{CFG.config.TaserKillPoints} Taser Kill]");
										PlayerSummaries[killerController].Points += CFG.config.TaserKillPoints;
									}
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
										killStreakMessage = "DoubleK ill";
										break;
									case 3:
										points = CFG.config.TripleKillPoints;
										killStreakMessage = "Triple Kill";
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
										killStreakMessage = "Mega Kill";
										break;
									case 7:
										points = CFG.config.OwnagePoints;
										killStreakMessage = "Ownage";
										break;
									case 8:
										points = CFG.config.UltraKillPoints;
										killStreakMessage = "Ultra Kill";
										break;
									case 9:
										points = CFG.config.KillingSpreePoints;
										killStreakMessage = "Killing Spree";
										break;
									case 10:
										points = CFG.config.MonsterKillPoints;
										killStreakMessage = "Monster Kill";
										break;
									case 11:
										points = CFG.config.UnstoppablePoints;
										killStreakMessage = "Unstoppable";
										break;
									case 12:
										points = CFG.config.GodLikePoints;
										killStreakMessage = "God Like";
										break;
									default:
										// Handle other cases or reset the kill streak
										ResetKillStreak(attackerIndex);
										break;
								}

								if (points > 0)
								{
									pointChange += points;
									killerController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightYellow}[+{points} {killStreakMessage}]");
									PlayerSummaries[killerController].Points += points;
								}
							}
							else
							{
								// No kill streak, reset the count
								ResetKillStreak(attackerIndex);
							}
						}

						if (CFG.config.ScoreboardScoreSync)
							killerController.Score = PlayerSummaries[killerController].Points;

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
						assisterController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[assisterController].Points} [+{CFG.config.AssistPoints} Assist]");
						PlayerSummaries[assisterController].Points += CFG.config.AssistPoints;
					}

					if (@event.Assistedflash && CFG.config.AsssistFlashPoints > 0)
					{
						pointChange += CFG.config.AsssistFlashPoints;
						assisterController.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[assisterController].Points} [+{CFG.config.AsssistFlashPoints} Flash Assist]");
						PlayerSummaries[assisterController].Points += CFG.config.AsssistFlashPoints;
					}

					if (CFG.config.ScoreboardScoreSync)
						assisterController.Score = PlayerSummaries[assisterController].Points;

					MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` + {pointChange}) WHERE `steam_id` = {assisterController.SteamID};");
				}

				return HookResult.Continue;
			});
		}
	}
}