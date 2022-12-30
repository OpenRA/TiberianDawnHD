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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;

namespace OpenRA.Mods.Cnc.Graphics
{
	public class RemasterSpriteSequenceLoader : ClassicTilesetSpecificSpriteSequenceLoader
	{
		public readonly string DefaultSpriteExtension = ".shp";
		public readonly Dictionary<string, string> TilesetExtensions = new Dictionary<string, string>();
		public readonly Dictionary<string, string> TilesetCodes = new Dictionary<string, string>();
		public readonly float ClassicUpscaleFactor = 5.333333f;

		public RemasterSpriteSequenceLoader(ModData modData)
			: base(modData)
		{
			var metadata = modData.Manifest.Get<SpriteSequenceFormat>().Metadata;
			if (metadata.TryGetValue("DefaultSpriteExtension", out var yaml))
				DefaultSpriteExtension = yaml.Value;

			if (metadata.TryGetValue("TilesetExtensions", out yaml))
				TilesetExtensions = yaml.ToDictionary(kv => kv.Value);

			if (metadata.TryGetValue("TilesetCodes", out yaml))
				TilesetCodes = yaml.ToDictionary(kv => kv.Value);
		}

		public override ISpriteSequence CreateSequence(ModData modData, string tileset, SpriteCache cache, string image, string sequence, MiniYaml data, MiniYaml defaults)
		{
			return new RemasterSpriteSequence(modData, tileset, cache, this, image, sequence, data, defaults);
		}
	}

	[Desc("A sprite sequence that can have tileset-specific variants and has the oddities " +
	      "that come with first-generation Westwood titles.")]
	public class RemasterSpriteSequence : ClassicTilesetSpecificSpriteSequence
	{
		[Desc("Dictionary of <string: string> with tileset name to override -> tileset name to use instead.")]
		static readonly SpriteSequenceField<Dictionary<string, string>> TilesetOverrides = new SpriteSequenceField<Dictionary<string, string>>(nameof(TilesetOverrides), null);

		[Desc("Use `TilesetCodes` as defined in `mod.yaml` to add a letter as a second character " +
		      "into the sprite filename like the Westwood 2.5D titles did for tileset-specific variants.")]
		static readonly SpriteSequenceField<bool> UseTilesetCode = new SpriteSequenceField<bool>(nameof(UseTilesetCode), false);

		[Desc("Append a tileset-specific extension to the file name " +
		      "- either as defined in `mod.yaml`'s `TilesetExtensions` (if `UseTilesetExtension` is used) " +
		      "or the default hardcoded one for this sequence type (.shp).")]
		static readonly SpriteSequenceField<bool> AddExtension = new SpriteSequenceField<bool>(nameof(AddExtension), true);

		[Desc("Whether `mod.yaml`'s `TilesetExtensions` should be used with the sequence's file name.")]
		static readonly SpriteSequenceField<bool> UseTilesetExtension = new SpriteSequenceField<bool>(nameof(UseTilesetExtension), false);

		[Desc("File name of the remastered sprite to use for this sequence.")]
		static readonly SpriteSequenceField<string> RemasteredFilename = new SpriteSequenceField<string>(nameof(RemasteredFilename), null);

		[Desc("Dictionary of <tileset name>: filename to override the RemasteredFilename key.")]
		static readonly SpriteSequenceField<Dictionary<string, string>> RemasteredTilesetFilenames = new SpriteSequenceField<Dictionary<string, string>>(nameof(RemasteredTilesetFilenames), null);

		[Desc("File name pattern to build the remastered sprite to use for this sequence.")]
		static readonly SpriteSequenceField<string> RemasteredFilenamePattern = new SpriteSequenceField<string>(nameof(RemasteredFilenamePattern), null);

		[Desc("Dictionary of <tileset name>: <filename pattern> to override the RemasteredFilenamePattern key.")]
		static readonly SpriteSequenceField<Dictionary<string, string>> RemasteredTilesetFilenamesPattern = new SpriteSequenceField<Dictionary<string, string>>(nameof(RemasteredTilesetFilenamesPattern), null);

		[Desc("Change the position in-game on X, Y, Z.")]
		protected static readonly SpriteSequenceField<float3> RemasteredOffset = new SpriteSequenceField<float3>(nameof(RemasteredOffset), float3.Zero);

		static readonly int[] FirstFrame = { 0 };

		bool hasRemasteredSprite = true;

		public RemasterSpriteSequence(ModData modData, string tileset, SpriteCache cache, ISpriteSequenceLoader loader, string image, string sequence, MiniYaml data, MiniYaml defaults)
			: base(modData, tileset, cache, loader, image, sequence, data, defaults) { }

