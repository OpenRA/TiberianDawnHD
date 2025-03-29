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
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using OpenRA.FileSystem;
using OpenRA.Mods.Common.MapGenerator;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Mods.Mobius.FileSystem;
using OpenRA.Primitives;
using OpenRA.Support;

namespace OpenRA.Mods.Mobius.Terrain
{
	public class RemasterTerrainLoader : ITerrainLoader
	{
		readonly ModData modData;

		public RemasterTerrainLoader(ModData modData)
		{
			this.modData = modData;
		}

		public ITerrainInfo ParseTerrain(IReadOnlyFileSystem fileSystem, string path)
		{
			return new RemasterTerrain(modData, fileSystem, path);
		}
	}

	public class RemasterTerrainTemplateInfo : TerrainTemplateInfo
	{
		public readonly string Filename;
		public readonly Dictionary<int, string[]> RemasteredFilenames;
		public readonly string Palette;

		public RemasterTerrainTemplateInfo(ITerrainInfo terrainInfo, MiniYaml my)
			: base(terrainInfo, my) { }
	}

	public class RemasterTerrain : ITemplatedTerrainInfo, ITerrainInfoNotifyMapCreated
	{
		public readonly string Name;
		public readonly string Id;
		public readonly Size TileSize = new(24, 24);
		public readonly Size RemasteredTileSize = new(128, 128);
		public readonly int BgraSheetSize = 4096;
		public readonly int IndexedSheetSize = 512;
		public readonly Color[] HeightDebugColors = { Color.Red };
		public readonly string[] EditorTemplateOrder;
		public readonly string BlankTile = "blank.png";
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		[FieldLoader.Ignore]
		public readonly IReadOnlyDictionary<ushort, TerrainTemplateInfo> Templates;
		[FieldLoader.Ignore]
		public readonly IReadOnlyDictionary<TemplateSegment, TerrainTemplateInfo> SegmentsToTemplates;
		[FieldLoader.Ignore]
		public readonly IReadOnlyDictionary<string, IEnumerable<MultiBrushInfo>> MultiBrushCollections;

		[FieldLoader.Ignore]
		public readonly bool UseRemasteredTerrain;

		[FieldLoader.Ignore]
		public readonly TerrainTypeInfo[] TerrainInfo;
		readonly Dictionary<string, byte> terrainIndexByType = new();
		readonly byte defaultWalkableTerrainIndex;

		public RemasterTerrain(ModData modData, IReadOnlyFileSystem fileSystem, string filepath)
		{
			UseRemasteredTerrain = ((RemasterFileSystemLoader)modData.FileSystemLoader).UseRemasteredArtwork;
			var yaml = MiniYaml.FromStream(fileSystem.Open(filepath), filepath)
				.ToDictionary(x => x.Key, x => x.Value);

			// General info
			FieldLoader.Load(this, yaml["General"]);

			// TerrainTypes
			TerrainInfo = yaml["Terrain"].ToDictionary().Values
				.Select(y => new TerrainTypeInfo(y))
				.OrderBy(tt => tt.Type)
				.ToArray();

			if (TerrainInfo.Length >= byte.MaxValue)
				throw new YamlException("Too many terrain types.");

			for (byte i = 0; i < TerrainInfo.Length; i++)
			{
				var tt = TerrainInfo[i].Type;

				if (terrainIndexByType.ContainsKey(tt))
					throw new YamlException($"Duplicate terrain type '{tt}' in '{filepath}'.");

				terrainIndexByType.Add(tt, i);
			}

			defaultWalkableTerrainIndex = GetTerrainIndex("Clear");

			// Templates
			Templates = yaml["Templates"].ToDictionary().Values
				.Select(y => (TerrainTemplateInfo)new RemasterTerrainTemplateInfo(this, y)).ToDictionary(t => t.Id);

			SegmentsToTemplates = ImmutableDictionary.CreateRange(
				Templates.Values.SelectMany(
					template => template.Segments.Select(
						segment => new KeyValuePair<TemplateSegment, TerrainTemplateInfo>(segment, template))));

			MultiBrushCollections =
				yaml.TryGetValue("MultiBrushCollections", out var collectionDefinitions)
					? collectionDefinitions.ToDictionary()
						.Select(kv => new KeyValuePair<string, IEnumerable<MultiBrushInfo>>(
							kv.Key,
							MultiBrushInfo.ParseCollection(kv.Value)))
						.ToImmutableDictionary()
					: ImmutableDictionary<string, IEnumerable<MultiBrushInfo>>.Empty;
		}

