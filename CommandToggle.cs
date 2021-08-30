namespace SimpleInventorySort
{
	public class CommandToggle : CommandHandlerBase
	{
		public override string GetCommandText()
		{
			return "toggle";
		}

		public override void HandleCommand(string[] words)
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
