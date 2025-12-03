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

namespace OpenRA.Mods.Mobius
{
	public class RemasterModSettings
	{
		[FieldLoader.Ignore]
		readonly string settingsPath;

		public string ContentSource = null;

		public RemasterModSettings(string path)
		{
			settingsPath = path;
			try
			{
				FieldLoader.Load(this, new MiniYaml("", MiniYaml.FromFile(path)));
			}
			catch { }
		}

		public void Save()
		{
			var builder = new List<MiniYamlNodeBuilder>
			{
				new("ContentSource", new MiniYamlBuilder(ContentSource, []))
			};

			builder.WriteToFile(settingsPath);
		}
	}
}
