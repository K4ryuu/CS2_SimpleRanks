using MySqlConnector;
using System.Data;

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

		private static MySqlConnection? globalConnection;

		public static MySqlConnection GetConnection()
		{
			// If the global connection is null or closed, create a new connection and open it.
			if (globalConnection == null || globalConnection.State == ConnectionState.Closed)
			{
				globalConnection = new MySqlConnection(connection.ConnectionString);
				globalConnection.Open();
			}

			return globalConnection;
		}
	}
}
