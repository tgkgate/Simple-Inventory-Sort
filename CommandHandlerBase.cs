using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleInventorySort
{
    public abstract class CommandHandlerBase
    {
        public virtual Boolean CanHandle(String[] words, ref int commandCount)
        {
            commandCount = GetCommandText().Split(new char[] { ' ' }).Count();
            if (words.Length > commandCount - 1)
                return String.Join(" ", words).ToLower().StartsWith(GetCommandText());

            return false;
        }

        public virtual String GetCommandText()
        {
            return "";
        }

        public virtual void HandleCommand(String[] words)
        {

        }
    }
}
