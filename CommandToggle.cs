using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRageMath;
using Sandbox.Common.ObjectBuilders;
using SimpleInventorySort;
using System.Text.RegularExpressions;
using Sandbox.Definitions;
//using Sandbox.Common.ObjectBuilders.Serializer;
using VRage;

namespace SimpleInventorySort
{
	public class CommandToggle : CommandHandlerBase
	{
		public override String GetCommandText()
		{
			return "toggle";
		}

		public override void HandleCommand(String[] words)
		{
			Settings.Instance.Enabled = !Settings.Instance.Enabled;

			if (Settings.Instance.Enabled) {
				Communication.Message("Automated Sorting Toggled On.");
			}
			else {
				Communication.Message("Automated Sorting Toggled Off.");
			}
		}
	}
}