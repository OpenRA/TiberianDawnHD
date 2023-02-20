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
using OpenRA.Mods.Common.Installer;

namespace OpenRA.Mods.Mobius
{
	public sealed class RemasterModContent : IGlobalModData
	{
		[FieldLoader.Require]
		public readonly string RemasterDataMount = null;
		public readonly string InstallPromptMod = "cnccontent";
		public readonly Dictionary<string, string> Packages;

		[FieldLoader.Ignore]
		readonly Dictionary<string, ModContent.ModSource> sources = new Dictionary<string, ModContent.ModSource>();

		public RemasterModContent(MiniYaml yaml)
		{
			FieldLoader.Load(this, yaml);

			var sourcesNode = yaml.Nodes.Single(n => n.Key == "Sources");
			foreach (var s in sourcesNode.Value.Nodes)
				sources.Add(s.Key, new ModContent.ModSource(s.Value, null));
		}

		public bool TryMountPackagesInner(ModData modData)
		{
			foreach (var kv in sources)
			{
				var sourceResolver = modData.ObjectCreator.CreateObject<ISourceResolver>($"{kv.Value.Type.Value}SourceResolver");
				var path = sourceResolver.FindSourcePath(kv.Value);
				if (path != null)
				{
					modData.ModFiles.Mount(Path.Combine(path, "Data"), RemasterDataMount);
					foreach (var p in Packages)
					{
						var package = modData.ModFiles.OpenPackage(p.Key);
						if (package == null)
							return false;

						modData.ModFiles.Mount(package, p.Value);
					}

					return true;
				}
			}

			return false;
		}

		public bool TryMountPackages(ModData modData)
		{
			if (!TryMountPackagesInner(modData))
			{
				Game.InitializeMod(InstallPromptMod, new Arguments());
				return false;
			}

			return true;
		}
	}
}
