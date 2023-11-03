using MySqlConnector;

namespace K4ryuuSimpleRanks
{
	internal class Database
	{
		private static readonly MySqlConnectionStringBuilder connection = new()
		{
			Server = CFG.config.DatabaseHost,
			Port = CFG.config.DatabasePort,
			UserID = CFG.config.DatabaseUser,
			Password = CFG.config.DatabasePassword,
			Database = CFG.config.DatabaseName
		};

		public static MySqlConnection GetConnection()
		{
			return new MySqlConnection(connection.ConnectionString);
		}
	}
}