		string ResolveTilesetId(string tileset, MiniYaml data, MiniYaml defaults)
		{
			var yaml = data.Nodes.FirstOrDefault(n => n.Key == TilesetOverrides.Key) ?? defaults?.Nodes.FirstOrDefault(n => n.Key == TilesetOverrides.Key);
			if (yaml != null)
			{
				var tsNode = yaml.Value.Nodes.FirstOrDefault(n => n.Key == tileset);
				if (tsNode != null)
					return tsNode.Value.Value;
			}

			return tileset;
		}

		string GetSpriteSrc(string tileset, string spriteName, MiniYaml data, MiniYaml defaults)
		{
			var loader = (RemasterSpriteSequenceLoader)Loader;

			if (LoadField(UseTilesetCode, data, defaults))
			{
				if (loader.TilesetCodes.TryGetValue(ResolveTilesetId(tileset, data, defaults), out var code))
					spriteName = spriteName.Substring(0, 1) + code + spriteName.Substring(2, spriteName.Length - 2);
			}

			if (LoadField(AddExtension, data, defaults))
			{
				var useTilesetExtension = LoadField(UseTilesetExtension, data, defaults);

				if (useTilesetExtension && loader.TilesetExtensions.TryGetValue(ResolveTilesetId(tileset, data, defaults), out var tilesetExtension))
					return spriteName + tilesetExtension;

				return spriteName + loader.DefaultSpriteExtension;
			}

			return spriteName;
		}

		protected override void ReserveSprites(ModData modData, string tileset, SpriteCache cache, MiniYaml data, MiniYaml defaults)
		{
			var frames = LoadField(Frames, data, defaults);
			var flipX = LoadField(FlipX, data, defaults);
			var flipY = LoadField(FlipY, data, defaults);
			var zRamp = LoadField(ZRamp, data, defaults);
			var offset = LoadField(Offset, data, defaults);
			var remasteredOffset = LoadField(RemasteredOffset, data, defaults);
			var blendMode = LoadField(BlendMode, data, defaults);

			var combineNode = data.Nodes.FirstOrDefault(n => n.Key == Combine.Key);
			if (combineNode != null)
			{
				for (var i = 0; i < combineNode.Value.Nodes.Count; i++)
				{
					var subData = combineNode.Value.Nodes[i].Value;
					var subOffset = LoadField(Offset, subData, NoData);
					var remasteredSubOffset = LoadField(RemasteredOffset, subData, NoData);
					var subFlipX = LoadField(FlipX, subData, NoData);
					var subFlipY = LoadField(FlipY, subData, NoData);
					var subFrames = LoadField(Frames, data);

					foreach ((var subFilename, var subLoadFrames, var subUseFrames, var subLocation) in ParseRemasterCombineFilenames(tileset, subFrames, combineNode.Value.Nodes[i].Key, subData))
					{
						spritesToLoad.Add(new SpriteReservation
						{
							Token = cache.ReserveSprites(subFilename, subLoadFrames, subLocation),
							Offset = hasRemasteredSprite ? remasteredSubOffset + remasteredOffset : subOffset + offset,
							FlipX = subFlipX ^ flipX,
							FlipY = subFlipY ^ flipY,
							BlendMode = blendMode,
							ZRamp = zRamp,
							Frames = subUseFrames
						});
					}
				}
			}
			else
			{
				foreach ((var filename, var loadFrames, var useFrames, var location) in ParseRemasterFilenames(tileset, frames, data, defaults))
				{
					spritesToLoad.Add(new SpriteReservation
					{
						Token = cache.ReserveSprites(filename, loadFrames, location),
						Offset = hasRemasteredSprite ? remasteredOffset : offset,
						FlipX = flipX,
						FlipY = flipY,
						BlendMode = blendMode,
						ZRamp = zRamp,
						Frames = useFrames,
					});
				}
			}
		}

