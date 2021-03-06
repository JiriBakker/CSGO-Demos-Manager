﻿using DemoInfo;
using Newtonsoft.Json;

namespace CSGO_Demos_Manager.Models.Events
{
	public class KillEvent : BaseEvent
	{
		[JsonProperty("killer_steamid")]
		public long KillerSteamId { get; set; }

		[JsonProperty("killer_entity_id")]
		public int KillerEntityId { get; set; }

		[JsonProperty("killed_entity_id")]
		public int KilledEntityId { get; set; }

		[JsonProperty("killed_steamid")]
		public long KilledSteamId { get; set; }

		[JsonProperty("assister_steamid")]
		public long AssisterSteamId { get; set; }

		[JsonProperty("weapon")]
		public Weapon Weapon { get; set; }

		[JsonProperty("heatmap_point")]
		public KillHeatmapPoint Point { get; set; }

		[JsonProperty("killer_vel_x")]
		public float KillerVelocityX { get; set; }

		[JsonProperty("killer_vel_y")]
		public float KillerVelocityY { get; set; }

		[JsonProperty("killer_vel_z")]
		public float KillerVelocityZ { get; set; }

		[JsonProperty("killer_side")]
		public Team KillerSide { get; set; }

		[JsonProperty("killed_side")]
		public Team KilledSide { get; set; }

		[JsonProperty("killer_name")]
		public string KillerName { get; set; }

		[JsonProperty("killed_name")]
		public string KilledName { get; set; }

		[JsonProperty("assister_name")]
		public string AssisterName { get; set; }

		[JsonProperty("round_number")]
		public int RoundNumber { get; set; }

		[JsonProperty("killer_crouching")]
		public bool IsKillerCrouching { get; set; }

		[JsonProperty("is_trade_kill")]
		public bool IsTradeKill { get; set; }

		[JsonIgnore]
		public override string Message => KillerName + " killed " + KilledName + " with " + Weapon.Name;

		public KillEvent(int tick, float seconds) : base(tick, seconds) { }
	}
}
