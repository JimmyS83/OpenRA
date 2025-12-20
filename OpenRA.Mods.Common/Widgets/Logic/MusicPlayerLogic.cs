#region Copyright & License Information

/*



 * Copyright (c) The OpenRA Developers and Contributors



 * This file is part of OpenRA, which is free software. It is made



 * available to you under the terms of the GNU General Public License



 * as published by the Free Software Foundation, either version 3 of



 * the License, or (at your option) any later version. For more



 * information, see COPYING.



 */

#endregion

using System;

using System.Collections.Generic;

using System.Linq;

using OpenRA.GameRules;

using OpenRA.Mods.Common.Traits;

using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class MusicPlayerLogic : ChromeLogic
	{
		[FluentReference]

		const string SoundMuted = "label-sound-muted";

		[FluentReference]

		const string NoSongPlaying = "label-no-song-playing";

		readonly ScrollPanelWidget musicList;

		readonly ScrollItemWidget itemTemplate;

		readonly MusicPlaylist musicPlaylist;

		MusicInfo currentSong = null;

		bool noSongsForMode;

		readonly Widget panel;

		readonly bool allowPanelResize;

		readonly int panelCenterY;

		readonly Widget customFilterPanel;

		readonly Widget labelContainer;

		readonly Widget buttonsPanel;

		readonly LabelWidget timeLabelWidget;

		readonly LabelWidget titleLabelWidget;

		readonly CheckboxWidget shuffleWidget;

		readonly CheckboxWidget repeatWidget;

		readonly LabelWidget volumeLabelWidget;

		readonly SliderWidget musicSliderWidget;

		readonly Widget noMusicContainer;

		readonly LabelWidget muteLabelWidget;

		readonly Widget modePanel;

		readonly ButtonWidget backButtonWidget;

		readonly LabelWidget noSongsLabelWidget;

		readonly List<(Widget Widget, int BaseY, int BaseHeight, bool StretchHeight, bool ShiftWithCustom)> layoutShiftTargets = new();

		readonly List<(Widget Widget, int BaseY)> customRaiseTargets = new();

		readonly List<(Widget Widget, int BaseY)> customControlTargets = new();

		const int CustomDownOffset = 0;

		const int CustomUpOffset = 0;

		const int CustomLowerOffset = 0;

		const int CustomControlDownOffset = 130;

		readonly int basePanelHeight;

		readonly int expandedPanelHeight;

		readonly int collapsedPanelHeight;

		readonly int customLayoutOffset;

		bool showCustomFilters;

		static readonly StringComparer CustomCategoryComparer = StringComparer.OrdinalIgnoreCase;

		static readonly (string CheckboxId, string Category)[] CustomFilterOptions = new[]
		{
			("CUSTOM_FILTER_ALLIES", "allies"),

			("CUSTOM_FILTER_SOVIET", "soviet"),

			("CUSTOM_FILTER_CHINA", "china"),

			("CUSTOM_FILTER_GDI", "gdi"),

			("CUSTOM_FILTER_NOD", "nod"),

			("CUSTOM_FILTER_SCRIN", "scrin"),

			("CUSTOM_FILTER_GENERIC", MusicCategories.Generic),

			("CUSTOM_FILTER_OLDSCHOOL", MusicCategories.Oldschool)
		};

		[ObjectCreator.UseCtor]

		public MusicPlayerLogic(Widget widget, World world, ModData modData, Action onExit)
		{
			panel = widget;

			musicList = panel.Get<ScrollPanelWidget>("MUSIC_LIST");

			itemTemplate = musicList.Get<ScrollItemWidget>("MUSIC_TEMPLATE");

			musicPlaylist = world.WorldActor.Trait<MusicPlaylist>();

			customFilterPanel = panel.GetOrNull<Widget>("CUSTOM_FILTER_PANEL");

			labelContainer = panel.GetOrNull<Widget>("LABEL_CONTAINER");

			buttonsPanel = panel.GetOrNull<Widget>("BUTTONS");

			timeLabelWidget = panel.GetOrNull<LabelWidget>("TIME_LABEL");

			titleLabelWidget = panel.GetOrNull<LabelWidget>("TITLE_LABEL");

			shuffleWidget = panel.Get<CheckboxWidget>("SHUFFLE");

			repeatWidget = panel.Get<CheckboxWidget>("REPEAT");

			volumeLabelWidget = panel.GetOrNull<LabelWidget>("VOLUME_LABEL");

			musicSliderWidget = panel.GetOrNull<SliderWidget>("MUSIC_SLIDER");

			noMusicContainer = panel.GetOrNull<Widget>("NO_MUSIC_LABEL");

			muteLabelWidget = panel.GetOrNull<LabelWidget>("MUTE_LABEL");

			modePanel = panel.GetOrNull<Widget>("MODE_PANEL");

			backButtonWidget = panel.GetOrNull<ButtonWidget>("BACK_BUTTON");

			noSongsLabelWidget = panel.GetOrNull<LabelWidget>("NO_MATCHING_SONGS");

			basePanelHeight = panel.Bounds.Height;

			panelCenterY = panel.Bounds.Y + basePanelHeight / 2;

			allowPanelResize = panel.Id == "MUSIC_PANEL";

			customLayoutOffset = customFilterPanel != null ? customFilterPanel.Bounds.Height + CustomDownOffset + CustomLowerOffset : 0;

			expandedPanelHeight = basePanelHeight + (customFilterPanel != null ? customLayoutOffset : 0);

			collapsedPanelHeight = basePanelHeight;

			if (customFilterPanel != null)

				customFilterPanel.IsVisible = () => showCustomFilters;

			RegisterLayoutTarget(labelContainer, shiftWithCustom: true);

			RegisterLayoutTarget(musicList, shiftWithCustom: true);

			RegisterLayoutTarget(noSongsLabelWidget, shiftWithCustom: true);

			RegisterLayoutTarget(titleLabelWidget, shiftWithCustom: true);

			RegisterLayoutTarget(buttonsPanel);

			RegisterLayoutTarget(timeLabelWidget);

			RegisterLayoutTarget(shuffleWidget);

			RegisterLayoutTarget(repeatWidget);

			RegisterLayoutTarget(volumeLabelWidget);

			RegisterLayoutTarget(musicSliderWidget);

			RegisterLayoutTarget(noMusicContainer);

			RegisterLayoutTarget(muteLabelWidget);

			RegisterCustomRaiseTarget(modePanel);

			RegisterLayoutTarget(backButtonWidget);

			RegisterCustomControlTarget(buttonsPanel);

			RegisterCustomControlTarget(timeLabelWidget);

			RegisterCustomControlTarget(shuffleWidget);

			RegisterCustomControlTarget(repeatWidget);

			RegisterCustomControlTarget(volumeLabelWidget);

			if (buttonsPanel == null || musicSliderWidget?.Parent != buttonsPanel)

				RegisterCustomControlTarget(musicSliderWidget);

			RegisterCustomControlTarget(noMusicContainer);

			RegisterCustomControlTarget(muteLabelWidget);

			RegisterCustomControlTarget(backButtonWidget);

			if (noSongsLabelWidget != null)

				noSongsLabelWidget.IsVisible = () => noSongsForMode;

			ConfigureModeButton(panel, "MUSIC_MODE_MIX", MusicPlaybackMode.MixAll);

			ConfigureModeButton(panel, "MUSIC_MODE_OLD", MusicPlaybackMode.OnlyOldschool);

			ConfigureModeButton(panel, "MUSIC_MODE_FACTION", MusicPlaybackMode.FactionSpecific);

			ConfigureModeButton(panel, "MUSIC_MODE_CUSTOM", MusicPlaybackMode.Custom);

			ConfigureCustomFilters();

			bool NoMusic() => !musicPlaylist.IsMusicAvailable || musicPlaylist.CurrentSongIsBackground || currentSong == null;

			if (noMusicContainer != null)

				noMusicContainer.IsVisible = () => !musicPlaylist.IsMusicAvailable;

			if (musicPlaylist.IsMusicAvailable && muteLabelWidget != null)
			{
				muteLabelWidget.GetText = () =>
				{
					if (Game.Settings.Sound.Mute)

						return FluentProvider.GetMessage(SoundMuted);

					return "";
				};
			}

			var playButton = panel.Get<ButtonWidget>("BUTTON_PLAY");

			playButton.OnClick = Play;

			playButton.IsDisabled = NoMusic;

			playButton.IsVisible = () => !Game.Sound.MusicPlaying;

			var pauseButton = panel.Get<ButtonWidget>("BUTTON_PAUSE");

			pauseButton.OnClick = Game.Sound.PauseMusic;

			pauseButton.IsDisabled = NoMusic;

			pauseButton.IsVisible = () => Game.Sound.MusicPlaying;

			var stopButton = panel.Get<ButtonWidget>("BUTTON_STOP");

			stopButton.OnClick = musicPlaylist.Stop;

			stopButton.IsDisabled = NoMusic;

			var nextButton = panel.Get<ButtonWidget>("BUTTON_NEXT");

			nextButton.OnClick = () => { currentSong = musicPlaylist.GetNextSong(); Play(); };

			nextButton.IsDisabled = NoMusic;

			var prevButton = panel.Get<ButtonWidget>("BUTTON_PREV");

			prevButton.OnClick = () => { currentSong = musicPlaylist.GetPrevSong(); Play(); };

			prevButton.IsDisabled = NoMusic;

			shuffleWidget.IsChecked = () => Game.Settings.Sound.Shuffle;

			shuffleWidget.OnClick = () => Game.Settings.Sound.Shuffle ^= true;

			shuffleWidget.IsDisabled = () => musicPlaylist.CurrentSongIsBackground;

			repeatWidget.IsChecked = () => Game.Settings.Sound.Repeat;

			repeatWidget.OnClick = () => Game.Sound.SetMusicLooped(!Game.Settings.Sound.Repeat);

			repeatWidget.IsDisabled = () => musicPlaylist.CurrentSongIsBackground;

			if (timeLabelWidget != null)

				timeLabelWidget.GetText = () =>
				{
					if (currentSong == null || musicPlaylist.CurrentSongIsBackground)

						return "";

					var seek = Game.Sound.MusicSeekPosition;

					var minutes = (int)seek / 60;

					var seconds = (int)seek % 60;

					var totalMinutes = currentSong.Length / 60;

					var totalSeconds = currentSong.Length % 60;

					return $"{minutes:D2}:{seconds:D2} / {totalMinutes:D2}:{totalSeconds:D2}";
				};

			var noSongPlaying = FluentProvider.GetMessage(NoSongPlaying);

			var musicTitle = panel.GetOrNull<LabelWidget>("TITLE_LABEL");

			if (musicTitle != null)

				musicTitle.GetText = () => currentSong != null ? currentSong.Title : noSongPlaying;

			var musicSlider = panel.Get<SliderWidget>("MUSIC_SLIDER");

			musicSlider.OnChange += x => Game.Sound.MusicVolume = x;

			musicSlider.Value = Game.Sound.MusicVolume;

			var songWatcher = widget.GetOrNull<LogicTickerWidget>("SONG_WATCHER");

			if (songWatcher != null)
			{
				songWatcher.OnTick = () =>
				{
					if (musicPlaylist.CurrentSongIsBackground && currentSong != null)

						currentSong = null;

					if (Game.Sound.CurrentMusic == null || currentSong == Game.Sound.CurrentMusic || musicPlaylist.CurrentSongIsBackground)

						return;

					currentSong = Game.Sound.CurrentMusic;
				};
			}

			if (backButtonWidget != null)

				backButtonWidget.OnClick = () => { Game.Settings.Save(); Ui.CloseWindow(); onExit(); };

			UpdateCustomLayout(forceRefresh: true);

			BuildMusicTable();
		}

		public void BuildMusicTable()
		{
			if (!musicPlaylist.IsMusicAvailable)

				return;

			var music = musicPlaylist.AvailablePlaylist();

			noSongsForMode = musicPlaylist.IsMusicAvailable && music.Length == 0;

			currentSong = musicPlaylist.CurrentSong();

			musicList.RemoveChildren();

			foreach (var song in music)
			{
				var item = ScrollItemWidget.Setup(song.Filename, itemTemplate, () => currentSong == song, () => { currentSong = song; Play(); }, () => { });

				var label = item.Get<LabelWithTooltipWidget>("TITLE");

				WidgetUtils.TruncateLabelToTooltip(label, song.Title);

				item.Get<LabelWidget>("LENGTH").GetText = () => SongLengthLabel(song);

				musicList.AddChild(item);
			}

			if (currentSong != null && !musicPlaylist.CurrentSongIsBackground)

				musicList.ScrollToItem(currentSong.Filename);
		}

		void ConfigureModeButton(Widget panel, string id, MusicPlaybackMode mode)
		{
			var button = panel.GetOrNull<ButtonWidget>(id);

			if (button == null)

				return;

			button.IsHighlighted = () => Game.Settings.Sound.MusicMode == mode;

			button.OnClick = () =>
			{
				if (Game.Settings.Sound.MusicMode == mode)

					return;

				var resume = Game.Sound.MusicPlaying && !musicPlaylist.CurrentSongIsBackground;

				Game.Settings.Sound.MusicMode = mode;

				UpdateCustomLayout(forceRefresh: false);

				RefreshPlaylistAfterFilterChange(resume);
			};
		}

		void ConfigureCustomFilters()
		{
			if (customFilterPanel == null)

				return;

			foreach (var (checkboxId, category) in CustomFilterOptions)
			{
				var checkbox = customFilterPanel.GetOrNull<CheckboxWidget>(checkboxId);

				if (checkbox == null)

					continue;

				checkbox.IsChecked = () => IsCustomCategoryEnabled(category);

				checkbox.OnClick = () => ToggleCustomCategory(category);
			}
		}

		void RegisterLayoutTarget(Widget widget, bool stretchHeight = false, bool shiftWithCustom = false)
		{
			if (widget == null)

				return;

			layoutShiftTargets.Add((widget, widget.Bounds.Y, widget.Bounds.Height, stretchHeight, shiftWithCustom));
		}

		void RegisterCustomRaiseTarget(Widget widget)
		{
			if (widget == null)

				return;

			customRaiseTargets.Add((widget, widget.Bounds.Y));
		}

		void RegisterCustomControlTarget(Widget widget)
		{
			if (widget == null)

				return;

			customControlTargets.Add((widget, widget.Bounds.Y));
		}

		void UpdateCustomLayout(bool forceRefresh)
		{
			if (customFilterPanel == null)

				return;

			var shouldShow = Game.Settings.Sound.MusicMode == MusicPlaybackMode.Custom;

			if (!forceRefresh && shouldShow == showCustomFilters)

				return;

			showCustomFilters = shouldShow;

			var height = showCustomFilters ? expandedPanelHeight : collapsedPanelHeight;

			var offset = showCustomFilters ? customLayoutOffset : 0;

			if (allowPanelResize)
			{
				var centeredY = panelCenterY - height / 2;

				panel.Bounds = new WidgetBounds(panel.Bounds.X, centeredY, panel.Bounds.Width, height);
			}

			foreach (var (widget, baseY, baseHeight, stretchHeight, shiftWithCustom) in layoutShiftTargets)
			{
				if (widget == null)

					continue;

				var bounds = widget.Bounds;

				var targetHeight = stretchHeight && allowPanelResize

					? baseHeight + (showCustomFilters ? customLayoutOffset : 0)

					: baseHeight;

				var shouldShift = showCustomFilters && allowPanelResize && shiftWithCustom;

				var yOffset = shouldShift ? offset : 0;

				widget.Bounds = new WidgetBounds(bounds.X, baseY + yOffset, bounds.Width, targetHeight);
			}

			foreach (var (widget, baseY) in customRaiseTargets)
			{
				if (widget == null)

					continue;

				var bounds = widget.Bounds;

				var y = showCustomFilters ? baseY - CustomUpOffset : baseY;

				widget.Bounds = new WidgetBounds(bounds.X, y, bounds.Width, bounds.Height);
			}

			foreach (var (widget, baseY) in customControlTargets)
			{
				if (widget == null)

					continue;

				var bounds = widget.Bounds;

				var y = showCustomFilters ? baseY + CustomControlDownOffset : baseY;

				widget.Bounds = new WidgetBounds(bounds.X, y, bounds.Width, bounds.Height);
			}
		}

		void Play()
		{
			if (currentSong == null)

				return;

			musicList.ScrollToItem(currentSong.Filename);

			musicPlaylist.Play(currentSong);
		}

		void ToggleCustomCategory(string category)
		{
			var normalized = NormalizeCategory(category);

			if (string.IsNullOrEmpty(normalized))

				return;

			var selection = Game.Settings.Sound.CustomMusicCategories?.ToList() ?? new List<string>();

			var existingIndex = selection.FindIndex(c => CustomCategoryComparer.Equals(c, normalized));

			if (existingIndex >= 0)

				selection.RemoveAt(existingIndex);
			else

				selection.Add(normalized);

			Game.Settings.Sound.CustomMusicCategories = selection.ToArray();

			var resume = Game.Sound.MusicPlaying && !musicPlaylist.CurrentSongIsBackground;

			RefreshPlaylistAfterFilterChange(resume);
		}

		static bool IsCustomCategoryEnabled(string category)
		{
			var normalized = NormalizeCategory(category);

			if (string.IsNullOrEmpty(normalized))

				return false;

			return (Game.Settings.Sound.CustomMusicCategories ?? Array.Empty<string>())

				.Any(c => CustomCategoryComparer.Equals(c, normalized));
		}

		static string NormalizeCategory(string category)
		{
			return MusicCategories.Normalize(category, null);
		}

		void RefreshPlaylistAfterFilterChange(bool resume)
		{
			musicPlaylist.RefreshForPlaybackModeChange(resume);

			BuildMusicTable();

			if (resume)

				Play();
		}

		static string SongLengthLabel(MusicInfo song)
		{
			return $"{song.Length / 60:D1}:{song.Length % 60:D2}";
		}
	}
}
