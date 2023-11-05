using System;
using System.Collections.Generic;
using MySqlConnector;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4ryuuSimpleRanks
{
	internal class Queries
	{
		private static Dictionary<string, char> colorMapping = new Dictionary<string, char>
		{
			{ "default", '\u0001' },
			{ "white", '\u0001' },
			{ "darkred", '\u0002' },
			{ "green", '\u0004' },
			{ "lightyellow", '\u0003' },
			{ "lightblue", '\u0003' },
			{ "olive", '\u0005' },
			{ "lime", '\u0006' },
			{ "red", '\a' },
			{ "purple", '\u0003' },
			{ "grey", '\b' },
			{ "yellow", '\t' },
			{ "gold", '\u0010' },
			{ "silver", '\n' },
			{ "blue", '\v' },
			{ "darkblue", '\f' },
			{ "bluegrey", '\r' },
			{ "magenta", '\u000e' },
			{ "lightred", '\u000f' }
		};

		public static async Task CreateTable()
		{
			using MySqlConnection connection = Database.GetConnection();
			try
			{
				await connection.OpenAsync();
				await ExecuteNonQueryAsync(@"CREATE TABLE IF NOT EXISTS `k4ranks` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steam_id` VARCHAR(255) NOT NULL, `name` VARCHAR(255) DEFAULT NULL, `rank` VARCHAR(255) DEFAULT NULL, `points` INT NOT NULL, UNIQUE (`steam_id`));");
			}
			catch (MySqlException ex)
			{
				LogError("Error executing query: " + ex.Message);
			}
			finally
			{
				connection.Close();
			}
		}

		public static async Task InsertUserAsync(CCSPlayerController playerController)
		{
			using MySqlConnection connection = Database.GetConnection();
			try
			{
				await connection.OpenAsync();

				await ExecuteNonQueryAsync("INSERT INTO `k4ranks` (`steam_id`, `points`, `name`) " +
									"VALUES (@steamId, 0, @playerName) ON DUPLICATE KEY UPDATE `name` = @playerName;",
									new MySqlParameter("@steamId", playerController.SteamID.ToString()),
									new MySqlParameter("@playerName", playerController.PlayerName));
			}
			catch (MySqlException ex)
			{
				LogError("Error executing query: " + ex.Message);
			}
			finally
			{
				connection.Close();
			}
		}

		public static async Task<List<(string PlayerName, int Points, string Rank)>?> GetTopPlayersAsync()
		{
			List<(string PlayerName, int Points, string Rank)> topPlayers = new List<(string PlayerName, int Points, string Rank)>();

			using MySqlConnection connection = Database.GetConnection();
			try
			{
				await connection.OpenAsync();

				using MySqlCommand command = connection.CreateCommand();
				command.CommandText = "SELECT `points`, `name`, `rank` FROM `k4ranks` ORDER BY `points` DESC LIMIT 5;";

				using MySqlDataReader reader = await command.ExecuteReaderAsync();

				while (await reader.ReadAsync())
				{
					string name = reader.IsDBNull(reader.GetOrdinal("name")) ? "Unknown" : reader.GetString("name");
					int points = reader.IsDBNull(reader.GetOrdinal("points")) ? 0 : reader.GetInt32("points");
					string rank = reader.IsDBNull(reader.GetOrdinal("rank")) ? "None" : reader.GetString("rank");

					topPlayers.Add((name, points, rank));
				}
			}
			catch (MySqlException ex)
			{
				LogError("Error executing query: " + ex.Message);
			}
			finally
			{
				connection.Close();
			}

			return topPlayers;
		}

		public static async Task AddPointsAsync(CCSPlayerController playerController, int points)
		{
			using MySqlConnection connection = Database.GetConnection();
			try
			{
				await connection.OpenAsync();

				string steamId = playerController.SteamID.ToString();

				await ExecuteNonQueryAsync("UPDATE `k4ranks` SET `points` = `points` + @points WHERE `steam_id` = @steamId;",
					new MySqlParameter("@steamId", steamId),
					new MySqlParameter("@points", points));

				await UpdatePlayerRankAsync(playerController);
			}
			catch (MySqlException ex)
			{
				LogError("Error executing query: " + ex.Message);
			}
			finally
			{
				connection.Close();
			}
		}

		public static async Task RemovePointsAsync(CCSPlayerController playerController, int points)
		{
			using MySqlConnection connection = Database.GetConnection();
			try
			{
				await connection.OpenAsync();

				string steamId = playerController.SteamID.ToString();

				await ExecuteNonQueryAsync("UPDATE `k4ranks` SET `points` = CASE " +
					"WHEN (`points` - @points) < 0 THEN 0 " +
					"ELSE (`points` - @points) END " +
					"WHERE `steam_id` = @steamId;",
					new MySqlParameter("@steamId", steamId),
					new MySqlParameter("@points", points));

				await UpdatePlayerRankAsync(playerController);
			}
			catch (MySqlException ex)
			{
				LogError("Error executing query: " + ex.Message);
			}
			finally
			{
				connection.Close();
			}
		}
		private static async Task UpdatePlayerRankAsync(CCSPlayerController playerController)
		{
			string steamId = playerController.SteamID.ToString();

			if (playerController.IsValidPlayer())
				await InsertUserAsync(playerController);

			using MySqlConnection connection = Database.GetConnection();
			try
			{
				await connection.OpenAsync();

				using MySqlCommand command = connection.CreateCommand();
				command.CommandText = "SELECT `points`, `rank` FROM `k4ranks` WHERE `steam_id` = @steamId;";
				command.Parameters.AddWithValue("@steamId", steamId);

				using MySqlDataReader reader = await command.ExecuteReaderAsync();

				int playerPoints = 0;
				string currentRank = "None";

				while (await reader.ReadAsync())
				{
					playerPoints = reader.GetInt32("points");
					currentRank = reader.IsDBNull(reader.GetOrdinal("rank")) ? "None" : reader.GetString("rank");
				}

				string newRank = DetermineNewRankAsync(playerPoints);

				if (newRank != currentRank)
				{
					await UpdatePlayerRankInDatabaseAsync(playerController, newRank);
				}
			}
			catch (MySqlException ex)
			{
				LogError("Error executing query: " + ex.Message);
			}
			finally
			{
				connection.Close();
			}
		}

		private static string DetermineNewRankAsync(int playerPoints)
		{
			string newRank = "None";

			foreach (var kvp in SimpleRanks.ranks)
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (playerPoints >= rank.Exp)
				{
					newRank = level;
				}
				else
					break;
			}

			return newRank;
		}

		private static async Task UpdatePlayerRankInDatabaseAsync(CCSPlayerController playerController, string newRank)
		{
			string steamId = playerController.SteamID.ToString();

			string change = await DetermineRankChange(playerController, newRank);

			using MySqlConnection connection = Database.GetConnection();
			try
			{
				await connection.OpenAsync();

				await ExecuteNonQueryAsync("UPDATE `k4ranks` SET `rank` = @rank WHERE `steam_id` = @steamId;",
					new MySqlParameter("@steamId", steamId),
					new MySqlParameter("@rank", newRank));

				if (!string.IsNullOrEmpty(change))
				{
					Server.PrintToChatAll($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Gold}{playerController.PlayerName} has been {change} to {newRank}.");
				}
			}
			catch (MySqlException ex)
			{
				LogError("Error executing query: " + ex.Message);
			}
			finally
			{
				connection.Close();
			}
		}

		public static async Task<(char ColorCode, string RankName)> GetRankInfoAsync(CCSPlayerController playerController)
		{
			string steamId = playerController.SteamID.ToString();
			string suitableRank = "None";
			string colorCode = "Default";

			using MySqlConnection connection = Database.GetConnection();
			try
			{
				await connection.OpenAsync();

				using MySqlCommand command = connection.CreateCommand();

				command.CommandText = "SELECT `rank`, `points` FROM `k4ranks` WHERE `steam_id` = @steamId;";
				command.Parameters.AddWithValue("@steamId", steamId);

				using MySqlDataReader reader = await command.ExecuteReaderAsync();

				while (await reader.ReadAsync())
				{
					string rankName = reader.IsDBNull(reader.GetOrdinal("rank")) ? "None" : reader.GetString("rank");
					int points = reader.GetInt32(reader.GetOrdinal("points"));

					if (SimpleRanks.ranks.ContainsKey(rankName))
					{
						return (colorMapping[SimpleRanks.ranks[rankName].Color.ToLower()], rankName);
					}

					suitableRank = DetermineNewRankAsync(points);
					colorCode = SimpleRanks.ranks.ContainsKey(suitableRank) ? SimpleRanks.ranks[suitableRank].Color : "Default";
				}
			}
			catch (MySqlException ex)
			{
				LogError("Error executing query: " + ex.Message);
			}
			finally
			{
				connection.Close();
			}

			return (colorMapping[colorCode.ToLower()], suitableRank);
		}

		private static async Task<string> DetermineRankChange(CCSPlayerController playerController, string newRank)
		{
			string steamId = playerController.SteamID.ToString();

			if (!SimpleRanks.ranks.ContainsKey(newRank))
			{
				return "";
			}

			string currentRank = await GetPlayerRankAsync(steamId);

			if (!SimpleRanks.ranks.ContainsKey(currentRank))
			{
				return "";
			}

			int oldPoints = SimpleRanks.ranks[currentRank].Exp;
			int newPoints = SimpleRanks.ranks[newRank].Exp;

			string change = newPoints > oldPoints ? "promoted" : (newPoints < oldPoints ? "demoted" : "");

			return change;
		}

		public static async Task<int> GetPointsAsync(string steamId)
		{
			using MySqlConnection connection = Database.GetConnection();
			try
			{
				await connection.OpenAsync();

				using MySqlCommand command = connection.CreateCommand();
				command.CommandText = "SELECT `points` FROM `k4ranks` WHERE `steam_id` = @steamId;";
				command.Parameters.AddWithValue("@steamId", steamId);

				using MySqlDataReader reader = await command.ExecuteReaderAsync();

				while (await reader.ReadAsync())
				{
					return reader.GetInt32("points");
				}
			}
			catch (MySqlException ex)
			{
				LogError("Error executing query: " + ex.Message);
			}
			finally
			{
				connection.Close();
			}

			return 0;
		}

		public static async Task<string> GetPlayerRankAsync(string steamId)
		{
			using MySqlConnection connection = Database.GetConnection();
			try
			{
				await connection.OpenAsync();

				using MySqlCommand command = connection.CreateCommand();
				command.CommandText = "SELECT `rank` FROM `k4ranks` WHERE `steam_id` = @steamId;";
				command.Parameters.AddWithValue("@steamId", steamId);

				using MySqlDataReader reader = await command.ExecuteReaderAsync();

				while (await reader.ReadAsync())
				{
					string rankName = reader.IsDBNull(reader.GetOrdinal("rank")) ? "None" : reader.GetString("rank");
					return rankName;
				}
			}
			catch (MySqlException ex)
			{
				LogError("Error executing query: " + ex.Message);
			}
			finally
			{
				connection.Close();
			}

			return "None";
		}

		public static async Task ExecuteNonQueryAsync(string query, params MySqlParameter[] parameters)
		{
			using MySqlConnection connection = Database.GetConnection();
			try
			{
				await connection.OpenAsync();

				using MySqlCommand command = connection.CreateCommand();
				command.CommandText = query;

				foreach (var parameter in parameters)
				{
					command.Parameters.Add(parameter);
				}

				await command.ExecuteNonQueryAsync();
			}
			catch (MySqlException ex)
			{
				LogError("Error executing query: " + ex.Message);
			}
			finally
			{
				connection.Close();
			}
		}

		private static void LogError(string errorMessage)
		{
			string text = "Error: " + errorMessage;
			SimpleRanks.Log(text);
		}
	}
}
