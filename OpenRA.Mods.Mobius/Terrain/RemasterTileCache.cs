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
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.SpriteLoaders;

namespace OpenRA.Mods.Mobius.Terrain
{
	public sealed class RemasterTileCache : IDisposable
	{
		static readonly int[] ChannelsBGRA = { 0, 1, 2, 3 };
		static readonly int[] ChannelsRGBA = { 2, 1, 0, 3 };
		static readonly int[] FirstFrame = { 0 };

		readonly Dictionary<ushort, Dictionary<int, Sprite[]>> sprites = new();
		readonly Dictionary<ushort, float> scale = new();
		public SpriteCache SpriteCache { get; }
		Sprite BlankSprite { get; }

		Sprite GenerateCompositeFrame(string path)
		{
			var frames = FrameLoader.GetFrames(Game.ModData.DefaultFileSystem, path, Game.ModData.SpriteLoaders, out var allMetadata);
			var frame = frames[0];

			// Composite frames are assembled at runtime from one or more source images
			var metadata = allMetadata?.GetOrDefault<PngSheetMetadata>();
			if (frame.Type != SpriteFrameType.Indexed8 || metadata == null)
				throw new InvalidDataException($"{path} is not a valid composite tile template.");

			var data = new byte[frame.Size.Width * frame.Size.Height * 4];
			for (var i = 0; i < frames.Length; i++)
			{
				if (!metadata.Metadata.TryGetValue($"SourceFilename[{i}]", out var sf))
					throw new InvalidDataException($"{path}: SourceFilename[{i}] not defined.");

				ISpriteFrame overlay;
				try
				{
					overlay = FrameLoader.GetFrames(Game.ModData.DefaultFileSystem, sf, Game.ModData.SpriteLoaders, out _)[0];
				}
				catch (FileNotFoundException)
				{
					throw new InvalidDataException($"{path}: SourceFilename[{i}]={sf} does not exist.");
				}

				if (overlay.Type != SpriteFrameType.Bgra32 && overlay.Type != SpriteFrameType.Rgba32)
					throw new InvalidDataException($"{path}: SourceFilename[{i}] is not a 32 bit sprite.");

				var channels = overlay.Type == SpriteFrameType.Bgra32 ? ChannelsBGRA : ChannelsRGBA;
				for (var y = 0; y < frame.Size.Height; y++)
				{
					var o = 4 * y * frame.Size.Width;
					for (var x = 0; x < frame.Size.Width; x++)
					{
						var maskAlpha = frames[i].Data[y * frame.Size.Width + x];
						for (var j = 0; j < 4; j++)
						{
							// Note: we want to pre-multiply the colour channels by the alpha channel,
							// but not the alpha channel itself. The simplest way to do this is to
							// always include the overlay alpha in the alpha component, and
							// special-case the alpha's channel value instead.
							var overlayAlpha = overlay.Data[o + 4 * x + 3] * maskAlpha;
							var overlayChannel = j < 3 ? overlay.Data[o + 4 * x + channels[j]] : 255;

							// Base channels have already been pre-multiplied by alpha
							var baseAlpha = 65205 - overlayAlpha;
							var baseChannel = data[o + 4 * x + j];

							// Apply mask and pre-multiply alpha
							data[o + 4 * x + j] = (byte)((overlayChannel * overlayAlpha + baseChannel * baseAlpha) / 65205);
						}
					}
				}
			}

			var sc = SpriteCache.SheetBuilders[SheetType.BGRA].Allocate(frame.Size, 1f, frame.Offset);
			Util.FastCopyIntoChannel(sc, data, SpriteFrameType.Bgra32);
			return sc;
		}

		public RemasterTileCache(RemasterTerrain terrainInfo)
		{
			SpriteCache = new SpriteCache(Game.ModData.DefaultFileSystem, Game.ModData.SpriteLoaders, terrainInfo.BgraSheetSize, terrainInfo.IndexedSheetSize, 0);

			var blankToken = SpriteCache.ReserveSprites(terrainInfo.BlankTile, FirstFrame, default);

			var remasteredSpriteReservations = new Dictionary<ushort, Dictionary<int, int[]>>();
			foreach (var t in terrainInfo.Templates)
			{
				var templateInfo = (RemasterTerrainTemplateInfo)t.Value;
				var templateTokens = new Dictionary<int, int[]>();
				sprites[t.Key] = new Dictionary<int, Sprite[]>();

				if (templateInfo.RemasteredFilenames != null || templateInfo.RemasteredCompositeFilenames != null)
				{
					// Composite frames are built directly, bypassing the frame cache
					if (templateInfo.RemasteredCompositeFilenames != null)
						foreach (var kv in templateInfo.RemasteredCompositeFilenames)
							sprites[t.Key][kv.Key] = kv.Value.Select(GenerateCompositeFrame).ToArray();

					if (templateInfo.RemasteredFilenames != null)
						foreach (var kv in templateInfo.RemasteredFilenames)
							templateTokens[kv.Key] = kv.Value
								.Select(f => SpriteCache.ReserveSprites(f, FirstFrame, default))
								.ToArray();

					scale[t.Key] = 1f;
				}
				else
				{
					for (var i = 0; i < t.Value.TilesCount; i++)
					{
						if (t.Value[i] == null)
							continue;

						templateTokens[i] = new[] { SpriteCache.ReserveSprites(templateInfo.Filename, new[] { i }, default) };
					}

					scale[t.Key] = terrainInfo.ClassicUpscaleFactor;
				}

				remasteredSpriteReservations[t.Key] = templateTokens;
			}

			SpriteCache.LoadReservations(Game.ModData);

			BlankSprite = SpriteCache.ResolveSprites(blankToken).First(s => s != null);
			foreach (var kv in remasteredSpriteReservations)
				foreach (var tokens in kv.Value)
					sprites[kv.Key][tokens.Key] = tokens.Value
						.Select(t => SpriteCache.ResolveSprites(t).FirstOrDefault(s => s != null))
						.ToArray();
		}

		public bool HasTileSprite(TerrainTile r, int frame)
		{
			return TileSprite(r, frame) != BlankSprite;
		}

		public Sprite TileSprite(TerrainTile r, int frame)
		{
			if (!sprites.TryGetValue(r.Type, out var templateSprites))
				return BlankSprite;

			if (!templateSprites.TryGetValue(r.Index, out var tileSprites))
				return BlankSprite;

			return tileSprites[frame % tileSprites.Length];
		}

		public float TileScale(TerrainTile r)
		{
			if (!scale.TryGetValue(r.Type, out var templateScale))
				return 1f;

			return templateScale;
		}

		public Sprite MissingTile => BlankSprite;

		public void Dispose()
		{
			SpriteCache.Dispose();
		}
	}
}
