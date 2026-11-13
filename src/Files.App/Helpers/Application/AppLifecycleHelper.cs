// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.Helpers.Application;
using Files.App.Services.Git;
using Files.App.Services.SizeProvider;
using Files.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.IO;
using System.Text;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Files.App.Helpers
{
	/// <summary>
	/// Provides static helper to manage app lifecycle.
	/// </summary>
	public static class AppLifecycleHelper
	{
		private readonly static string AppInformationKey = @$"Software\Files Community\{Package.Current.Id.Name}\v1\AppInformation";

		/// <summary>
		/// Gets the value that indicates whether the app is updated.
		/// </summary>
		public static bool IsAppUpdated { get; }

		/// <summary>
		/// Gets the value that indicates whether the app is running for the first time.
		/// </summary>
		public static bool IsFirstRun { get; }

		/// <summary>
		/// Gets the value that indicates the total launch count of the app.
		/// </summary>
		public static long TotalLaunchCount { get; }

		static AppLifecycleHelper()
		{
			using var infoKey = Registry.CurrentUser.CreateSubKey(AppInformationKey);
			var version = infoKey.GetValue("LastLaunchVersion");
			var launchCount = infoKey.GetValue("TotalLaunchCount");
			if (version is null)
			{
				IsAppUpdated = true;
				IsFirstRun = true;
			}
			else
			{
				IsAppUpdated = version.ToString() != AppVersion.ToString();
			}

			TotalLaunchCount = long.TryParse(launchCount?.ToString(), out var v) ? v + 1 : 1;
			infoKey.SetValue("LastLaunchVersion", AppVersion.ToString());
			infoKey.SetValue("TotalLaunchCount", TotalLaunchCount);
		}

		/// <summary>
		/// Gets the value that provides application environment or branch name.
		/// </summary>
		public static AppEnvironment AppEnvironment =>
			Enum.TryParse("cd_app_env_placeholder", true, out AppEnvironment appEnvironment)
				? appEnvironment
				: AppEnvironment.Dev;

		/// <summary>
		/// Gets application package version.
		/// </summary>
		public static Version AppVersion { get; } =
			new(Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);

		/// <summary>
		/// Gets application icon path.
		/// </summary>
		public static string AppIconPath => _appIconPath.Value;

		private static readonly Lazy<string> _appIconPath = new(() =>
			SystemIO.Path.Combine(Package.Current.InstalledPath, AppEnvironment switch
			{
				AppEnvironment.Dev => Constants.AssetPaths.DevLogo,
				AppEnvironment.SideloadPreview or AppEnvironment.StorePreview => Constants.AssetPaths.PreviewLogo,
				_ => Constants.AssetPaths.StableLogo
			}));

		/// <summary>
		/// Initializes the app components.
		/// </summary>
		public static async Task InitializeAppComponentsAsync()
		{
			var userSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
			var addItemService = Ioc.Default.GetRequiredService<IAddItemService>();
			var generalSettingsService = userSettingsService.GeneralSettingsService;
			var jumpListService = Ioc.Default.GetRequiredService<IWindowsJumpListService>();

			// Start non-critical tasks without waiting; pinned loads alongside the others so its shell enumeration doesn't block them.
			_ = Task.Run(async () =>
			{
				await SafetyExtensions.IgnoreExceptions(() => Task.WhenAll(
					App.QuickAccessManager.InitializeAsync(),
					OptionalTaskAsync(CloudDrivesManager.UpdateDrivesAsync(), generalSettingsService.ShowCloudDrivesSection),
					App.LibraryManager.UpdateLibrariesAsync(),
					OptionalTaskAsync(WSLDistroManager.UpdateDrivesAsync(), generalSettingsService.ShowWslSection),
					OptionalTaskAsync(App.FileTagsManager.UpdateFileTagsAsync(), generalSettingsService.ShowFileTagsSection),
					jumpListService.InitializeAsync()
				), App.Logger);

				await SafetyExtensions.IgnoreExceptions(addItemService.InitializeAsync, App.Logger);
			});

			_ = Task.Run(async () =>
			{
				// The follwing method invokes UI thread, so we run it in a separate task
				await CheckAppUpdate();

			});

			static Task OptionalTaskAsync(Task task, bool condition)
			{
				if (condition)
					return task;

				return Task.CompletedTask;
			}

		}

		/// <summary>
		/// Checks application updates and download if available.
		/// </summary>
		public static async Task CheckAppUpdate()
		{
			var updateService = Ioc.Default.GetRequiredService<IUpdateService>();

			await updateService.CheckForUpdatesAsync();
			await updateService.DownloadMandatoryUpdatesAsync();

			if (IsAppUpdated)
				await updateService.CheckAndUpdateFilesLauncherAsync();
		}

		/// <summary>
		/// Configures DI (dependency injection) container.
		/// </summary>
		/// <param name="appModel">Constructed on the UI thread by the caller (its ctor is UI-thread-only).</param>
		public static IServiceProvider ConfigureHost(AppModel appModel)
		{
			var services = new ServiceCollection();
			var fileLoggerProvider = new FileLoggerProvider(Path.Combine(ApplicationData.Current.LocalFolder.Path, $"debug_{(Environment.IsPrivilegedProcess ? "privileged" : "unprivileged")}.log"));

			services.AddSingleton(fileLoggerProvider);

			services.AddLogging(builder => builder
					.AddDebug()
					.AddProvider(fileLoggerProvider)
					.SetMinimumLevel(LogLevel.Information));

			services
					// Settings services
					.AddSingleton<IUserSettingsService, UserSettingsService>()
					.AddSingleton<IAppearanceSettingsService, AppearanceSettingsService>(sp => new AppearanceSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))
					.AddSingleton<IGeneralSettingsService, GeneralSettingsService>(sp => new GeneralSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))
					.AddSingleton<IFoldersSettingsService, FoldersSettingsService>(sp => new FoldersSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))
					.AddSingleton<IDevToolsSettingsService, DevToolsSettingsService>(sp => new DevToolsSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))
					.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>(sp => new ApplicationSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))
					.AddSingleton<IInfoPaneSettingsService, InfoPaneSettingsService>(sp => new InfoPaneSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))
					.AddSingleton<ILayoutSettingsService, LayoutSettingsService>(sp => new LayoutSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))
					.AddSingleton<IAppSettingsService, AppSettingsService>(sp => new AppSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))
					.AddSingleton<IActionsSettingsService, ActionsSettingsService>(sp => new ActionsSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))
					.AddSingleton<IFileTagsSettingsService, FileTagsSettingsService>()
					// Contexts
					.AddSingleton<IMultiPanesContext, MultiPanesContext>()
					.AddSingleton<IContentPageContext, ContentPageContext>()
					.AddSingleton<IDisplayPageContext, DisplayPageContext>()
					.AddSingleton<IHomePageContext, HomePageContext>()
					.AddSingleton<IWindowContext, WindowContext>()
					.AddSingleton<IMultitaskingContext, MultitaskingContext>()
					.AddSingleton<ITagsContext, TagsContext>()
					.AddSingleton<ISidebarContext, SidebarContext>()
					.AddSingleton<IShelfContext, ShelfContext>()
					// Services
					.AddSingleton<IWindowsRecentItemsService, WindowsRecentItemsService>()
					.AddSingleton<IWindowsIniService, WindowsIniService>()
					.AddSingleton<IWindowsWallpaperService, WindowsWallpaperService>()
					.AddSingleton<IWindowsSecurityService, WindowsSecurityService>()
					.AddSingleton<IAppThemeModeService, AppThemeModeService>()
					.AddSingleton<IDialogService, DialogService>()
					.AddSingleton<ICommonDialogService, CommonDialogService>()
					.AddSingleton<IImageService, ImagingService>()
					.AddSingleton<IThreadingService, ThreadingService>()
					.AddSingleton<ILocalizationService, LocalizationService>()
					.AddSingleton<ICloudDetector, CloudDetector>()
					.AddSingleton<IFileTagsService, FileTagsService>()
					.AddSingleton<ICommandManager, CommandManager>()
					.AddSingleton<IModifiableCommandManager, ModifiableCommandManager>()
					.AddSingleton<IStorageService, NativeStorageLegacyService>()
					.AddSingleton<IAddItemService, AddItemService>()
					.AddSingleton<IPreviewPopupService, PreviewPopupService>()
					.AddSingleton<IDateTimeFormatterFactory, DateTimeFormatterFactory>()
					.AddSingleton<IDateTimeFormatter, UserDateTimeFormatter>()
					.AddSingleton<ISizeProvider, UserSizeProvider>()
					.AddSingleton<IQuickAccessService, QuickAccessService>()
					.AddSingleton<IResourcesService, ResourcesService>()
					.AddSingleton<IWindowsJumpListService, WindowsJumpListService>()
					.AddSingleton<IStorageTrashBinService, StorageTrashBinService>()
					.AddSingleton<IRemovableDrivesService, RemovableDrivesService>()
					.AddSingleton<INetworkService, NetworkService>()
					.AddSingleton<IStartMenuService, StartMenuService>()
					.AddSingleton<IStorageCacheService, StorageCacheService>()
					.AddSingleton<IIconCacheService, IconCacheService>()
					.AddSingleton<IStorageArchiveService, StorageArchiveService>()
					.AddSingleton<IStorageSecurityService, StorageSecurityService>()
					.AddSingleton<IWindowsCompatibilityService, WindowsCompatibilityService>()
					.AddSingleton</*IVersionControlService,*/ LibGit2Service>()
					// ViewModels
					.AddSingleton<MainPageViewModel>()
					.AddSingleton<InfoPaneViewModel>()
					.AddSingleton<SidebarViewModel>()
					.AddSingleton<DrivesViewModel>()
					.AddSingleton<ShelfViewModel>()
					.AddSingleton<StatusCenterViewModel>()
					.AddSingleton<AppearanceViewModel>()
					.AddSingleton<ToolbarCustomizationViewModel>()
					.AddTransient<HomeViewModel>()
					.AddSingleton<QuickAccessWidgetViewModel>()
					.AddSingleton<DrivesWidgetViewModel>()
					.AddSingleton<NetworkLocationsWidgetViewModel>()
					.AddSingleton<FileTagsWidgetViewModel>()
					.AddSingleton<RecentFilesWidgetViewModel>()
					// Utilities
					.AddSingleton<QuickAccessManager>()
					.AddSingleton<StorageHistoryWrapper>()
					.AddSingleton<FileTagsManager>()
					.AddSingleton<LibraryManager>()
					.AddSingleton(appModel);

			// Conditional DI
			if (AppEnvironment is AppEnvironment.SideloadPreview or AppEnvironment.SideloadStable)
				services.AddSingleton<IUpdateService, SideloadUpdateService>();
			else if (AppEnvironment is AppEnvironment.StorePreview or AppEnvironment.StoreStable)
				services.AddSingleton<IUpdateService, StoreUpdateService>();
			else
				services.AddSingleton<IUpdateService, DummyUpdateService>();

			return services.BuildServiceProvider();
		}

		/// <summary>
		/// Saves the open tabs and selected index to the shared settings.
		/// </summary>
		public static void SaveSessionTabs()
		{
			var userSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();

			userSettingsService.GeneralSettingsService.LastSessionTabList = MainPageViewModel.AppInstances.DefaultIfEmpty().Select(tab =>
			{
				if (tab is not null && tab.NavigationParameter is not null)
				{
					return tab.NavigationParameter.Serialize();
				}
				else
				{
					return "";
				}
			})
			.ToList();

			userSettingsService.GeneralSettingsService.LastSessionSelectedTabIndex = App.AppModel.TabStripSelectedIndex;
		}

		/// <summary>
		/// Shows exception on the Debug Output and sends Toast Notification to the Windows Notification Center.
		/// </summary>
		public static void HandleAppUnhandledException(Exception? ex, bool showToastNotification, string? unhandledMessage = null)
		{
			try
			{
				StringBuilder formattedException = new()
				{
					Capacity = 200
				};

				formattedException.AppendLine("--------- UNHANDLED EXCEPTION ---------");

				if (ex is not null)
				{
					formattedException.AppendLine($">>>> HRESULT: {ex.HResult}");

					if (unhandledMessage is not null)
					{
						formattedException.AppendLine("--- UNHANDLED MESSAGE ---");
						formattedException.AppendLine(unhandledMessage);
					}

					if (ex.Message is not null)
					{
						formattedException.AppendLine("--- MESSAGE ---");
						formattedException.AppendLine(ex.Message);
					}
					if (ex.StackTrace is not null)
					{
						formattedException.AppendLine("--- STACKTRACE ---");
						formattedException.AppendLine(ex.StackTrace);
					}
					if (ex.Source is not null)
					{
						formattedException.AppendLine("--- SOURCE ---");
						formattedException.AppendLine(ex.Source);
					}
					if (ex.InnerException is not null)
					{
						formattedException.AppendLine("--- INNER ---");
						formattedException.AppendLine(ex.InnerException.ToString());
					}
				}
				else
				{
					formattedException.AppendLine("Exception data is not available.");
				}

				formattedException.AppendLine("---------------------------------------");

				Debug.WriteLine(formattedException.ToString());

				// Please check "Output Window" for exception details (View -> Output Window) (CTRL + ALT + O)
				Debugger.Break();

				// Save the current tab list in case it was overwriten by another instance
				SafetyExtensions.IgnoreExceptions(SaveSessionTabs);
				SafetyExtensions.IgnoreExceptions(() => App.Logger?.LogError(ex, ex?.Message ?? "An unhandled error occurred."));

				if (!showToastNotification)
					return;

				SafetyExtensions.IgnoreExceptions(AppToastNotificationHelper.ShowUnhandledExceptionToast);

				SafetyExtensions.IgnoreExceptions(() =>
				{
					// Restart the app
					var userSettingsService = Ioc.Default.GetService<IUserSettingsService>();
					if (userSettingsService is null)
						return;

					var lastSessionTabList = userSettingsService.GeneralSettingsService.LastSessionTabList;

					if (lastSessionTabList is null ||
						userSettingsService.GeneralSettingsService.LastCrashedTabList?.SequenceEqual(lastSessionTabList) is true)
					{
						// Avoid infinite restart loop
						userSettingsService.GeneralSettingsService.LastSessionTabList = null;
					}
					else
					{
						userSettingsService.AppSettingsService.RestoreTabsOnStartup = true;
						userSettingsService.GeneralSettingsService.LastCrashedTabList = lastSessionTabList;

						// Try to re-launch and start over
						MainWindow.Instance.DispatcherQueue.EnqueueOrInvokeAsync(async () =>
						{
							await Launcher.LaunchUriAsync(new Uri("files-dev:"));
						})
						.Wait(100);
					}
				});
			}
			catch
			{
				// Swallow any exception escaping the handler so it can't re-enter
				// Application.UnhandledException before the process terminates.
			}
			finally
			{
				Environment.Exit(ex?.HResult ?? 1);
			}
		}

	}
}
