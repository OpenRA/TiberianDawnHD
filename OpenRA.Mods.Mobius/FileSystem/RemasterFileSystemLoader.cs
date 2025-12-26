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

using System.Collections.Immutable;

namespace OpenRA.Mods.Mobius.FileSystem
{
	public class RemasterFileSystemLoader : ContentSourcesFileSystem
	{
		protected override bool InstallContentIfRequired(ModData modData)
		{
			if (base.InstallContentIfRequired(modData))
				return true;

			// Hack the mod manifest to set the requested variant
			static void BodgeManifestPaths(Manifest manifest, string field, ImmutableArray<string> paths) =>
				manifest.GetType().GetField(field)?.SetValue(manifest, paths);

			if (sourceSettings.ContentSource == "remaster")
			{
				BodgeManifestPaths(modData.Manifest, "Voices", [
					"cnchd|audio/voices-base.yaml",
					$"cnchd|audio/voices-{sourceSettings["AudioLanguage"]}-{sourceSettings["AudioStyle"]}.yaml",
				]);

				BodgeManifestPaths(modData.Manifest, "Notifications", [
					"cnchd|audio/notifications-base.yaml",
					$"cnchd|audio/notifications-{sourceSettings["AudioLanguage"]}-{sourceSettings["AudioStyle"]}.yaml",
				]);

				BodgeManifestPaths(modData.Manifest, "Music", [$"cnchd|audio/music-{sourceSettings["MusicStyle"]}.yaml"]);
			}

			if (sourceSettings["ArtworkStyle"] != "remastered")
			{
				var wvs = modData.GetOrCreate<WorldViewportSizes>();
				wvs.GetType().GetField("DefaultScale")?.SetValue(wvs, 1.0f);
			}

			return false;
		}
	}
}
