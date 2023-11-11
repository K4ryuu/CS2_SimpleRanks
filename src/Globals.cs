using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Nexd.MySQL;

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

	public partial class SimpleRanks
	{
		MySqlDb? MySql = null;
		private Dictionary<int, (int killStreak, DateTime lastKillTime)> playerKillStreaks = new Dictionary<int, (int, DateTime)>();
		public static Dictionary<string, Rank> ranks = new Dictionary<string, Rank>();
		internal static PlayerCache<User> PlayerSummaries = new PlayerCache<User>();

	}
}