		IEnumerable<(string Filename, int[] loadFrames, int[] useFrames, MiniYamlNode.SourceLocation location)> ParseRemasterFilenames(string tileset, int[] frames, MiniYaml data, MiniYaml defaults)
		{
			string filename = null;
			MiniYamlNode.SourceLocation location = default;
			var remasteredTilesetFilenamesPatternNode = data.Nodes.FirstOrDefault(n => n.Key == RemasteredTilesetFilenamesPattern.Key) ?? defaults.Nodes.FirstOrDefault(n => n.Key == RemasteredTilesetFilenamesPattern.Key);
			if (!string.IsNullOrEmpty(remasteredTilesetFilenamesPatternNode?.Value.Value))
			{
				var tilesetNode = remasteredTilesetFilenamesPatternNode.Value.Nodes.FirstOrDefault(n => n.Key == tileset);
				if (tilesetNode != null)
				{
					var patternStart = LoadField("Start", 0, tilesetNode.Value);
					var patternCount = LoadField("Count", 1, tilesetNode.Value);

					return Enumerable.Range(patternStart, patternCount)
						.Select(i => (string.Format(tilesetNode.Value.Value, i), FirstFrame, FirstFrame, tilesetNode.Location));
				}
			}

			var remasteredTilesetFilenamesNode = data.Nodes.FirstOrDefault(n => n.Key == RemasteredTilesetFilenames.Key) ?? defaults.Nodes.FirstOrDefault(n => n.Key == RemasteredTilesetFilenames.Key);
			if (!string.IsNullOrEmpty(remasteredTilesetFilenamesNode?.Value.Value))
			{
				var tilesetNode = remasteredTilesetFilenamesNode.Value.Nodes.FirstOrDefault(n => n.Key == tileset);
				if (tilesetNode != null)
				{
					filename = tilesetNode.Value.Value;
					location = tilesetNode.Location;
				}
			}
			else
			{
				var remasteredFilenamePatternNode = data.Nodes.FirstOrDefault(n => n.Key == RemasteredFilenamePattern.Key) ?? defaults.Nodes.FirstOrDefault(n => n.Key == RemasteredFilenamePattern.Key);
				if (!string.IsNullOrEmpty(remasteredFilenamePatternNode?.Value.Value))
				{
					var patternStart = LoadField("Start", 0, remasteredFilenamePatternNode.Value);
					var patternCount = LoadField("Count", 1, remasteredFilenamePatternNode.Value);

					return Enumerable.Range(patternStart, patternCount)
						.Select(i => (string.Format(remasteredFilenamePatternNode.Value.Value, i), FirstFrame, FirstFrame, remasteredFilenamePatternNode.Location));
				}
			}

			filename = filename ?? LoadField(RemasteredFilename, data, defaults, out location);
			if (filename != null)
			{
				// Only request the subset of frames that we actually need
				int[] loadFrames = null;
				if (length != null)
				{
					loadFrames = CalculateFrameIndices(start, length.Value, stride ?? length.Value, facings, frames, transpose, reverseFacings);
					if (shadowStart >= 0)
						loadFrames = loadFrames.Concat(loadFrames.Select(i => i + shadowStart - start)).ToArray();
				}

				return new[] { (filename, loadFrames, frames, location) };
			}
			else
			{
				hasRemasteredSprite = false;
				filename = GetSpriteSrc(tileset, data?.Value ?? defaults?.Value ?? image, data, defaults);
				location = (data.Nodes.FirstOrDefault() ?? defaults.Nodes.FirstOrDefault())?.Location ?? default;
				int[] loadFrames = null;

				// Only request the subset of frames that we actually need
				if (length != null)
				{
					loadFrames = CalculateFrameIndices(start, length.Value, stride ?? length.Value, facings, frames, transpose, reverseFacings);
					if (shadowStart >= 0)
						loadFrames = loadFrames.Concat(loadFrames.Select(i => i + shadowStart - start)).ToArray();
				}

				return new[] { (filename, loadFrames, frames, location) };
			}
		}

		IEnumerable<(string Filename, int[] loadFrames, int[] useFrames, MiniYamlNode.SourceLocation location)> ParseRemasterCombineFilenames(string tileset, int[] frames, string key, MiniYaml data)
		{
			string filename = null;
			MiniYamlNode.SourceLocation location = default;

			var node = data.Nodes.FirstOrDefault(n => n.Key == RemasteredTilesetFilenames.Key);
			if (node != null)
			{
				var tilesetNode = node.Value.Nodes.FirstOrDefault(n => n.Key == tileset);
				if (tilesetNode != null)
				{
					filename = tilesetNode.Value.Value;
					location = tilesetNode.Location;
				}
			}

			filename = filename ?? LoadField(RemasteredFilename, data, null, out location);
			if (frames == null)
			{
				if (LoadField<string>("Length", null, data) != "*")
				{
					var subStart = LoadField("Start", 0, data);
					var subLength = LoadField("Length", 1, data);
					frames = Exts.MakeArray(subLength, i => subStart + i);
				}
			}

			if (filename != null)
			{
				yield return (filename, frames, frames, location);
				yield break;
			}

			hasRemasteredSprite = false;
			filename = GetSpriteSrc(tileset, key, data, data);
			location = data.Nodes.FirstOrDefault()?.Location ?? default;

			if (frames == null)
			{
				if (LoadField<string>("Length", null, data) != "*")
				{
					var subStart = LoadField("Start", 0, data);
					var subLength = LoadField("Length", 1, data);
					frames = Exts.MakeArray(subLength, i => subStart + i);
				}
			}

			yield return (filename, frames, frames, location);
		}

		protected override float GetScale()
		{
			if (!hasRemasteredSprite)
				return ((RemasterSpriteSequenceLoader)Loader).ClassicUpscaleFactor * scale;

			return scale;
		}
	}
}
