#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Graphics;
using OpenRA.Mods.Mobius.Terrain;

namespace OpenRA.Mods.Mobius.UtilityCommands
{
	class DumpTilesetSheetsCommand : IUtilityCommand
	{
		static readonly int[] ChannelMasks = { 2, 1, 0, 3 };

		string IUtilityCommand.Name => "--remaster-dump-tileset-sheets";

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return args.Length >= 3;
		}

		[Desc("PALETTE", "TILESET-OR-MAP", "Exports tileset texture atlas as a set of png images.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			var modData = Game.ModData = utility.ModData;

			var palette = new ImmutablePalette(args[1], new[] { 0 }, Array.Empty<int>());

			var remasterContent = modData.Manifest.Get<RemasterModContent>();
			if (!remasterContent.TryMountPackages(modData))
			{
				Console.WriteLine("Failed to mount remaster content");
				Environment.Exit(1);
			}

			if (!modData.DefaultTerrainInfo.TryGetValue(args[2], out var terrainInfo))
				throw new InvalidOperationException($"{args[2]} is not a valid tileset");

			var tileCache = new RemasterTileCache(terrainInfo as RemasterTerrain);
			var count = 0;

			var sb = tileCache.SpriteCache.SheetBuilders[SheetType.Indexed];
			foreach (var s in sb.AllSheets)
			{
				var max = s == sb.Current ? (int)sb.CurrentChannel + 1 : 4;
				for (var i = 0; i < max; i++)
					s.AsPng((TextureChannel)ChannelMasks[i], palette).Save($"{count}.{i}.png");

				count++;
			}

			sb = tileCache.SpriteCache.SheetBuilders[SheetType.BGRA];
			foreach (var s in sb.AllSheets)
				s.AsPng().Save($"{count++}.png");

			Console.WriteLine("Saved [0..{0}].png", count - 1);
		}
	}
}
