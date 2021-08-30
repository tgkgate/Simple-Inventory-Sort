namespace SimpleInventorySort
{
	public class CommandSettings : CommandHandlerBase
	{
		public override string GetCommandText()
		{
			return "settings";
		}

		public override void HandleCommand(string[] words)
		{
			Communication.Message("Settings: ");
			Communication.Message(string.Format("Sorting is {0}", Settings.Instance.Enabled ? "Enabled" : "Disabled"));
			Communication.Message(string.Format("Faction Sorting is {0}", Settings.Instance.Faction ? "Enabled" : "Disabled"));
			Communication.Message(string.Format("Interval set to {0} seconds", Settings.Instance.Interval));
		}
	}
}
