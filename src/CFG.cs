using CounterStrikeSharp.API.Modules.Utils;
using System.Reflection;
using System.Text.Json;

namespace K4ryuuSimpleRanks;

internal class CFG
{
	public static Config config = new();

	public void CheckConfig(string moduleDirectory)
	{
		string path = Path.Join(moduleDirectory, "config.json");

		if (!File.Exists(path))
		{
			CreateAndWriteFile(path);
		}

		using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
		using (StreamReader sr = new StreamReader(fs))
		{
			// Deserialize the JSON from the file and load the configuration.
			config = JsonSerializer.Deserialize<Config>(sr.ReadToEnd())!;
		}

		if (config != null && config.ChatPrefix != null)
			config.ChatPrefix = ModifyColorValue(config.ChatPrefix);
	}

	private static void CreateAndWriteFile(string path)
	{

		using (FileStream fs = File.Create(path))
		{
			// File is created, and fs will automatically be disposed when the using block exits.
		}

		Console.WriteLine($"File created: {File.Exists(path)}");

		Config config = new Config
		{
			ChatPrefix = "{Blue}[SimpleRanks]",
			DisableSpawnMessage = false,
			MinPlayers = 4,
			WarmupPoints = false,
			FFAMode = false,
			ScoreboardRanks = true,
			DatabaseHost = "localhost",
			DatabasePort = 3306,
			DatabaseUser = "root",
			DatabasePassword = "password",
			DatabaseName = "database",
			PointsForBots = false,
			DeathPoints = 5,
			KillPoints = 5,
			HeadshotPoints = 1,
			PenetratedPoints = 1,
			NoScopePoints = 10,
			ThrusmokePoints = 10,
			BlindKillPoints = 3,
			TeamKillPoints = 7,
			SuicidePoints = 6,
			AssistPoints = 2,
			AsssistFlashPoints = 3,
			PlantPoints = 10,
			RoundWinPoints = 1,
			RoundLosePoints = 1,
			MVPPoints = 3,
			DefusePoints = 4,
			BombDropPoints = 1,
			BombPickupPoints = 1,
			HostageHurtPoints = 1,
			HostageKillPoints = 10,
			HostageRescuePoints = 10,
			AcePoints = 20,
			LongDistanceKillPoints = 4,
			LongDistance = 100,
			SecondsBetweenKills = 5,
			DoubleKillPoints = 2,
			TripleKillPoints = 3,
			DominationPoints = 4,
			RampagePoints = 5,
			MegaKillPoints = 6,
			OwnagePoints = 7,
			UltraKillPoints = 8,
			KillingSpreePoints = 9,
			MonsterKillPoints = 10,
			UnstoppablePoints = 11,
			GodLikePoints = 12,
			GrenadeKillPoints = 25,
			TaserKillPoints = 15,
			KnifeKillPoints = 10
		};

		// Serialize the config object to JSON and write it to the file.
		string jsonConfig = JsonSerializer.Serialize(config, new JsonSerializerOptions()
		{
			WriteIndented = true
		});
		File.WriteAllText(path, jsonConfig);
	}

	// Essential method for replacing chat colors from the config file, the method can be used for other things as well.
	private string ModifyColorValue(string msg)
	{
		if (msg.Contains('{'))
		{
			string modifiedValue = msg;
			foreach (FieldInfo field in typeof(ChatColors).GetFields())
			{
				string pattern = $"{{{field.Name}}}";
				if (msg.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}
			return modifiedValue;
		}

		return string.IsNullOrEmpty(msg) ? "[SimpleRanks]" : msg;
	}
}

internal class Config
{
	public string? ChatPrefix { get; set; }
	public bool DisableSpawnMessage { get; set; }
	public bool WarmupPoints { get; set; }
	public int MinPlayers { get; set; }
	public bool FFAMode { get; set; }
	public bool ScoreboardRanks { get; set; }
	public string? DatabaseHost { get; set; }
	public int DatabasePort { get; set; }
	public string? DatabaseUser { get; set; }
	public string? DatabasePassword { get; set; }
	public string? DatabaseName { get; set; }
	public bool PointsForBots { get; set; }
	public int DeathPoints { get; set; }
	public int KillPoints { get; set; }
	public int HeadshotPoints { get; set; }
	public int PenetratedPoints { get; set; }
	public int NoScopePoints { get; set; }
	public int ThrusmokePoints { get; set; }
	public int BlindKillPoints { get; set; }
	public int TeamKillPoints { get; set; }
	public int SuicidePoints { get; set; }
	public int AssistPoints { get; set; }
	public int AsssistFlashPoints { get; set; }
	public int PlantPoints { get; set; }
	public int RoundWinPoints { get; set; }
	public int RoundLosePoints { get; set; }
	public int MVPPoints { get; set; }
	public int DefusePoints { get; set; }
	public int BombDropPoints { get; set; }
	public int BombPickupPoints { get; set; }
	public int HostageHurtPoints { get; set; }
	public int HostageKillPoints { get; set; }
	public int HostageRescuePoints { get; set; }
	public int AcePoints { get; set; }
	public int LongDistanceKillPoints { get; set; }
	public int LongDistance { get; set; }
	public int SecondsBetweenKills { get; set; }
	public int DoubleKillPoints { get; set; }
	public int TripleKillPoints { get; set; }
	public int DominationPoints { get; set; }
	public int RampagePoints { get; set; }
	public int MegaKillPoints { get; set; }
	public int OwnagePoints { get; set; }
	public int UltraKillPoints { get; set; }
	public int KillingSpreePoints { get; set; }
	public int MonsterKillPoints { get; set; }
	public int UnstoppablePoints { get; set; }
	public int GodLikePoints { get; set; }
	public int GrenadeKillPoints { get; set; }
	public int TaserKillPoints { get; set; }
	public int KnifeKillPoints { get; set; }
}