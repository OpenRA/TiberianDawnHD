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

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;

namespace OpenRA.Mods.Mobius.Terrain
{
	public sealed class RemasterTileCache : IDisposable
	{
		static readonly int[] FirstFrame = { 0 };

		readonly Dictionary<ushort, Dictionary<int, Sprite[]>> sprites = new Dictionary<ushort, Dictionary<int, Sprite[]>>();
		readonly Dictionary<ushort, float> scale = new Dictionary<ushort, float>();
		readonly SpriteCache spriteCache;
		readonly Sprite blankSprite;

		public SpriteCache SpriteCache => spriteCache;

		public RemasterTileCache(RemasterTerrain terrainInfo)
		{
			spriteCache = new SpriteCache(Game.ModData.DefaultFileSystem, Game.ModData.SpriteLoaders, terrainInfo.BgraSheetSize, terrainInfo.IndexedSheetSize, 0);

			var blankToken = spriteCache.ReserveSprites(terrainInfo.BlankTile, FirstFrame, default);

			var remasteredSpriteReservations = new Dictionary<ushort, Dictionary<int, int[]>>();
			foreach (var t in terrainInfo.Templates)
			{
				var templateInfo = (RemasterTerrainTemplateInfo)t.Value;
				var templateTokens = new Dictionary<int, int[]>();

				if (templateInfo.RemasteredFilenames?.Any() ?? false)
				{
					foreach (var kv in templateInfo.RemasteredFilenames)
						templateTokens[kv.Key] = kv.Value
							.Select(f => spriteCache.ReserveSprites(f, FirstFrame, default))
							.ToArray();
					scale[t.Key] = 1f;
				}
				else
				{
					for (var i = 0; i < t.Value.TilesCount; i++)
					{
						if (t.Value[i] == null)
							continue;

						templateTokens[i] = new[] { spriteCache.ReserveSprites(templateInfo.Filename, new[] { i }, default) };
					}

					scale[t.Key] = terrainInfo.ClassicUpscaleFactor;
				}

				remasteredSpriteReservations[t.Key] = templateTokens;
			}

			spriteCache.LoadReservations(Game.ModData);

			blankSprite = spriteCache.ResolveSprites(blankToken).First(s => s != null);
			foreach (var kv in remasteredSpriteReservations)
			{
				sprites[kv.Key] = new Dictionary<int, Sprite[]>();
				foreach (var tokens in kv.Value)
					sprites[kv.Key][tokens.Key] = tokens.Value
						.Select(t => spriteCache.ResolveSprites(t).FirstOrDefault(s => s != null))
						.ToArray();
			}
		}

		public bool HasTileSprite(TerrainTile r, int frame)
		{
			return TileSprite(r, frame) != blankSprite;
		}

		public Sprite TileSprite(TerrainTile r, int frame)
		{
			if (!sprites.TryGetValue(r.Type, out var templateSprites))
				return blankSprite;

			if (!templateSprites.TryGetValue(r.Index, out var tileSprites))
				return blankSprite;

			return tileSprites[frame % tileSprites.Length];
		}

		public float TileScale(TerrainTile r)
		{
			if (!scale.TryGetValue(r.Type, out var templateScale))
				return 1f;

			return templateScale;
		}

		public Sprite MissingTile => blankSprite;

		public void Dispose()
		{
			spriteCache.Dispose();
		}
	}
}
