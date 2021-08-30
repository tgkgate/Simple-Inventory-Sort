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
using VRage;
using VRage.ObjectBuilders;

namespace SimpleInventorySort
{
    public class CommandSettings : CommandHandlerBase
    {
        public override String GetCommandText()
        {
            return "settings";
        }

        public override void HandleCommand(String[] words)
        {
			Communication.Message("Settings: ");
			Communication.Message(String.Format("Sorting is {0}", Settings.Instance.Enabled ? "Enabled" : "Disabled"));
			Communication.Message(String.Format("Faction Sorting is {0}", Settings.Instance.Faction ? "Enabled" : "Disabled"));
			Communication.Message(String.Format("Interval set to {0} seconds", Settings.Instance.Interval));
        }
    }
}
