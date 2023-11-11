using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Nexd.MySQL;

namespace K4ryuuSimpleRanks
{
	[MinimumApiVersion(28)]
	public partial class SimpleRanks : BasePlugin
	{
		public override string ModuleName => "Simple Ranks";
		public override string ModuleVersion => "v2.1.3";
		public override string ModuleAuthor => "K4ryuu";

		public override void Load(bool hotReload)
		{
			new CFG().CheckConfig(ModuleDirectory);

			MySql = new MySqlDb(CFG.config.DatabaseHost!, CFG.config.DatabaseUser!, CFG.config.DatabasePassword!, CFG.config.DatabaseName!, CFG.config.DatabasePort);
			MySql.ExecuteNonQueryAsync(@"CREATE TABLE IF NOT EXISTS `k4ranks` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steam_id` VARCHAR(255) NOT NULL, `name` VARCHAR(255) DEFAULT NULL, `points` INT NOT NULL DEFAULT 0, UNIQUE (`steam_id`));");

			if (hotReload)
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();

				foreach (CCSPlayerController player in players)
				{
					if (player.IsBot)
						continue;

					LoadPlayerData(player);
				}
			}

			LoadRanksFromConfig();
			SetupGameEvents();

			Log($"{ModuleName} [{ModuleVersion}] by {ModuleAuthor} has been loaded.");
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
	}
}