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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.FileSystem;
using OpenRA.Mods.Common.Installer;

namespace OpenRA.Mods.Mobius.FileSystem
{
	public class RemasterFileSystemLoader : IFileSystemLoader, IFileSystemExternalContent
	{
		public sealed class ContentSource
		{
			public readonly string SourceMount = null;
			public readonly bool IsRemasteredContent = false;
			public readonly Dictionary<string, string> ContentPackages = null;

			[FieldLoader.LoadUsing(nameof(LoadSources))]
			public readonly Dictionary<string, ModContent.ModSource> Sources = null;

			static object LoadSources(MiniYaml yaml)
			{
				var ret = new Dictionary<string, ModContent.ModSource>();
				var sourcesNode = yaml.Nodes.Single(n => n.Key == "Sources");
				foreach (var s in sourcesNode.Value.Nodes)
					ret.Add(s.Key, new ModContent.ModSource(s.Value));

				return ret;
			}

			public ContentSource(MiniYaml yaml)
			{
				FieldLoader.Load(this, yaml);
			}
		}

		[Desc("Mod to use for content installation.")]
		public readonly string ContentInstallerMod = "remaster-content";

		[Desc("A list of mod-provided packages. Anything required to display the initial load screen must be listed here.")]
		public readonly Dictionary<string, string> SystemPackages = null;

		public bool UseRemasteredContent { get; private set; }

		[FieldLoader.LoadUsing(nameof(LoadContentSources))]
		readonly Dictionary<string, ContentSource> contentSources = null;

		static object LoadContentSources(MiniYaml yaml)
		{
			var ret = new Dictionary<string, ContentSource>();
			var sourcesNode = yaml.Nodes.Single(n => n.Key == "ContentSources");
			foreach (var s in sourcesNode.Value.Nodes)
				ret.Add(s.Key, new ContentSource(s.Value));

			return ret;
		}

		bool contentAvailable;

		public void Mount(OpenRA.FileSystem.FileSystem fileSystem, ObjectCreator objectCreator)
		{
			foreach (var kv in SystemPackages)
				fileSystem.Mount(kv.Key, kv.Value);

			// Hack to get the mod id
			// This will go away as support is merged upstream
			var modID = fileSystem.GetType().GetField("modID", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(fileSystem);
			var settingsPath = Path.Combine(Platform.SupportDir, $"settings.{modID}.yaml");

			var modSettings = new RemasterModSettings(settingsPath);
			if (modSettings.ContentSource == null || !contentSources.TryGetValue(modSettings.ContentSource, out var source))
				return;

			foreach (var kv in source.Sources)
			{
				var sourceResolver = objectCreator.CreateObject<ISourceResolver>($"{kv.Value.Type.Value}SourceResolver");
				var path = sourceResolver.FindSourcePath(kv.Value);
				if (path != null)
				{
					if (!Directory.Exists(path))
						continue;

					contentAvailable = true;
					UseRemasteredContent = source.IsRemasteredContent;
					fileSystem.Mount(path, source.SourceMount);
					foreach (var p in source.ContentPackages)
					{
						var package = fileSystem.OpenPackage(p.Key);
						if (package == null)
						{
							contentAvailable = false;
							continue;
						}

						fileSystem.Mount(package, p.Value);
					}
				}
			}
		}

		bool IFileSystemExternalContent.InstallContentIfRequired(ModData modData)
		{
			if (!contentAvailable)
				Game.InitializeMod(ContentInstallerMod, new Arguments());

			// Hack the mod manifest to restore non-remastered values
			// This will go away as support is merged upstream
			if (!UseRemasteredContent)
			{
				var manifest = Game.ModData.Manifest;
				foreach (var key in (string[])["Voices", "Notifications", "Music"])
				{
					var field = manifest.GetType().GetField(key);
					if (field == null)
						continue;

					var files = (string[])field.GetValue(manifest);
					field.SetValue(manifest, files?.Select(f => f.Replace("cnchd|", "cnc|")).ToArray());
				}

				var wvs = manifest.Get<WorldViewportSizes>();
				wvs.GetType().GetField("DefaultScale")?.SetValue(wvs, 1.0f);
			}

			return !contentAvailable;
		}

		void IFileSystemExternalContent.ManageContent(ModData modData)
		{
			// Switching mods changes the world state (by disposing it),
			// so we can't do this inside the input handler.
			Game.RunAfterTick(() => Game.InitializeMod(ContentInstallerMod, new Arguments()));
		}
	}
}
