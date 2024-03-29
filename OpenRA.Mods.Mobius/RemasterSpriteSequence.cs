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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Cnc.Graphics
{
	public class RemasterSpriteSequenceLoader : ClassicTilesetSpecificSpriteSequenceLoader
	{
		public readonly float ClassicUpscaleFactor = 5.333333f;

		public RemasterSpriteSequenceLoader(ModData modData)
			: base(modData) { }

		public override ISpriteSequence CreateSequence(ModData modData, string tileset, SpriteCache cache, string image, string sequence, MiniYaml data, MiniYaml defaults)
		{
			return new RemasterSpriteSequence(cache, this, image, sequence, data, defaults);
		}
	}

	[Desc("A sprite sequence that can have tileset-specific variants and has the oddities " +
	      "that come with first-generation Westwood titles.")]
	public class RemasterSpriteSequence : ClassicTilesetSpecificSpriteSequence
	{
		[Desc("File name of the remastered sprite to use for this sequence.")]
		static readonly SpriteSequenceField<string> RemasteredFilename = new(nameof(RemasteredFilename), null);

		[Desc("Dictionary of <tileset name>: filename to override the RemasteredFilename key.")]
		static readonly SpriteSequenceField<Dictionary<string, string>> RemasteredTilesetFilenames = new(nameof(RemasteredTilesetFilenames), null);

		[Desc("File name pattern to build the remastered sprite to use for this sequence.")]
		static readonly SpriteSequenceField<string> RemasteredFilenamePattern = new(nameof(RemasteredFilenamePattern), null);

		[Desc("Dictionary of <tileset name>: <filename pattern> to override the RemasteredFilenamePattern key.")]
		static readonly SpriteSequenceField<Dictionary<string, string>> RemasteredTilesetFilenamesPattern = new(nameof(RemasteredTilesetFilenamesPattern), null);

		[Desc("File name pattern of the sprite to mask the remastered sprite.")]
		static readonly SpriteSequenceField<string> RemasteredMaskFilename = new(nameof(RemasteredMaskFilename), null);

		[Desc("Change the position in-game on X, Y, Z.")]
		protected static readonly SpriteSequenceField<float3> RemasteredOffset = new(nameof(RemasteredOffset), float3.Zero);

		[Desc("Frame index to start from.")]
		protected static readonly SpriteSequenceField<int?> RemasteredStart = new(nameof(RemasteredStart), null);

		[Desc("Number of frames to use. Does not have to be the total amount the sprite sheet has.")]
		protected static readonly SpriteSequenceField<int?> RemasteredLength = new(nameof(RemasteredLength), null);

		[Desc("Time (in milliseconds at default game speed) to wait until playing the next frame in the animation.")]
		protected static readonly SpriteSequenceField<int?> RemasteredTick = new(nameof(RemasteredTick), null);

		[Desc("Adjusts the rendered size of the sprite")]
		protected static readonly SpriteSequenceField<float?> RemasteredScale = new(nameof(RemasteredScale), null);

		[Desc("Sprite data is already pre-multiplied by alpha channel.")]
		protected static readonly SpriteSequenceField<bool> RemasteredPremultiplied = new(nameof(RemasteredPremultiplied), true);

		static readonly int[] FirstFrame = { 0 };

		bool hasRemasteredSprite = true;

		IEnumerable<ReservationInfo> ParseRemasterFilenames(ModData modData, string tileset, int[] frames, MiniYaml data, MiniYaml defaults)
		{
			string filename = null;
			MiniYamlNode.SourceLocation location = default;
			var remasteredTilesetFilenamesPatternNode = data.Nodes.FirstOrDefault(n => n.Key == RemasteredTilesetFilenamesPattern.Key) ?? defaults.Nodes.FirstOrDefault(n => n.Key == RemasteredTilesetFilenamesPattern.Key);
			if (remasteredTilesetFilenamesPatternNode != null)
			{
				var tilesetNode = remasteredTilesetFilenamesPatternNode.Value.Nodes.FirstOrDefault(n => n.Key == tileset);
				if (tilesetNode != null)
				{
					var patternStart = LoadField("Start", 0, tilesetNode.Value);
					var patternCount = LoadField("Count", 1, tilesetNode.Value);

					return Enumerable.Range(patternStart, patternCount).Select(i =>
						new ReservationInfo(string.Format(tilesetNode.Value.Value, i), FirstFrame, FirstFrame, tilesetNode.Location));
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

					return Enumerable.Range(patternStart, patternCount).Select(i =>
						new ReservationInfo(string.Format(remasteredFilenamePatternNode.Value.Value, i),
						FirstFrame, FirstFrame, remasteredFilenamePatternNode.Location));
				}
			}

			filename ??= LoadField(RemasteredFilename, data, defaults, out location);
			if (filename != null)
			{
				// Only request the subset of frames that we actually need
				var loadFrames = CalculateFrameIndices(start, length, stride ?? length ?? 0, facings, frames, transpose, reverseFacings, shadowStart);
				return new[] { new ReservationInfo(filename, loadFrames, frames, location) };
			}
			else
			{
				hasRemasteredSprite = false;
				return ParseFilenames(modData, tileset, frames, data, defaults);
			}
		}

		IEnumerable<ReservationInfo> ParseRemasterCombineFilenames(ModData modData, string tileset, int[] frames, MiniYaml data)
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

			filename ??= LoadField(RemasteredFilename, data, null, out location);
			if (frames == null && LoadField<string>("Length", null, data) != "*")
			{
				var subStart = LoadField("Start", 0, data);
				var subLength = LoadField("Length", 1, data);
				frames = Exts.MakeArray(subLength, i => subStart + i);
			}

			if (filename != null)
			{
				return new[] { new ReservationInfo(filename, frames, frames, location) };
			}
			else
			{
				hasRemasteredSprite = false;
				return ParseCombineFilenames(modData, tileset, frames, data);
			}
		}

		public RemasterSpriteSequence(SpriteCache cache, ISpriteSequenceLoader loader, string image, string sequence, MiniYaml data, MiniYaml defaults)
			: base(cache, loader, image, sequence, data, defaults)
		{
			start = LoadField(RemasteredStart, data, defaults) ?? start;
			tick = LoadField(RemasteredTick, data, defaults) ?? tick;
			scale = LoadField(RemasteredScale, data, defaults) ?? scale;

			if (LoadField<string>(RemasteredLength.Key, null, data, defaults) != "*")
				length = LoadField(RemasteredLength, data, defaults) ?? length;
			else
				length = null;
		}

		int? remasteredMaskToken;

		public override void ReserveSprites(ModData modData, string tileset, SpriteCache cache, MiniYaml data, MiniYaml defaults)
		{
			var frames = LoadField(Frames, data, defaults);
			var flipX = LoadField(FlipX, data, defaults);
			var flipY = LoadField(FlipY, data, defaults);
			var zRamp = LoadField(ZRamp, data, defaults);
			var offset = LoadField(Offset, data, defaults);
			var remasteredOffset = LoadField(RemasteredOffset, data, defaults);
			var remasteredMaskFilename = LoadField(RemasteredMaskFilename, data, defaults, out var remasteredMaskFilenameLocation);
			var blendMode = LoadField(BlendMode, data, defaults);
			var premultiplied = LoadField(RemasteredPremultiplied, data, defaults);

			if (!string.IsNullOrEmpty(remasteredMaskFilename))
				remasteredMaskToken = cache.ReserveFrames(remasteredMaskFilename, null, remasteredMaskFilenameLocation);

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

					foreach (var f in ParseRemasterCombineFilenames(modData, tileset, subFrames, subData))
					{
						int token;
						if (remasteredMaskToken != null)
							token = cache.ReserveFrames(f.Filename, f.LoadFrames, f.Location);
						else
							token = cache.ReserveSprites(f.Filename, f.LoadFrames, f.Location, hasRemasteredSprite && premultiplied);

						spritesToLoad.Add(new SpriteReservation
						{
							Token = token,
							Offset = hasRemasteredSprite ? remasteredSubOffset + remasteredOffset : subOffset + offset,
							FlipX = subFlipX ^ flipX,
							FlipY = subFlipY ^ flipY,
							BlendMode = blendMode,
							ZRamp = zRamp,
							Frames = f.Frames
						});
					}
				}
			}
			else
			{
				foreach (var f in ParseRemasterFilenames(modData, tileset, frames, data, defaults))
				{
					int token;
					if (remasteredMaskToken != null)
						token = cache.ReserveFrames(f.Filename, f.LoadFrames, f.Location);
					else
						token = cache.ReserveSprites(f.Filename, f.LoadFrames, f.Location, hasRemasteredSprite && premultiplied);

					spritesToLoad.Add(new SpriteReservation
					{
						Token = token,
						Offset = hasRemasteredSprite ? remasteredOffset : offset,
						FlipX = flipX,
						FlipY = flipY,
						BlendMode = blendMode,
						ZRamp = zRamp,
						Frames = f.Frames,
					});
				}
			}
		}

		public override void ResolveSprites(SpriteCache cache)
		{
			if (bounds != null)
				return;

			Sprite depthSprite = null;
			if (depthSpriteReservation != null)
				depthSprite = cache.ResolveSprites(depthSpriteReservation.Value).First(s => s != null);

			Sprite[] allSprites;
			if (remasteredMaskToken != null)
			{
				var maskFrames = cache.ResolveFrames(remasteredMaskToken.Value);
				var allFrames = spritesToLoad.SelectMany<SpriteReservation, (SpriteReservation, ISpriteFrame)>(r =>
				{
					var resolved = cache.ResolveFrames(r.Token);
					if (r.Frames != null)
						return r.Frames.Select(f => (r, resolved[f]));

					return resolved.Select(f => (r, f));
				}).ToArray();

				var frameLength = length ?? allFrames.Length - start;
				if (maskFrames.Length != frameLength)
					throw new YamlException($"Sequence {image}.{Name} with {frameLength} frames cannot use mask with {maskFrames.Length} frames.");

				allSprites = new Sprite[allFrames.Length];
				for (var i = 0; i < frameLength; i++)
				{
					(var r, var frame) = allFrames[start + i];
					var mask = maskFrames[i];
					if (frame.Size != mask.Size)
						throw new YamlException($"Sequence {image}.{Name} frame {i} with size {frame.Size} frames cannot use mask with size {mask.Size}.");

					if (mask.Type != SpriteFrameType.Indexed8)
						throw new YamlException($"Sequence {image}.{Name} mask frame {i} must be an indexed image.");

					var data = new byte[frame.Data.Length];
					var channels = frame.Data.Length / mask.Data.Length;
					for (var j = 0; j < mask.Data.Length; j++)
						if (mask.Data[j] != 0)
							for (var k = 0; k < channels; k++)
								data[j * channels + k] = frame.Data[j * channels + k];

					var s = cache.SheetBuilders[SheetBuilder.FrameTypeToSheetType(frame.Type)]
						.Add(data, frame.Type, frame.Size, 0, frame.Offset);

					var dx = r.Offset.X + (r.FlipX ? -s.Offset.X : s.Offset.X);
					var dy = r.Offset.Y + (r.FlipY ? -s.Offset.Y : s.Offset.Y);
					var dz = r.Offset.Z + s.Offset.Z + r.ZRamp * dy;
					s = new Sprite(s.Sheet, FlipRectangle(s.Bounds, r.FlipX, r.FlipY), r.ZRamp, new float3(dx, dy, dz), s.Channel, r.BlendMode);

					if (depthSprite != null)
					{
						var cw = (depthSprite.Bounds.Left + depthSprite.Bounds.Right) / 2 + (int)(s.Offset.X + depthSpriteOffset.X);
						var ch = (depthSprite.Bounds.Top + depthSprite.Bounds.Bottom) / 2 + (int)(s.Offset.Y + depthSpriteOffset.Y);
						var w = s.Bounds.Width / 2;
						var h = s.Bounds.Height / 2;

						s = new SpriteWithSecondaryData(s, depthSprite.Sheet, Rectangle.FromLTRB(cw - w, ch - h, cw + w, ch + h), depthSprite.Channel);
					}

					allSprites[start + i] = s;
				}
			}
			else
			{
				allSprites = spritesToLoad.SelectMany(r =>
				{
					var resolved = cache.ResolveSprites(r.Token);
					if (r.Frames != null)
						resolved = r.Frames.Select(f => resolved[f]).ToArray();

					return resolved.Select(s =>
					{
						if (s == null)
							return null;

						var dx = r.Offset.X + (r.FlipX ? -s.Offset.X : s.Offset.X);
						var dy = r.Offset.Y + (r.FlipY ? -s.Offset.Y : s.Offset.Y);
						var dz = r.Offset.Z + s.Offset.Z + r.ZRamp * dy;
						var sprite = new Sprite(s.Sheet, FlipRectangle(s.Bounds, r.FlipX, r.FlipY), r.ZRamp, new float3(dx, dy, dz), s.Channel, r.BlendMode);
						if (depthSprite == null)
							return sprite;

						var cw = (depthSprite.Bounds.Left + depthSprite.Bounds.Right) / 2 + (int)(s.Offset.X + depthSpriteOffset.X);
						var ch = (depthSprite.Bounds.Top + depthSprite.Bounds.Bottom) / 2 + (int)(s.Offset.Y + depthSpriteOffset.Y);
						var w = s.Bounds.Width / 2;
						var h = s.Bounds.Height / 2;

						return new SpriteWithSecondaryData(sprite, depthSprite.Sheet, Rectangle.FromLTRB(cw - w, ch - h, cw + w, ch + h), depthSprite.Channel);
					});
				}).ToArray();
			}

			length ??= allSprites.Length - start;

			if (alpha != null)
			{
				if (alpha.Length == 1)
					alpha = Exts.MakeArray(length.Value, _ => alpha[0]);
				else if (alpha.Length != length.Value)
					throw new YamlException($"Sequence {image}.{Name} must define either 1 or {length.Value} Alpha values.");
			}
			else if (alphaFade)
				alpha = Exts.MakeArray(length.Value, i => float2.Lerp(1f, 0f, i / (length.Value - 1f)));

			// Reindex sprites to order facings anti-clockwise and remove unused frames
			var index = CalculateFrameIndices(start, length.Value, stride ?? length.Value, facings, null, transpose, reverseFacings, -1);
			if (reverses)
			{
				index.AddRange(index.Skip(1).Take(length.Value - 2).Reverse());
				length = 2 * length - 2;
			}

			if (index.Count == 0)
				throw new YamlException($"Sequence {image}.{Name} does not define any frames.");

			var minIndex = index.Min();
			var maxIndex = index.Max();
			if (minIndex < 0 || maxIndex >= allSprites.Length)
				throw new YamlException($"Sequence {image}.{Name} uses frames between {minIndex}..{maxIndex}, but only 0..{allSprites.Length - 1} exist.");

			sprites = index.Select(f => allSprites[f]).ToArray();
			if (shadowStart >= 0)
				shadowSprites = index.Select(f => allSprites[f - start + shadowStart]).ToArray();

			bounds = sprites.Concat(shadowSprites ?? Enumerable.Empty<Sprite>()).Select(OffsetSpriteBounds).Union();
		}

		protected override float GetScale()
		{
			if (!hasRemasteredSprite)
				return ((RemasterSpriteSequenceLoader)Loader).ClassicUpscaleFactor * scale;

			return scale;
		}
	}
}
