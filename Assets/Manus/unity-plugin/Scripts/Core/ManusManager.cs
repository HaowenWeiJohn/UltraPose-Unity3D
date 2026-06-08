using System;
using System.Threading;
using Manus.Utility;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Manus
{
	/// <summary>
	/// This is the central location for all communication between certain aspects of the Manus Plugin.
	/// </summary>
#if UNITY_EDITOR
	[InitializeOnLoad]
#endif
	public static class ManusManager
	{
		private static bool s_IsShutdown = false;
#if MANUS_EXTENDED
		private static CommunicationHubExtended s_CommunicationHub = null;
#else
		private static CommunicationHub s_CommunicationHub = null;
#endif
#if PLATFORM_ANDROID
		private static ManusSettings s_Settings = new ManusSettings(false);
#else
		private static ManusSettings s_Settings = new ManusSettings(true);
#endif
		private static bool s_PlayMode;

		public static Thread mainUnityThread = Thread.CurrentThread;

		/// <summary>
		/// Finds or instantiates and returns the Communication Hub.
		/// Can return NULL if shutting down!
		/// Also launch UnityMainThreadDispatcher for main thread executions.
		/// </summary>
#if MANUS_EXTENDED
		public static CommunicationHubExtended communicationHub
#else
		public static CommunicationHub communicationHub
#endif
		{
			get
			{
#if UNITY_EDITOR
				if( EditorApplication.isCompiling ) return s_CommunicationHub;
#endif
				if( s_CommunicationHub != null ) return s_CommunicationHub;
				if( s_IsShutdown ) return null;
#if !UNITY_EDITOR
				UnityMainThreadDispatcher.Initalize();
				if ( settings.autoConnect )
				{
					Debug.Log( "Auto connect enabled" );
				}
#endif
#if MANUS_EXTENDED
				s_CommunicationHub = new CommunicationHubExtended();
#else
				s_CommunicationHub = new CommunicationHub();
#endif
				return s_CommunicationHub;
			}
		}

		/// <summary>
		/// Finds and loads settings.
		/// </summary>
		public static ManusSettings settings
		{
			get
			{
				return s_Settings;
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// Handle switching between editor and playmode.
		/// </summary>
		static ManusManager()
		{
			EditorApplication.update += StartUp;
			AssemblyReloadEvents.beforeAssemblyReload += Quitting;
			EditorApplication.playModeStateChanged += EvaluatePlayModeChanged;
			mainUnityThread = Thread.CurrentThread;
		}
		/// <summary>
		/// Forwards changes in the state of playmode.
		/// </summary>
		/// <param name="p_State">Current playmode state</param>
		static void EvaluatePlayModeChanged( PlayModeStateChange p_State )
		{
			switch( p_State )
			{
				case PlayModeStateChange.ExitingPlayMode:
					s_PlayMode = false;
					break;
				case PlayModeStateChange.EnteredPlayMode:
					UnityMainThreadDispatcher.Initalize();
					s_PlayMode = true;
					communicationHub?.UpdatePlayMode( s_PlayMode );
					goto default;
				default:
					communicationHub?.VerifyUpdateLoop();
					break;
			}
		}

		static void StartUp()
		{
			EditorApplication.update -= StartUp;
			communicationHub.UpdatePlayMode( s_PlayMode );
			communicationHub.VerifyUpdateLoop();
		}

		public static void ForceQuit()
		{
			if( s_CommunicationHub == null ) return;
			s_IsShutdown = true;
			s_CommunicationHub.Destroy();
			s_CommunicationHub = null;
			System.GC.Collect();
			s_IsShutdown = false;
			return;
		}
#else
		/// <summary>
		/// Handle shutting down of plugin.
		/// </summary>
		static ManusManager()
		{
			Application.quitting += Quitting;
		}
#endif

		/// <summary>
		/// Ensure communication hub thread gets shut down appropriately.
		/// </summary>
		private static void Quitting()
		{
#if UNITY_EDITOR
			AssemblyReloadEvents.beforeAssemblyReload -= Quitting;
#endif
			s_IsShutdown = true;
			s_CommunicationHub?.UpdatePlayMode( false );
			s_CommunicationHub?.Destroy();
			s_CommunicationHub = null;
		}

		/// <summary>
		/// Check whether unity hub exists.
		/// </summary>
		/// <returns>True if communication hub exists</returns>
		public static bool HasCommunicationHub()
		{
			return s_CommunicationHub != null;
		}
	}
}
