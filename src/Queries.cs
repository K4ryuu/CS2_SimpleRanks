using MySqlConnector;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4ryuuSimpleRanks
{
	internal class Queries
	{
		private static bool DatabaseConnected = DatabaseConnection();
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
		private static bool DatabaseConnection()
		{
			try
			{
				using var connection = Database.GetConnection();
				connection.Open();
				CreateTable(connection);
				connection.Close();
				return true;
			}
			catch (Exception ex)
			{
				LogError("Database connection error: " + ex.Message);
				return false;
			}
		}

		public static void InsertUser(string steamId)
		{
			if (!DatabaseConnected)
			{
				return;
			}

			try
			{
				using var connection = Database.GetConnection();

				using var command = connection.CreateCommand();
				command.CommandText = "INSERT INTO `k4ranks` (`steam_id`, `points`) SELECT @steamId, 0 FROM DUAL WHERE NOT EXISTS(SELECT `steam_id` FROM `k4ranks` WHERE `steam_id` = @steamId) LIMIT 1; ";
				command.Parameters.AddWithValue("@steamId", steamId);

				connection.Open();
				command.ExecuteNonQuery();
				connection.Close();
			}
			catch (Exception ex)
			{
				LogError("Error while inserting user: " + ex.Message);
			}
		}

		public static void AddPoints(CCSPlayerController playerController, int points, Dictionary<string, Rank> ranks)
		{
			try
			{
				using var connection = Database.GetConnection();

				using var command = connection.CreateCommand();

				command.CommandText = $"UPDATE `k4ranks` SET `points` = `points` + @points WHERE `steam_id` = @steamId;";

				string steamID = playerController.SteamID.ToString();

				command.Parameters.AddWithValue("@steamId", steamID);
				command.Parameters.AddWithValue("@points", points);

				connection.Open();
				command.ExecuteNonQuery();
				connection.Close();

				UpdatePlayerRank(playerController, ranks);
			}
			catch (Exception ex)
			{
				LogError("Error while adding points: " + ex.Message);
			}
		}

		public static void RemovePoints(CCSPlayerController playerController, int points, Dictionary<string, Rank> ranks)
		{
			try
			{
				using var connection = Database.GetConnection();

				using var command = connection.CreateCommand();

				// Use a SQL CASE statement to ensure points don't go below 0
				command.CommandText = "UPDATE `k4ranks` SET `points` = CASE " +
									"WHEN (`points` - @points) < 0 THEN 0 " +
									"ELSE (`points` - @points) END " +
									"WHERE `steam_id` = @steamId";

				string steamID = playerController.SteamID.ToString();

				command.Parameters.AddWithValue("@steamId", steamID);
				command.Parameters.AddWithValue("@points", points);

				connection.Open();
				command.ExecuteNonQuery();
				connection.Close();

				UpdatePlayerRank(playerController, ranks);
			}
			catch (Exception ex)
			{
				LogError("Error while removing points: " + ex.Message);
			}
		}

		private static void UpdatePlayerRank(CCSPlayerController playerController, Dictionary<string, Rank> ranks)
		{
			// Retrieve the current points of the player
			int playerPoints = 0;
			string currentRank = "None";

			try
			{
				using var connection = Database.GetConnection();

				using var command = connection.CreateCommand();
				command.CommandText = "SELECT `points`, `rank` FROM `k4ranks` WHERE `steam_id` = @steamId;";
				command.Parameters.AddWithValue("@steamId", playerController.SteamID.ToString());

				connection.Open();

				using var reader = command.ExecuteReader();
				while (reader.Read())
				{
					playerPoints = reader.GetInt32("points");
					currentRank = reader.GetString("rank");

					if (currentRank == null)
						currentRank = "None";
				}
			}
			catch (Exception ex)
			{
				LogError("Error while getting groupped points: " + ex.Message);
			}

			// Determine the new rank based on the updated points
			string newRank = currentRank;

			foreach (var kvp in ranks)
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

			if (newRank != currentRank)
			{
				int newExp = ranks.ContainsKey(newRank) ? ranks[newRank].Exp : 0;
				int oldExp = ranks.ContainsKey(currentRank) ? ranks[currentRank].Exp : 0;


				if (newExp != oldExp)
				{
					string change = newExp > oldExp ? "promoted" : "demoted";
					Server.PrintToChatAll($" {ChatColors.LightRed}{CFG.config.ChatPrefix} {ChatColors.Gold}{playerController.PlayerName} has been {change} to {newRank}.");

				}

				try
				{
					using var connection = Database.GetConnection();
					using var command = connection.CreateCommand();

					command.CommandText = "UPDATE `k4ranks` SET `rank` = @rank WHERE `steam_id` = @steamId";
					command.Parameters.AddWithValue("@steamId", playerController.SteamID.ToString());
					command.Parameters.AddWithValue("@rank", newRank);

					connection.Open();
					command.ExecuteNonQuery();
					connection.Close();

				}
				catch (Exception ex)
				{
					LogError("Error while updating player rank: " + ex.Message);
				}
			}
		}

		public static (char ColorCode, string RankName) GetRankInfo(CCSPlayerController playerController, Dictionary<string, Rank> ranks)
		{
			string suitableRank = "None";
			string colorCode = "Default";

			try
			{
				using var connection = Database.GetConnection();

				using var command = connection.CreateCommand();
				command.CommandText = "SELECT `rank`, `points` FROM `k4ranks` WHERE `steam_id` = @steamId;";
				command.Parameters.AddWithValue("@steamId", playerController.SteamID.ToString());

				connection.Open();

				using var reader = command.ExecuteReader();
				while (reader.Read())
				{
					string rankName = reader.IsDBNull(reader.GetOrdinal("rank")) ? "None" : reader.GetString("rank");
					int points = reader.GetInt32(reader.GetOrdinal("points"));

					if (ranks.ContainsKey(rankName))
						return (colorMapping[ranks[rankName].Color.ToLower()], rankName);

					UpdatePlayerRank(playerController, ranks);

					foreach (var rank in ranks)
					{
						if (points >= rank.Value.Exp)
						{
							suitableRank = rank.Key;
							colorCode = rank.Value.Color;
						}
						else
							break;
					}
				}
			}
			catch (Exception ex)
			{
				LogError("Error while getting or updating rank info: " + ex.Message);
			}

			return (colorMapping[colorCode.ToLower()], suitableRank); // Default color and rank name if there's an error
		}

		private static void CreateTable(MySqlConnection connection)
		{
			try
			{
				using var command = connection.CreateCommand();
				command.CommandText = @"CREATE TABLE IF NOT EXISTS `k4ranks` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steam_id` VARCHAR(255) NOT NULL, `rank` VARCHAR(255) DEFAULT NULL, `points` INT NOT NULL, UNIQUE (`steam_id`));";

				command.ExecuteNonQuery();
				command.Dispose();
			}
			catch (Exception ex)
			{
				LogError("Error while creating table: " + ex.Message);
			}
		}

		public static int GetPoints(string steamId)
		{
			try
			{
				using var connection = Database.GetConnection();

				using var command = connection.CreateCommand();
				command.CommandText = "SELECT `points` FROM `k4ranks` WHERE `steam_id` = @steamId;";
				command.Parameters.AddWithValue("@steamId", steamId);

				connection.Open();

				using var reader = command.ExecuteReader();
				while (reader.Read())
				{
					return reader.GetInt32("points");
				}
			}
			catch (Exception ex)
			{
				LogError("Error while getting points: " + ex.Message);
			}

			return 0;
		}

		private static void LogError(string errorMessage)
		{
			string text = "Error: " + errorMessage;
			Console.WriteLine(text);
			Server.PrintToChatAll(text);
		}
	}
}
