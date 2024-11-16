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

using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class RemasterContentPromptLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public RemasterContentPromptLogic(Widget widget, ModData modData)
		{
			widget.Get<ButtonWidget>("QUIT_BUTTON").OnClick = Game.Exit;
		}
	}
}
