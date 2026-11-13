// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

namespace Files.App.Data.Items
{
	/// <summary>
	/// Describes a built-in column of <see cref="DetailsLayoutPage"/>.
	/// </summary>
	/// <param name="Id">The column id.</param>
	/// <param name="HeaderResourceKey">The resource key of the header text.</param>
	/// <param name="DefaultWidth">The default width in pixels.</param>
	/// <param name="IsVisibleByDefault">Whether the column is shown by default.</param>
	/// <param name="SortOption">The sort option the header sorts by, or <see langword="null"/> to sort by its property.</param>
	/// <param name="GetText">Gets the cell text, or <see langword="null"/> when the cell is defined in XAML.</param>
	/// <param name="TextProperties">The item properties the cell text depends on.</param>
	/// <param name="GetToolTip">Gets the cell tooltip, or <see langword="null"/> to show the text when it is trimmed.</param>
	public sealed record DetailsLayoutColumnDefinition(
		string Id,
		string HeaderResourceKey,
		double DefaultWidth,
		bool IsVisibleByDefault,
		SortOption? SortOption = null,
		Func<ListedItem, string?>? GetText = null,
		string[]? TextProperties = null,
		Func<ListedItem, string?>? GetToolTip = null,
		double MinWidth = 50,
		double MaxWidth = 800,
		bool IsResizable = true);

	/// <summary>
	/// Provides the built-in columns of <see cref="DetailsLayoutPage"/>.
	/// </summary>
	public static class DetailsLayoutColumnDefinitions
	{
		/// <summary>
		/// Gets the columns Files draws itself, in their default display order. Windows property columns aren't listed; they're added at runtime.
		/// </summary>
		public static IReadOnlyList<DetailsLayoutColumnDefinition> BuiltIn { get; } = new DetailsLayoutColumnDefinition[]
		{
			new(DetailsLayoutColumnIds.Icon, string.Empty, 24, true, IsResizable: false),
			new(DetailsLayoutColumnIds.Name, Strings.Name, 240, true, SortOption.Name, MaxWidth: 1000),
			new(DetailsLayoutColumnIds.Tag, Strings.Tag, 140, true, SortOption.FileTag),
			new(DetailsLayoutColumnIds.Path, Strings.Path, 200, true, SortOption.Path,
				item => item.ItemPath, [nameof(ListedItem.ItemPath)], MaxWidth: 500),
			new(DetailsLayoutColumnIds.OriginalPath, Strings.OriginalPath, 200, false, SortOption.OriginalFolder,
				item => (item as RecycleBinItem)?.ItemOriginalPath, [nameof(RecycleBinItem.ItemOriginalPath)], MaxWidth: 500),
			new(DetailsLayoutColumnIds.DateDeleted, Strings.DateDeleted, 200, false, SortOption.DateDeleted,
				item => (item as RecycleBinItem)?.ItemDateDeleted, [nameof(RecycleBinItem.ItemDateDeleted)]),
			new(DetailsLayoutColumnIds.DateModified, Strings.DateModifiedLowerCase, 200, true, SortOption.DateModified,
				item => item.ItemDateModified, [nameof(ListedItem.ItemDateModified)]),
			new(DetailsLayoutColumnIds.DateCreated, Strings.DateCreated, 200, false, SortOption.DateCreated,
				item => item.ItemDateCreated, [nameof(ListedItem.ItemDateCreated)]),
			new(DetailsLayoutColumnIds.ItemType, Strings.Type, 140, true, SortOption.FileType,
				item => item.ItemType, [nameof(ListedItem.ItemType)]),
			new(DetailsLayoutColumnIds.Size, Strings.Size, 100, true, SortOption.Size),
			new(DetailsLayoutColumnIds.Status, Strings.Status, 50, true, SortOption.SyncStatus, MaxWidth: 80),
		};

		private static readonly Dictionary<string, DetailsLayoutColumnDefinition> _definitions = BuiltIn.ToDictionary(definition => definition.Id);

		public static DetailsLayoutColumnDefinition? Get(string? id)
		{
			return id is not null && _definitions.TryGetValue(id, out var definition) ? definition : null;
		}

		public static bool IsBuiltIn(string? id)
		{
			return id is not null && _definitions.ContainsKey(id);
		}

		public static string GetHeader(string id)
		{
			return Get(id) is { HeaderResourceKey.Length: > 0 } definition ? definition.HeaderResourceKey.GetLocalizedResource() : string.Empty;
		}
	}
}
