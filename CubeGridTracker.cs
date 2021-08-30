﻿using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Common.ObjectBuilders;
using System.Text.RegularExpressions;
using Sandbox.Definitions;
using System.Linq;
using VRage.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace SimpleInventorySort
{
	public static class CubeGridTracker
	{
		private static DateTime m_lastRebuld = DateTime.Now;
		private static bool m_rebuild = true;

		public static DateTime LastRebuild
		{
			get {
				return m_lastRebuld;
			}
		}

		public static bool ShouldRebuild
		{
			get {
				return m_rebuild;
			}
		}

		public static void Rebuild()
		{
			DateTime start = DateTime.Now;

			try {
				m_rebuild = false;
				m_lastRebuld = DateTime.Now;
				HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
				MyAPIGateway.Entities.GetEntities(entities, x => x is IMyCubeGrid);
				
				foreach (IMyEntity entity in entities) {
					IMyCubeGrid grid = (IMyCubeGrid)entity;

					grid.OnBlockAdded -= OnBlockAdded;
					grid.OnBlockAdded += OnBlockAdded;
					grid.OnBlockRemoved -= OnBlockRemoved;
					grid.OnBlockRemoved += OnBlockRemoved;
					grid.OnBlockOwnershipChanged -= OnBlockOwnershipChanged;
					grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
				}
			}
			catch (Exception ex) {
				Logging.Instance.WriteLine(String.Format("CubeGridTracker.Rebuild(): {0}", ex.ToString()));
			}
			finally {
				if (Core.Debug) {
					Logging.Instance.WriteLine(string.Format("CubeGridTracker {0}ms", (DateTime.Now - start).Milliseconds));
				}
			}
		}

		public static void TriggerRebuild()
		{
			m_rebuild = true;
		}

		private static void CustomNameChanged(IMyTerminalBlock obj)
		{
			Inventory.TriggerRebuild();
		}
		
		private static void CustomDataChanged(IMyTerminalBlock obj)
		{
			Inventory.TriggerRebuild();
		}

		private static void OnBlockOwnershipChanged(IMyCubeGrid obj)
		{
			Inventory.TriggerRebuild();
		}

		private static void OnBlockRemoved(IMySlimBlock obj)
		{
			Inventory.TriggerRebuild();
		}

		private static void OnBlockAdded(IMySlimBlock obj)
		{
			Inventory.TriggerRebuild();
		}
	}
}