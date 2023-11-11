using Newtonsoft.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Nexd.MySQL;
using CounterStrikeSharp.API.Modules.Admin;

namespace K4ryuuSimpleRanks
{
	public partial class SimpleRanks
	{
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
	}
}