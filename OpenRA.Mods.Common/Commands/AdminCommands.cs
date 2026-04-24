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
using OpenRA.Graphics;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Commands
{
	[TraitLocation(SystemActors.World)]
	[Desc("Enables the admin authentication command in the chatbox. Attach this to the world actor.")]
	public class AdminCommandsInfo : TraitInfo<AdminCommands> { }

	public class AdminCommands : IChatCommand, IWorldLoaded
	{
		readonly IDictionary<string, Action<string, World>> commandHandlers = new Dictionary<string, Action<string, World>>
		{
			{ "admin", OpenAdminAuthWindow },
		};

		World world;

		public void WorldLoaded(World w, WorldRenderer wr)
		{
			world = w;
			var console = world.WorldActor.Trait<ChatCommands>();
			foreach (var cmd in commandHandlers)
				console.RegisterCommand(cmd.Key, this);
		}

		public void InvokeCommand(string name, string arg)
		{
			if (commandHandlers.TryGetValue(name, out var handler))
				handler(arg, world);
		}

		static void OpenAdminAuthWindow(string arg, World world)
		{
			var wa = new WidgetArgs { { "world", world }, { "orderManager", null } };
			wa["args"] = wa;
			Ui.OpenWindow("ADMIN_AUTH_PANEL", wa);
		}
	}
}
