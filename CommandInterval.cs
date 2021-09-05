namespace SimpleInventorySort
{
	public class CommandInterval : CommandHandlerBase
	{
		public override string GetCommandText()
		{
			return "interval";
		}

		public override void HandleCommand(string[] words)
		{
			if (words.Length < 1) {
				Communication.Message(string.Format("This command lets you set the interval that the automated sort runs.  Currently it's set to {0} second(s).  Usage: /sort interval [Time in seconds].", Settings.Instance.Interval));
				return;
			}

			int interval;

			if (!int.TryParse(words[0], out interval)) {
				Communication.Message(string.Format("'{0}' is not a valid interval time.  Please enter a number.", words[0]));
				return;
			}

			if (interval > 1) {
				Settings.Instance.Interval = interval;
			}
			else {
				Communication.Message("Interval value must be greater than 1");
				return;
			}

			Communication.Message(string.Format("Sort Interval set to {0} seconds.", interval));
		}
	}
}
