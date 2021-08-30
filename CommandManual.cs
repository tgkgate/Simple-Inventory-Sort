using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
//using Sandbox.ModAPI.Interfaces;
using VRageMath;
using Sandbox.Common.ObjectBuilders;
using SimpleInventorySort;
using System.Text.RegularExpressions;
using Sandbox.Definitions;
using VRage;
using VRage.ObjectBuilders;

namespace SimpleInventorySort
{
	public class CommandManual : CommandHandlerBase
	{
		public override String GetCommandText()
		{
			return "manual";
		}

		public override void HandleCommand(String[] words)
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
			//Inventory.SortInventory();
			Inventory.NewSortInventory();
			Inventory.ProcessQueue();
			
			Communication.Message(String.Format("Manual sort completed in {0}ms", (DateTime.Now - start).TotalMilliseconds));
		}
	}
}