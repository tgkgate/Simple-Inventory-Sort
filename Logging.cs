using Sandbox.ModAPI;
using System;
using System.IO;
using System.Text;

// This was just pulled from example script, it's a good enough logger

namespace SimpleInventorySort
{
	internal class Logging
	{
		private static Logging m_instance;
		private TextWriter m_writer;
		private int m_indent = 0;
		private readonly StringBuilder m_cache = new StringBuilder();
		private readonly string m_logFile = "default.log";
		private bool m_closed = false;

		public static Logging Instance {
			get {
				if (m_instance == null) {
					m_instance = new Logging("InventorySort.log");
				}

				return m_instance;
			}
		}

		public Logging(string logFile)
		{
			m_logFile = logFile;
			m_writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(logFile, typeof(Logging));
			m_instance = this;
		}

		public void IncreaseIndent()
		{
			m_indent++;
		}

		public void DecreaseIndent()
		{
			if (m_indent > 0) {
				m_indent--;
			}
		}

		public void WriteLine(string text)
		{
			if (MyAPIGateway.Utilities == null) {
				return;
			}

			if (m_writer == null && !m_closed) {
				m_writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(m_logFile, typeof(Logging));
				return;
			}

			if (m_cache.Length > 0) {
				m_writer.WriteLine(m_cache);
			}

			m_cache.Clear();
			m_cache.Append(DateTime.Now.ToString("[HH:mm:ss] "));

			for (int i = 0; i < m_indent; i++) {
				m_cache.Append("\t");
			}

			m_writer.WriteLine(m_cache.Append(text));
			m_writer.Flush();
			m_cache.Clear();
		}

		public void Write(string text)
		{
			m_cache.Append(text);
		}

		internal void Close()
		{
			m_closed = true;

			if (m_cache.Length > 0) {
				m_writer.WriteLine(m_cache);
			}

			m_writer.Flush();
			m_writer.Close();
		}
	}
}
