﻿using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CSGO_Demos_Manager.Models;
using DemoInfo;
using MoreLinq;

namespace CSGO_Demos_Manager.Services.Analyzer
{
	public class CevoAnalyzer : DemoAnalyzer
	{
		// Used to detect when a new side start
		private int _roundStartedCount = 0;

		private bool _isLastRoundFinal;

		// First LO3 has 3 round_start event, the next LO3 have 4, we use this to detect if the first LO3 happened
		private bool _firstLo3Occured;

		public CevoAnalyzer(Demo demo)
		{
			Parser = new DemoParser(File.OpenRead(demo.Path));
			// Reset to have update on UI
			demo.ResetStats();
			Demo = demo;
			RegisterEvents();
		}

		public override async Task<Demo> AnalyzeDemoAsync(CancellationToken token)
		{
			Parser.ParseHeader();

			await Task.Run(() => Parser.ParseToEnd(token), token);

			Application.Current.Dispatcher.Invoke(delegate
			{
				Demo.Winner = Demo.ScoreTeam1 > Demo.ScoreTeam2 ? Demo.TeamCT : Demo.TeamT;
				if (Demo.Players.Any())
				{
					Demo.MostHeadshotPlayer = Demo.Players.OrderByDescending(x => x.HeadshotPercent).First();
					Demo.MostBombPlantedPlayer = Demo.Players.OrderByDescending(x => x.BombPlantedCount).First();
					Demo.MostEntryKillPlayer = Demo.Players.MaxBy(p => p.EntryKills.Count);
				}
				if (Demo.Kills.Any())
				{
					var weapons = Demo.Kills.GroupBy(k => k.Weapon).Select(weap => new
					{
						Weapon = weap.Key,
						Count = weap.Count()
					}).OrderByDescending(w => w.Count);
					if(weapons.Any()) Demo.MostKillingWeapon = weapons.Select(w => w.Weapon).First();
				}
				if (AnalyzePlayersPosition)
				{
					LastPlayersFireEndedMolotov.Clear();
				}
			});

			return Demo;
		}

		protected sealed override void RegisterEvents()
		{
			Parser.MatchStarted += HandleMatchStarted;
			Parser.RoundMVP += HandleRoundMvp;
			Parser.PlayerKilled += HandlePlayerKilled;
			Parser.RoundStart += HandleRoundStart;
			Parser.RoundOfficiallyEnd += HandleRoundOfficiallyEnd;
			Parser.BombPlanted += HandleBombPlanted;
			Parser.BombDefused += HandleBombDefused;
			Parser.BombExploded += HandleBombExploded;
			Parser.TickDone += HandleTickDone;
			Parser.WeaponFired += HandleWeaponFired;
			Parser.RoundEnd += HandleRoundEnd;
			Parser.FlashNadeExploded += HandleFlashNadeExploded;
			Parser.ExplosiveNadeExploded += HandleExplosiveNadeExploded;
			Parser.SmokeNadeStarted += HandleSmokeNadeStarted;
			Parser.FireNadeEnded += HandleFireNadeEnded;
			Parser.SmokeNadeEnded += HandleSmokeNadeEnded;
			Parser.FireNadeStarted += HandleFireNadeStarted;
			Parser.DecoyNadeStarted += HandleDecoyNadeStarted;
			Parser.DecoyNadeEnded += HandleDecoyNadeEnded;
			Parser.LastRoundHalf += HandleLastRoundHalf;
			Parser.WinPanelMatch += HandleWinPanelMatch;
			Parser.RoundFinal += HandleRoundFinal;
			Parser.PlayerHurt += HandlePlayerHurted;
			Parser.PlayerDisconnect += HandlePlayerDisconnect;
		}

		protected void HandleWinPanelMatch(object sender, WinPanelMatchEventArgs e)
		{
			// Add the last round (round_officially_ended isn't raised at the end)
			Application.Current.Dispatcher.Invoke(delegate
			{
				Demo.Rounds.Add(CurrentRound);
			});

			// Add the last OT
			if (IsOvertime)
			{
				Application.Current.Dispatcher.Invoke(delegate
				{
					Demo.Overtimes.Add(CurrentOvertime);
				});
			}

			ProcessPlayersRating();
		}

