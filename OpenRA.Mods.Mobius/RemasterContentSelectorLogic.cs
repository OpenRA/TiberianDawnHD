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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Installer;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Mobius.FileSystem;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Mobius.Widgets.Logic
{
	public class RemasterModContent : IGlobalModData
	{
		public sealed class ContentSource
		{
			[FluentReference]
			public readonly string Title = null;

			public readonly Dictionary<string, ModContent.ModSource> Sources;

			public ContentSource(MiniYaml yaml)
			{
				Title = yaml.Value;
				Sources = yaml.Nodes.ToDictionary(n => n.Key, n => new ModContent.ModSource(n.Value));
			}
		}

		[FieldLoader.Require]
		public readonly string Mod;

		[IncludeFluentReferences(LintDictionaryReference.Values)]
		[FieldLoader.LoadUsing(nameof(LoadContentSources))]
		public readonly Dictionary<string, ContentSource> ContentSources = null;

		static object LoadContentSources(MiniYaml yaml)
		{
			var ret = new Dictionary<string, ContentSource>();
			var sourcesNode = yaml.Nodes.Single(n => n.Key == "ContentSources");
			foreach (var s in sourcesNode.Value.Nodes)
				ret.Add(s.Key, new ContentSource(s.Value));

			return ret;
		}
	}

	public class RemasterContentSelectorLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public RemasterContentSelectorLogic(Widget widget, ModData modData)
		{
			var content = modData.Manifest.Get<RemasterModContent>();
			var contentSourceSettings = Game.Settings.GetOrCreate<RemasterFileSystemLoader.ContentSourceSettings>(modData.ObjectCreator, content.Mod);

			var sources = new Dictionary<string, string>();
			foreach (var cs in content.ContentSources)
			{
				foreach (var s in cs.Value.Sources)
				{
					var sourceResolver = modData.ObjectCreator.CreateObject<ISourceResolver>($"{s.Value.Type.Value}SourceResolver");
					var path = sourceResolver.FindSourcePath(s.Value);
					if (path != null)
					{
						sources[cs.Key] = FluentProvider.GetMessage(cs.Value.Title);
						break;
					}
				}
			}

			var sourceDropdown = widget.Get<DropDownButtonWidget>("SOURCE_DROPDOWN");
			var continueButton = widget.Get<ButtonWidget>("CONTINUE_BUTTON");
			var quitButton = widget.Get<ButtonWidget>("QUIT_BUTTON");
			if (sources.Count > 0)
			{
				if (contentSourceSettings.ContentSource == null || !sources.ContainsKey(contentSourceSettings.ContentSource))
				{
					contentSourceSettings.ContentSource = sources.Keys.First();
					contentSourceSettings.Save();
				}

				ScrollItemWidget SetupItem(KeyValuePair<string, string> option, ScrollItemWidget template)
				{
					var item = ScrollItemWidget.Setup(template,
						() => contentSourceSettings.ContentSource == option.Key,
						() =>
						{
							contentSourceSettings.ContentSource = option.Key;
							contentSourceSettings.Save();
						});

					item.Get<LabelWidget>("LABEL").GetText = () => option.Value;
					return item;
				}

				var sourceLabel = new CachedTransform<string, string>(s => sources[s]);
				sourceDropdown.GetText = () => sourceLabel.Update(contentSourceSettings.ContentSource);
				sourceDropdown.OnClick = () => sourceDropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 210, sources, SetupItem);
				continueButton.OnClick = () => Game.RunAfterTick(() => Game.InitializeMod(content.Mod, new Arguments()));
			}
			else
			{
				sourceDropdown.Disabled = true;
				continueButton.Visible = false;
				quitButton.Visible = true;
				quitButton.OnClick = Game.Exit;
			}
		}
	}
}
