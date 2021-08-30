using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Common;
using Sandbox.ModAPI;
using Sandbox.Definitions;
using Sandbox.Game;
using VRage.Game.Components;

namespace SimpleInventorySort
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Core : MySessionComponentBase
    {
        // Declarations
        private static string version = "v0.1.0.21";
        private static bool m_debug = false;

        private bool m_initialized = false;
        private List<CommandHandlerBase> m_chatHandlers = new List<CommandHandlerBase>();
        private List<SimulationProcessorBase> m_simHandlers = new List<SimulationProcessorBase>();
        
        // Properties
        public static bool Debug
        {
            get { return m_debug; }
            set { m_debug = value; }
        }

        // Initializers
        private void Initialize()
        {
			// Load Settings
			Settings.Instance.Load();

            // Chat Line Event
            AddMessageHandler();

            // Chat Handlers
            m_chatHandlers.Add(new CommandToggle());
            m_chatHandlers.Add(new CommandDebug());
            m_chatHandlers.Add(new CommandFaction());
			m_chatHandlers.Add(new CommandManual());
			m_chatHandlers.Add(new CommandInterval());
			m_chatHandlers.Add(new CommandSettings());

            // Simulation Handlers
            m_simHandlers.Add(new SimulationSort());

            // Setup Grid Tracker
            //CubeGridTracker.SetupGridTracking();

            Logging.Instance.WriteLine(String.Format("Script Initializeda: {0}", version));

            //MyPerGameSettings.BallFriendlyPhysics = true;            
        }

        // Utility
        public void HandleMessageEntered(string messageText, ref bool sendToOthers)
        {
            try
            {
                if (messageText[0] != '/')
                    return;

                string[] commandParts = messageText.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                if (commandParts[0].ToLower() != "/sort")
                    return;

                sendToOthers = false;
                int paramCount = commandParts.Length - 1;
                if (paramCount < 1 || (paramCount == 1 && commandParts[1].ToLower() == "help"))
                {
                    List<String> commands = new List<string>();
                    foreach (CommandHandlerBase chatHandler in m_chatHandlers)
                    {
                        String commandBase = chatHandler.GetCommandText().Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).First();
                        if (!commands.Contains(commandBase))
                            commands.Add(commandBase);
                    }

                    String commandList = String.Join(", ", commands);
                    String info = String.Format("Simple Inventory Sort {0}.  Available Commands: {1}", version, commandList);

                    //if (MyAPIGateway.Multiplayer.MultiplayerActive)
                    //    info += string.Format("\nSort commands no longer function in multiplayer.  Sorting happens every 30 seconds, and run on the server due to the new Netcode.");

                    Communication.Message(info);
                    return;
                }

                foreach (CommandHandlerBase chatHandler in m_chatHandlers)
                {
                    int commandCount = 0;
                    if (chatHandler.CanHandle(commandParts.Skip(1).ToArray(), ref commandCount))
                    {
                        chatHandler.HandleCommand(commandParts.Skip(commandCount + 1).ToArray());
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(String.Format("HandleMessageEntered(): {0}", ex.ToString()));
            }
        }

        public void AddMessageHandler()
        {
            MyAPIGateway.Utilities.MessageEntered += HandleMessageEntered;
        }

        public void RemoveMessageHandler()
        {
            MyAPIGateway.Utilities.MessageEntered -= HandleMessageEntered;
        }

        // Overrides
        public override void UpdateBeforeSimulation()
        {
			try
			{
				if (MyAPIGateway.Utilities == null)
					return;

				// Run the init
				if (!m_initialized)
				{
					m_initialized = true;
					Initialize();
				}

				// Run the sim handlers
				foreach (SimulationProcessorBase simHandler in m_simHandlers)
				{
					simHandler.Handle();
				}
			}
			catch (Exception ex)
			{
				Logging.Instance.WriteLine(String.Format("UpdateBeforeSimulation(): {0}", ex.ToString()));
			}
        }

        protected override void UnloadData()
        {
            try
            {
                RemoveMessageHandler();
                if (Logging.Instance != null)
                    Logging.Instance.Close();
            }
            catch { }

            base.UnloadData();
        }
    }
}