		protected void HandleLastRoundHalf(object sender, LastRoundHalfEventArgs e)
		{
			if (!IsMatchStarted) return;
			IsLastRoundHalf = true;
		}

		protected override void HandleMatchStarted(object sender, MatchStartedEventArgs e)
		{
			AddTeams();
		}

		protected override void HandleRoundStart(object sender, RoundStartedEventArgs e)
		{
			_roundStartedCount++;

			// Beginning of the first round of the game
			if (Parser.TScore == 0 && Parser.CTScore == 0) AddTeams();

			// Check for the first LO3
			if (!IsOvertime && !_firstLo3Occured && !IsHalfMatch && _roundStartedCount >= 3)
			{
				_firstLo3Occured = true;
				IsMatchStarted = true;
			}

			// Check for the LO3 occured after the first LO3 (there are 4 round_start events)
			if (_firstLo3Occured && _roundStartedCount >= 4) IsMatchStarted = true;

			if (_isLastRoundFinal) _isLastRoundFinal = false;

			if (!IsMatchStarted) return;

			CreateNewRound();
		}

		protected new void HandleRoundEnd(object sender, RoundEndedEventArgs e)
		{
			_roundStartedCount = 0;
			base.HandleRoundEnd(sender, e);
		}

		protected void HandleRoundFinal(object sender, RoundFinalEventArgs e)
		{
			_isLastRoundFinal = true;
		}

		protected new void HandleRoundOfficiallyEnd(object sender, RoundOfficiallyEndedEventArgs e)
		{
			if (!IsMatchStarted) return;

			CheckForSpecialClutchEnd();
			UpdateKillsCount();
			UpdatePlayerScore();

			Application.Current.Dispatcher.Invoke(delegate
			{
				Demo.Rounds.Add(CurrentRound);
			});

			// End of a half
			if (IsLastRoundHalf)
			{
				IsHalfMatch = !IsHalfMatch;
				IsMatchStarted = false;
			}

			// Last round of the match ended, may have OT
			if (_isLastRoundFinal)
			{
				IsMatchStarted = false;
				IsOvertime = true;

				// Add the current overtime only if it's not the first
				if (CurrentOvertime.Number != 0)
				{
					Application.Current.Dispatcher.Invoke(delegate
					{
						Demo.Overtimes.Add(CurrentOvertime);
					});
					IsHalfMatch = true;
				}
				else
				{
					// If it's the first OT, teams haven't been swapped
					IsHalfMatch = false;
				}

				// Create new OT
				CurrentOvertime = new Overtime
				{
					Number = ++OvertimeCount
				};
			}

			if (IsLastRoundHalf)
			{
				CheckForSpecialClutchEnd();
				IsSwapTeamRequired = true;
				IsLastRoundHalf = false;
			}
		}

		private void AddTeams()
		{
			// Add all players to our ObservableCollection of PlayerExtended
			foreach (Player player in Parser.PlayingParticipants)
			{
				PlayerExtended pl = new PlayerExtended
				{
					SteamId = player.SteamID,
					Name = player.Name,
					Side = player.Team
				};

				Application.Current.Dispatcher.Invoke(delegate
				{
					if (!Demo.Players.Contains(pl)) Demo.Players.Add(pl);

					if (pl.Side == Team.CounterTerrorist)
					{
						pl.TeamName = Demo.TeamCT.Name;
						if (!Demo.TeamCT.Players.Contains(pl)) Demo.TeamCT.Players.Add(pl);
					}

					if (pl.Side == Team.Terrorist)
					{
						pl.TeamName = Demo.TeamT.Name;
						if (!Demo.TeamT.Players.Contains(pl)) Demo.TeamT.Players.Add(pl);
					}
				});
			}
		}
	}
}