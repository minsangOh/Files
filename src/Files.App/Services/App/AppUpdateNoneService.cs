
namespace Files.App.Services
{
	internal sealed partial class DummyUpdateService : ObservableObject, IUpdateService
	{
		public bool IsUpdateAvailable => false;

		public bool IsUpdating => false;

		public int UpdateProgress => 0;

		public bool IsAppUpdated => AppLifecycleHelper.IsAppUpdated;

		public new event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }

		public Task CheckAndUpdateFilesLauncherAsync()
		{
			return Task.CompletedTask;
		}

		public Task CheckForUpdatesAsync()
		{
			return Task.CompletedTask;
		}

		public Task DownloadMandatoryUpdatesAsync()
		{
			return Task.CompletedTask;
		}

		public Task DownloadUpdatesAsync()
		{
			return Task.CompletedTask;
		}
	}
}
