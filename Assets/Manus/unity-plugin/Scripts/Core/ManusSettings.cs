using Manus.Utility;

using UnityEngine;

namespace Manus
{
	/// <summary>
	/// The global Settings used by the Manus Manager.
	/// </summary>
	[System.Serializable]
	public class ManusSettings
	{
		public const string s_FileName = "ConnectionSettings";
		public const string s_FileExtension = ".json";

		public string directory
		{
			get { return Application.streamingAssetsPath; }
		}

		public string filePath
		{
			get
			{
				return $"{directory}/{s_FileName}{s_FileExtension}";
			}
		}

		[SerializeField] private bool m_AutoConnect;
		[SerializeField] private bool m_LocalOnly;
		[SerializeField] private bool m_ConnectGRPC;
		[SerializeField] private string m_LastConnectedHost;

		public ManusSettings( bool p_LoadSettings = false )
		{
			// Set default values
			m_AutoConnect = true;
			m_LocalOnly = true;
			m_ConnectGRPC = false;
			m_LastConnectedHost = "";

			// Try loading the settings from a file
			if( p_LoadSettings )
				LoadConnectionSettings();
		}

		public bool autoConnect
		{
			get { return m_AutoConnect; }
			set
			{
				m_AutoConnect = value;
			}
		}
		public bool localOnly
		{
			get { return m_LocalOnly; }
			set
			{
				m_LocalOnly = value;
			}
		}
		public bool connectGRPC
		{
			get { return m_ConnectGRPC; }
			set
			{
				m_ConnectGRPC = value;
			}
		}

		public string lastConnectedHost
		{
			get { return m_LastConnectedHost; }
			set
			{
				m_LastConnectedHost = value.Replace( ".localdomain", "" );
			}
		}

		public void Save()
		{
			SaveConnectionSettings();
		}

		private void LoadConnectionSettings()
		{
#if PLATFORM_ANDROID
			return;
#endif

			string t_FilePath = filePath;
			if( !System.IO.File.Exists( t_FilePath ) )
			{
				if( !System.IO.Directory.Exists( directory ) )
					System.IO.Directory.CreateDirectory( directory );
				
				SaveConnectionSettings();
				return;
			}

			string t_JSON = System.IO.File.ReadAllText(t_FilePath);
			var t_Settings = JsonUtility.FromJson<ManusSettings>( t_JSON );

			m_AutoConnect = t_Settings.autoConnect;
			m_LocalOnly = t_Settings.localOnly;
			m_ConnectGRPC = t_Settings.connectGRPC;
			m_LastConnectedHost = t_Settings.lastConnectedHost;
		}

		private void SaveConnectionSettings()
		{
#if PLATFORM_ANDROID
			return;
#endif
			string t_JSON = JsonUtility.ToJson( this, true );
			string t_FilePath = filePath;
			System.IO.File.WriteAllText( t_FilePath, t_JSON );
		}
	}
}
