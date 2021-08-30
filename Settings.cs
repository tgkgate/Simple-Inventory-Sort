using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI;
using System.IO;


namespace SimpleInventorySort
{
	public class Settings
	{
		private static Settings m_instance = null;
		private int m_interval = 5;
		private bool m_enabled = true;
		private bool m_faction = false;

		public static Settings Instance
		{
			get
			{
				if (m_instance == null)
					m_instance = new Settings();

				return m_instance;
			}
		}

		public int Interval
		{
			get { return m_interval; }
			set 
			{
				m_interval = value;
				Save();
			}
		}

		public bool Enabled
		{
			get { return m_enabled; }
			set 
			{
				m_enabled = value;
				Save();
			}
		}

		public bool Faction
		{
			get { return m_faction; }
			set
			{
				m_faction = value;
				Save();
			}
		}

		/// <summary>
		/// God I hate this - This can change to serialize, but meh
		/// </summary>
		public void Load()
		{
			if (MyAPIGateway.Utilities == null)
				return;

			try
			{
				if (MyAPIGateway.Utilities.FileExistsInLocalStorage("Settings.txt", typeof(Settings)))
				{
					using (TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("Settings.txt", typeof(Settings)))
					{
						m_interval = 5;
						int.TryParse(reader.ReadLine(), out m_interval);

						m_enabled = true;
						bool.TryParse(reader.ReadLine(), out m_enabled);

						m_faction = false;
						bool.TryParse(reader.ReadLine(), out m_faction);
					}
				}
			}
			catch (Exception ex)
			{
				Logging.Instance.WriteLine(String.Format("Load(): {0}", ex.ToString()));
			}
		}

		public void Save()
		{
			if (MyAPIGateway.Utilities == null)
				return;

			try
			{
				using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("Settings.txt", typeof(Settings)))
				{
					writer.WriteLine(m_interval);
					writer.WriteLine(m_enabled);
					writer.WriteLine(m_faction);
				}
			}
			catch (Exception ex)
			{
				Logging.Instance.WriteLine(String.Format("Save(): {0}", ex.ToString()));
			}
		}
	}
}
