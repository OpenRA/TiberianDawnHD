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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.FileSystem;
using OpenRA.Mods.Common.Installer;

namespace OpenRA.Mods.Mobius.FileSystem
{
	public class RemasterFileSystemLoader : IFileSystemLoader, IFileSystemExternalContent
	{
		[FieldLoader.Require]
		public readonly string RemasterDataMount = null;
		public readonly string InstallPromptMod = "remaster-content";
		public readonly Dictionary<string, string> SystemPackages = null;
		public readonly Dictionary<string, string> RemasterPackages = null;

		[FieldLoader.LoadUsing(nameof(LoadSources))]
		readonly Dictionary<string, ModContent.ModSource> sources = null;

		static object LoadSources(MiniYaml yaml)
		{
			var ret = new Dictionary<string, ModContent.ModSource>();
			var sourcesNode = yaml.Nodes.Single(n => n.Key == "Sources");
			foreach (var s in sourcesNode.Value.Nodes)
				ret.Add(s.Key, new ModContent.ModSource(s.Value));

			return ret;
		}

		bool contentAvailable;

		public void Mount(Manifest manifest, OpenRA.FileSystem.FileSystem fileSystem, ObjectCreator objectCreator)
		{
			if (SystemPackages != null)
				foreach (var kv in SystemPackages)
					fileSystem.Mount(kv.Key, kv.Value);

			if (RemasterPackages == null)
				return;

			foreach (var kv in sources)
			{
				var sourceResolver = objectCreator.CreateObject<ISourceResolver>($"{kv.Value.Type.Value}SourceResolver");
				var path = sourceResolver.FindSourcePath(kv.Value);
				if (path != null)
				{
					var dataPath = Path.Combine(path, "Data");
					if (!Directory.Exists(dataPath))
						continue;

					contentAvailable = true;
					fileSystem.Mount(dataPath, RemasterDataMount);
					foreach (var p in RemasterPackages)
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
			if (!contentAvailable && Game.Mods.TryGetValue(InstallPromptMod, out var mod))
				Game.InitializeMod(mod, new Arguments());

			return !contentAvailable;
		}

		void IFileSystemExternalContent.ManageContent(ModData modData)
		{
			// Switching mods changes the world state (by disposing it),
			// so we can't do this inside the input handler.
			if (Game.Mods.TryGetValue(InstallPromptMod, out var mod))
				Game.RunAfterTick(() => Game.InitializeMod(mod, new Arguments()));
		}
	}
}