		public TerrainTypeInfo this[byte index] => TerrainInfo[index];

		public byte GetTerrainIndex(string type)
		{
			if (terrainIndexByType.TryGetValue(type, out var index))
				return index;

			throw new InvalidDataException($"Tileset '{Id}' lacks terrain type '{type}'");
		}

		public byte GetTerrainIndex(TerrainTile r)
		{
			if (!Templates.TryGetValue(r.Type, out var tpl))
				return defaultWalkableTerrainIndex;

			if (tpl.Contains(r.Index))
			{
				var tile = tpl[r.Index];
				if (tile != null && tile.TerrainType != byte.MaxValue)
					return tile.TerrainType;
			}

			return defaultWalkableTerrainIndex;
		}

		public TerrainTileInfo GetTileInfo(TerrainTile r)
		{
			return Templates[r.Type][r.Index];
		}

		public bool TryGetTileInfo(TerrainTile r, out TerrainTileInfo info)
		{
			if (!Templates.TryGetValue(r.Type, out var tpl) || !tpl.Contains(r.Index))
			{
				info = null;
				return false;
			}

			info = tpl[r.Index];
			return info != null;
		}

		string ITerrainInfo.Id => Id;
		Size ITerrainInfo.TileSize => UseRemasteredTerrain ? RemasteredTileSize : TileSize;
		TerrainTypeInfo[] ITerrainInfo.TerrainTypes => TerrainInfo;
		TerrainTileInfo ITerrainInfo.GetTerrainInfo(TerrainTile r) { return GetTileInfo(r); }
		bool ITerrainInfo.TryGetTerrainInfo(TerrainTile r, out TerrainTileInfo info) { return TryGetTileInfo(r, out info); }
		Color[] ITerrainInfo.HeightDebugColors => HeightDebugColors;
		IEnumerable<Color> ITerrainInfo.RestrictedPlayerColors { get { return TerrainInfo.Where(ti => ti.RestrictPlayerColor).Select(ti => ti.Color); } }
		float ITerrainInfo.MinHeightColorBrightness => 1.0f;
		float ITerrainInfo.MaxHeightColorBrightness => 1.0f;
		TerrainTile ITerrainInfo.DefaultTerrainTile => new(Templates.First().Key, 0);

		string[] ITemplatedTerrainInfo.EditorTemplateOrder => EditorTemplateOrder;
		IReadOnlyDictionary<ushort, TerrainTemplateInfo> ITemplatedTerrainInfo.Templates => Templates;
		IReadOnlyDictionary<TemplateSegment, TerrainTemplateInfo> ITemplatedTerrainInfo.SegmentsToTemplates => SegmentsToTemplates;
		IReadOnlyDictionary<string, IEnumerable<MultiBrushInfo>> ITemplatedTerrainInfo.MultiBrushCollections => MultiBrushCollections;

		void ITerrainInfoNotifyMapCreated.MapCreated(Map map)
		{
			// Randomize PickAny tile variants
			var r = new MersenneTwister();
			for (var j = map.Bounds.Top; j < map.Bounds.Bottom; j++)
			{
				for (var i = map.Bounds.Left; i < map.Bounds.Right; i++)
				{
					var type = map.Tiles[new MPos(i, j)].Type;
					if (!Templates.TryGetValue(type, out var template) || !template.PickAny)
						continue;

					map.Tiles[new MPos(i, j)] = new TerrainTile(type, (byte)r.Next(0, template.TilesCount));
				}
			}
		}
	}
}
