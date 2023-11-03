using CounterStrikeSharp.API.Core;
internal static class CCSPlayerControllerEx
{
	internal static bool IsValidPlayer(this CCSPlayerController controller)
	{
		return controller != null && controller.IsValid && !controller.IsBot;
	}
}