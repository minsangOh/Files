// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Files.App.UserControls.TabBar
{
	/// <summary>
	/// Represents item for <see cref="TabBar"/>.
	/// </summary>
	public sealed partial class TabBarItem : ObservableObject, ITabBarItem, IDisposable
	{
		// Background tabs release their pages after this delay and rebuild them from NavigationParameter when selected again
		private static readonly TimeSpan HibernationDelay = TimeSpan.FromSeconds(30);

		private DispatcherQueueTimer? hibernationTimer;

		public Frame? ContentFrame { get; private set; }

		public event EventHandler<TabBarItemParameter>? ContentChanged;

		private IconSource? _IconSource;
		public IconSource? IconSource
		{
			get => _IconSource;
			set => SetProperty(ref _IconSource, value);
		}

		private string? _Header;
		public string? Header
		{
			get => _Header;
			set => SetProperty(ref _Header, value);
		}

		private string? _Description = null;
		public string? Description
		{
			get => _Description;
			set => SetProperty(ref _Description, value);
		}

		private string? _ToolTipText;
		public string? ToolTipText
		{
			get => _ToolTipText;
			set => SetProperty(ref _ToolTipText, value);
		}

		private bool _AllowStorageItemDrop;
		public bool AllowStorageItemDrop
		{
			get => _AllowStorageItemDrop;
			set => SetProperty(ref _AllowStorageItemDrop, value);
		}

		private TabBarItemParameter? _NavigationArguments;
		public TabBarItemParameter? NavigationParameter
		{
			get => _NavigationArguments;
			set
			{
				if (value != _NavigationArguments)
				{
					_NavigationArguments = value;
					if (_NavigationArguments is not null)
					{
						ContentFrame?.Navigate(_NavigationArguments.InitialPageType, _NavigationArguments.NavigationParameter, new SuppressNavigationTransitionInfo());
					}
					else
					{
						if (ContentFrame is not null)
							ContentFrame.Content = null;
					}
				}
			}
		}

		public ITabBarItemContent? TabItemContent
			=> ContentFrame?.Content as ITabBarItemContent;

		public TabBarItem()
		{
			ContentFrame = new()
			{
				CacheSize = 0,
				IsNavigationStackEnabled = false,
			};

			ContentFrame.Navigated += ContentFrame_Navigated;
		}

		public void ScheduleHibernation()
		{
			if (ContentFrame?.Content is null || hibernationTimer?.IsRunning == true)
				return;

			if (hibernationTimer is null)
			{
				hibernationTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
				hibernationTimer.Interval = HibernationDelay;
				hibernationTimer.IsRepeating = false;
				hibernationTimer.Tick += HibernationTimer_Tick;
			}

			hibernationTimer.Start();
		}

		public void Wake()
		{
			hibernationTimer?.Stop();

			if (ContentFrame is { Content: null } frame && _NavigationArguments is not null)
				frame.Navigate(_NavigationArguments.InitialPageType, _NavigationArguments.NavigationParameter, new SuppressNavigationTransitionInfo());
		}

		private void HibernationTimer_Tick(DispatcherQueueTimer sender, object args)
		{
			var content = TabItemContent;
			if (content is null || content.IsCurrentInstance || ContentFrame is null)
				return;

			content.ContentChanged -= TabItemContent_ContentChanged;
			content.Dispose();
			ContentFrame.Content = null;

			AppMemoryHelper.RequestTrim();
		}

		public void Unload()
		{
			ContentChanged -= NavigationHelpers.Control_ContentChanged;

			Dispose();
		}

		private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
		{
			if (TabItemContent is not null)
				TabItemContent.ContentChanged += TabItemContent_ContentChanged;
		}

		private void TabItemContent_ContentChanged(object? sender, TabBarItemParameter e)
		{
			_NavigationArguments = e;
			ContentChanged?.Invoke(this, e);
		}

		public void Dispose()
		{
			if (hibernationTimer is not null)
			{
				hibernationTimer.Stop();
				hibernationTimer.Tick -= HibernationTimer_Tick;
				hibernationTimer = null;
			}

			var content = TabItemContent;
			if (content is not null)
			{
				content.ContentChanged -= TabItemContent_ContentChanged;
				content.Dispose();
			}

			var frame = ContentFrame;
			if (frame is not null)
			{
				frame.Navigated -= ContentFrame_Navigated;
				frame.Content = null;
				ContentFrame = null;
				OnPropertyChanged(nameof(ContentFrame));
			}
		}
	}
}
