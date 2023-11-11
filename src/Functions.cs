using Newtonsoft.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Nexd.MySQL;
using System.Reflection;
using MySqlConnector;

namespace K4ryuuSimpleRanks
{
	public partial class SimpleRanks
	{
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

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			MySqlQueryValue values = new MySqlQueryValue()
										.Add("name", escapedName)
										.Add("steam_id", player.SteamID.ToString());

			MySql!.Table("k4ranks").InsertIfNotExist(values, $"`name` = '{escapedName}'");

			MySqlQueryResult result = MySql!.Table("k4ranks").Where(MySqlQueryCondition.New("steam_id", "=", player.SteamID.ToString())).Select("points");

			PlayerSummaries[player].Points = result.Rows > 0 ? result.Get<int>(0, "points") : 0;

			if (CFG.config.ScoreboardScoreSync)
				player.Score = PlayerSummaries[player].Points;

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
						player.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Gold}{PlayerSummaries[player].Points} [={amount} {reason}]");
						PlayerSummaries[player].Points = amount;
						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = {amount} WHERE `steam_id` = {player.SteamID};");
						break;
					}
				case CHANGE_MODE.GIVE:
					{
						player.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[player].Points} [+{amount} {reason}]");
						PlayerSummaries[player].Points += amount;
						MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` + {amount}) WHERE `steam_id` = {player.SteamID};");
						break;
					}
				case CHANGE_MODE.REMOVE:
					{
						player.PrintToChat($" {CFG.config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Red}{PlayerSummaries[player].Points} [-{amount} {reason}]");
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

			if (CFG.config.ScoreboardScoreSync)
				player.Score = PlayerSummaries[player].Points;

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
			if (CFG.config.ScoreboardScoreSync)
				player.Score = PlayerSummaries[player].Points;

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