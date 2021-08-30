using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using Sandbox.ModAPI;
//using Sandbox.ModAPI.Interfaces;
using VRageMath;
using Sandbox.Common.ObjectBuilders;
using SimpleInventorySort;
using System.Text.RegularExpressions;
//using Sandbox.Definitions;
using VRage;
using VRage.ObjectBuilders;
using VRage.ModAPI;

namespace SimpleInventorySort
{
	public class CommandFaction : CommandHandlerBase
	{
		public override String GetCommandText()
		{
			return "faction";
		}

		public override void HandleCommand(String[] words)
		{
			Settings.Instance.Faction = !Settings.Instance.Faction;

			if (Settings.Instance.Faction) {
				Communication.Message("Sorting all faction and shared blocks toggled ON.");
			}
			else {
				Communication.Message("Sorting all faction and shared blocks toggled OFF.");
			}
		}
	}
}
