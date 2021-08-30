using Sandbox.ModAPI;
using System;
using System.Timers;

namespace SimpleInventorySort
{
	public class SimulationSort : SimulationProcessorBase
	{
		private DateTime m_lastUpdate;
		private Timer m_sortTimer;
		private bool m_init = false;

		public SimulationSort()
		{
			/* Do Nothing */
		}

		private void SetupSort()
		{
			Inventory.QueueReady = false;
			m_lastUpdate = DateTime.Now;
			m_sortTimer = new Timer();
			int intervalTime = 2;

			if (MyAPIGateway.Multiplayer.MultiplayerActive) {
				intervalTime = Math.Max(30, Settings.Instance.Interval);
			}
			else {
				intervalTime = Math.Max(2, Settings.Instance.Interval);
			}

			m_sortTimer.Interval = intervalTime * 1000;
			m_sortTimer.AutoReset = false;
			m_sortTimer.Elapsed += TimerElapsed;
			m_sortTimer.Enabled = true;
		}

		/// <summary>
		/// In order to try to stop from causing the game thread to pause on large sorts, we are going to use a Timer as a ThreadPool.  This may be
		/// extremely unsafe, but the entire sort mostly consists of reads, and any issue on read should safely exception and our state will be
		/// stable as we're just queuing "writes" for the game thread.  If this causes a lot of issues, I'll easily move it all back into the game
		/// thread, but I'd like to see this work.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TimerElapsed(object sender, ElapsedEventArgs e)
		{
			try {
				if (!Inventory.QueueReady) {
					m_sortTimer.Enabled = false;

					if (Settings.Instance.Enabled) {
						//if (DateTime.Now - CubeGridTracker.LastRebuild > TimeSpan.FromSeconds(60))
						CubeGridTracker.TriggerRebuild();

						// This will need to rebuild every update for now since CustomNameChanged event is
						// bugged.
						Inventory.TriggerRebuild();
						Conveyor.TriggerRebuild();

						// No longer sort on the client if we're in multiplayer.
						if (MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer) {
							return;
						}

						Inventory.SortInventory();
					}
				}
			}

			finally {
				int intervalTime;
				if (MyAPIGateway.Multiplayer.MultiplayerActive) {
					intervalTime = Math.Max(30, Settings.Instance.Interval);
				}
				else {
					intervalTime = Math.Max(2, Settings.Instance.Interval);
				}

				m_sortTimer.Interval = intervalTime * 1000;
				m_sortTimer.Enabled = true;
			}
		}

		public override void Handle()
		{
			if (MyAPIGateway.Session == null) {
				return;
			}

			if (MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer) {
				return;
			}

			try {
				if (!m_init) {
					m_init = true;
					SetupSort();
				}

				if (DateTime.Now - m_lastUpdate > TimeSpan.FromMilliseconds(500)) {
					m_lastUpdate = DateTime.Now;

					if (Inventory.QueueReady) {
						Inventory.ProcessQueue();
					}
				}
			}

			catch (Exception ex) {
				Logging.Instance.WriteLine(string.Format("Handle(): {0}", ex.ToString()));
			}

			finally {
				if (Inventory.QueueReady && Inventory.QueueCount < 1) {
					Inventory.QueueReady = false;
				}
			}

			base.Handle();
		}
	}
}
