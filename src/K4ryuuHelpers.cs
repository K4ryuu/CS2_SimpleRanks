using Newtonsoft.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
internal static class CCSPlayerControllerEx
{
	internal static bool IsValidPlayer(this CCSPlayerController controller)
	{
		return controller != null && controller.IsValid && !controller.IsBot;
	}
}

internal static class K4ryuu
{
	internal static CCSGameRules GameRules()
	{
		return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
	}
}