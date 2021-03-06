﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CSGO_Demos_Manager.Models;
using CSGO_Demos_Manager.Models.Events;
using CSGO_Demos_Manager.Models.Stats;
using CSGO_Demos_Manager.Models.Timeline;
using CSGO_Demos_Manager.Services;
using CSGO_Demos_Manager.Services.Design;
using CSGO_Demos_Manager.Services.Interfaces;
using CSGO_Demos_Manager.Views;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using MahApps.Metro.Controls.Dialogs;

namespace CSGO_Demos_Manager.ViewModel
{
	public class RoundViewModel : ViewModelBase
	{
		#region Properties

		private readonly DialogService _dialogService;

		private readonly IRoundService _roundService;

		private readonly IPlayerService _playerService;

		private readonly ICacheService _cacheService;

		private Demo _currentDemo;

		private int _roundNumber;

		private Round _currentRound;

		private KillEvent _selectedKill;

		private List<RoundEvent> _roundEventList;

		private List<PlayerRoundStats> _playersStats;

		private DateTime _visibleStartTime;

		private DateTime _visibleEndTime;

		private RelayCommand _windowLoadedCommand;

		private RelayCommand<Demo> _backToDemoDetailsCommand;

		private RelayCommand<KillEvent> _watchKillCommand;

		private RelayCommand _goToNextRoundCommand;

		private RelayCommand _goToPreviousRound;

		private RelayCommand _watchRoundCommand;

		#endregion

		#region Accessors

		public Demo CurrentDemo
		{
			get { return _currentDemo; }
			set { Set(() => CurrentDemo, ref _currentDemo, value); }
		}

		public int RoundNumber
		{
			get { return _roundNumber; }
			set { Set(() => RoundNumber, ref _roundNumber, value); }
		}

		public Round CurrentRound
		{
			get { return _currentRound; }
			set { Set(() => CurrentRound, ref _currentRound, value); }
		}

		public List<RoundEvent> RoundEventList
		{
			get { return _roundEventList; }
			set { Set(() => RoundEventList, ref _roundEventList, value); }
		}

		public List<PlayerRoundStats> PlayersStats
		{
			get { return _playersStats; }
			set { Set(() => PlayersStats, ref _playersStats, value); }
		}

		public KillEvent SelectedKill
		{
			get { return _selectedKill; }
			set { Set(() => SelectedKill, ref _selectedKill, value); }
		}

		public DateTime VisibleStartTime
		{
			get { return _visibleStartTime; }
			set { Set(() => VisibleStartTime, ref _visibleStartTime, value); }
		}

		public DateTime VisibleEndTime
		{
			get { return _visibleEndTime; }
			set { Set(() => VisibleEndTime, ref _visibleEndTime, value); }
		}

		#endregion

		#region Commands

		/// <summary>
		/// Command to back to details view
		/// </summary>
		public RelayCommand<Demo> BackToDemoDetailsCommand
		{
			get
			{
				return _backToDemoDetailsCommand
					?? (_backToDemoDetailsCommand = new RelayCommand<Demo>(
						demo =>
						{
							var detailsViewModel = (new ViewModelLocator()).Details;
							detailsViewModel.CurrentDemo = demo;
							var mainViewModel = (new ViewModelLocator()).Main;
							DetailsView detailsView = new DetailsView();
							mainViewModel.CurrentPage.ShowPage(detailsView);
						},
						demo => CurrentDemo != null));
			}
		}

		/// <summary>
		/// Command to watch the round
		/// </summary>
		public RelayCommand WatchRoundCommand
		{
			get
			{
				return _watchRoundCommand
					?? (_watchRoundCommand = new RelayCommand(
					async () =>
					{
						if (AppSettings.SteamExePath() == null)
						{
							await _dialogService.ShowMessageAsync("Steam doesn't seems to be installed." + Environment.NewLine
								+ "Unable to start the game.", MessageDialogStyle.Affirmative);
							return;
						}
						GameLauncher launcher = new GameLauncher();
						launcher.WatchDemoAt(CurrentDemo, CurrentRound.Tick);
					},
					() => CurrentRound != null));
			}
		}

		/// <summary>
		/// Command to go to the next round
		/// </summary>
		public RelayCommand GoToNextRoundCommand
		{
			get
			{
				return _goToNextRoundCommand
					?? (_goToNextRoundCommand = new RelayCommand(
						async () =>
						{
							RoundNumber++;
							await LoadDatas();
						},
						() => RoundNumber < CurrentDemo.Rounds.Count));
			}
		}

		/// <summary>
		/// Command to go to the previous round
		/// </summary>
		public RelayCommand GoToPreviousRoundCommand
		{
			get
			{
				return _goToPreviousRound
					?? (_goToPreviousRound = new RelayCommand(
						async () =>
						{
							RoundNumber--;
							await LoadDatas();
						},
						() => RoundNumber > 1));
			}
		}

		public RelayCommand WindowLoaded
		{
			get
			{
				return _windowLoadedCommand
					?? (_windowLoadedCommand = new RelayCommand(
					async () =>
					{
						await LoadDatas();
					}));
			}
		}

		#endregion

		private async Task LoadDatas()
		{
			CurrentDemo.WeaponFired = await _cacheService.GetDemoWeaponFiredAsync(CurrentDemo);
			CurrentRound = CurrentDemo.Rounds.First(r => r.Number == RoundNumber);
			VisibleStartTime = DateTime.Today.AddSeconds(-5);
			VisibleEndTime = CurrentRound.EndTickTime.AddSeconds(5);
			RoundEventList = await _roundService.GetTimeLineEventList(CurrentDemo, CurrentRound);
			PlayersStats = await _playerService.GetRoundStats(CurrentDemo, CurrentRound);
		}

		public RelayCommand<KillEvent> WatchKillCommand
		{
			get
			{
				return _watchKillCommand
					?? (_watchKillCommand = new RelayCommand<KillEvent>(
						async kill =>
						{
							if (AppSettings.SteamExePath() == null)
							{
								await _dialogService.ShowSteamNotFoundAsync();
								return;
							}
							GameLauncher launcher = new GameLauncher();
							launcher.WatchDemoAt(CurrentDemo, kill.Tick, true, kill.KillerEntityId);
						}));
			}
		}

		public RoundViewModel(DialogService dialogService, IRoundService rounderService, IPlayerService playerService, ICacheService cacheService)
		{
			_dialogService = dialogService;
			_roundService = rounderService;
			_playerService = playerService;
			_cacheService = cacheService;

			if (IsInDesignMode)
			{
				Application.Current.Dispatcher.Invoke(async () =>
				{
					DemosDesignService design = new DemosDesignService();
					CurrentDemo = await design.AnalyzeDemo(new Demo(), CancellationToken.None);
					RoundNumber = 10;
					await LoadDatas();
				});
			}
		}
	}
}
