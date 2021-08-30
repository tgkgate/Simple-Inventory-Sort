using Sandbox.ModAPI;
using System;

namespace SimpleInventorySort
{
	public class CommandManual : CommandHandlerBase
	{
		public override string GetCommandText()
		{
			return "manual";
		}

		public override void HandleCommand(string[] words)
		{
			if (MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer) {
				Communication.Message("Unable to manually sort in multiplayer.  Sort set to 30 second intervals.");
				return;
			}

			Communication.Message("Starting manual sort ...");

			DateTime start = DateTime.Now;
			CubeGridTracker.TriggerRebuild();
			Inventory.TriggerRebuild();
			Conveyor.TriggerRebuild();
			Inventory.SortInventory();
			Inventory.ProcessQueue();

			Communication.Message(string.Format("Manual sort completed in {0}ms", (DateTime.Now - start).TotalMilliseconds));
		}
	}
}
