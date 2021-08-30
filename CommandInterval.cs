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
	public class CommandInterval : CommandHandlerBase
	{
		public override String GetCommandText()
		{
			return "interval";
		}

		public override void HandleCommand(String[] words)
		{
			if (words.Count() < 1) {
				Communication.Message(String.Format("This command lets you set the interval that the automated sort runs.  Currently it's set to {0} second(s).  Usage: /sort interval [Time in seconds].", Settings.Instance.Interval));
				return;
			}

			int interval = 2;

			if (!int.TryParse(words[0], out interval)) {
				Communication.Message(String.Format("'{0}' is not a valid interval time.  Please enter a number.", words[0]));
				return;
			}

			if (interval > 1) {
				Settings.Instance.Interval = interval;
			}
			else {
				Communication.Message("Interval value must be greater than 1");
				return;
			}

			Communication.Message(String.Format("Sort Interval set to {0} seconds.", interval));
		}
	}
}
