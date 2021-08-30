namespace SimpleInventorySort
{
	public abstract class CommandHandlerBase
	{
		public virtual bool CanHandle(string[] words, ref int commandCount)
		{
			commandCount = GetCommandText().Split(new char[] { ' ' }).Length;

			if (words.Length > commandCount - 1) {
				return string.Join(" ", words).ToLower().StartsWith(GetCommandText());
			}

			return false;
		}

		public virtual string GetCommandText()
		{
			return "";
		}

		public virtual void HandleCommand(string[] words)
		{
			/* Do Nothing */
		}
	}
}
