using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Manus.Skeletons;
using Manus.Trackers;
using Manus.Utility;

using UnityEngine;
using UnityEngine.Events;

namespace Manus
{
	/// <summary>
	/// This class is responsible for the communication between Manus Core and the plugin.
	/// This component should not be added to the scene manually.
	/// Most of these functions are not to be accessed directly.
	/// </summary>
	public class CommunicationHub
	{
		public enum State
		{
			Unknown,
			Disconnected,
			Connecting,
			Connected
		}

		[System.Serializable]
		public struct ErgonomicsStream
		{
			public CoreSDK.ManusTimestamp publishTime;
			public List<CoreSDK.ErgonomicsData> data;

			public ErgonomicsStream( CoreSDK.ManusTimestamp p_PublishTime )
			{
				publishTime = p_PublishTime;
				data = new List<CoreSDK.ErgonomicsData>();
			}
		}

		[System.Serializable]
		public struct CommandResponse
		{
			public bool result;
			public string message;

			public CommandResponse( bool p_Result, string p_Message )
			{
				result = p_Result;
				message = p_Message;
			}
		}

		[System.Serializable]
		public struct Landscape
		{
			public struct UserLandscapeData
			{
				public uint id;
				public uint leftGloveID;
				public uint rightGloveID;
				public UserProfileLandscapeData profile;
				public uint userIndex;

				public struct UserProfileLandscapeData
				{
					public CoreSDK.ProfileType profileType;
					public List<CoreSDK.Measurement> bodyMeasurements;
					public List<CoreSDK.TrackerOffset> trackerOffsets;
					public List<CoreSDK.ExtraTrackerOffset> extraTrackerOffsets;

					public UserProfileLandscapeData( CoreSDK.ProfileType p_ProfileType )
					{
						profileType = p_ProfileType;
						bodyMeasurements = new List<CoreSDK.Measurement>();
						trackerOffsets = new List<CoreSDK.TrackerOffset>();
						extraTrackerOffsets = new List<CoreSDK.ExtraTrackerOffset>();
					}
				}
			}

			public List<CoreSDK.DongleLandscapeData> dongles;
			public List<CoreSDK.GloveLandscapeData> gloves;
			public List<CoreSDK.UserLandscapeData> users;
			public List<CoreSDK.SkeletonLandscapeData> skeletons;
			public List<CoreSDK.TrackerLandscapeData> trackers;
			public List<CoreSDK.GestureLandscapeData> gestures;
			public List<CoreSDK.NetDeviceLandscapeData> netDevices;
			public CoreSDK.SettingsLandscape settings;
			public CoreSDK.TimeLandscape time;

			public Landscape( CoreSDK.SettingsLandscape p_Settings, CoreSDK.TimeLandscape p_Time )
			{
				dongles = new List<CoreSDK.DongleLandscapeData>();
				gloves = new List<CoreSDK.GloveLandscapeData>();
				users = new List<CoreSDK.UserLandscapeData>();
				skeletons = new List<CoreSDK.SkeletonLandscapeData>();
				trackers = new List<CoreSDK.TrackerLandscapeData>();
				gestures = new List<CoreSDK.GestureLandscapeData>();
				netDevices = new List<CoreSDK.NetDeviceLandscapeData>();
				settings = p_Settings;
				time = p_Time;
			}
		}

		[System.Serializable]
		public class LandscapeEvent : UnityEvent<Landscape>
		{
		}
		public LandscapeEvent onLandscapeEvent = new LandscapeEvent();
		[System.Serializable]
		public class ErgonomicsEvent : UnityEvent<ErgonomicsStream>
		{
		}

		[System.Serializable]
		public class SystemMessageEvent : UnityEvent<CoreSDK.SystemMessage>
		{
		}

		[System.Serializable]
		public class SkeletonEvent : UnityEvent<CoreSDK.SkeletonStream>
		{
		}

		[System.Serializable]
		public class TrackerEvent : UnityEvent<CoreSDK.TrackerStream>
		{
		}

		[System.Serializable]
		public class RawSkeletonEvent : UnityEvent<CoreSDK.RawSkeletonStream>
		{
		}

		[System.Serializable]
		public class RawDeviceDataEvent : UnityEvent<CoreSDK.RawData>
		{
		}

		[System.Serializable]
		public class GestureEvent : UnityEvent<CoreSDK.GestureStream>
		{
		}

		public ErgonomicsEvent onErgonomicsData = new ErgonomicsEvent();
		public SystemMessageEvent onSystemMessageEvent = new SystemMessageEvent();
		public SkeletonEvent onSkeletonData = new SkeletonEvent();
		public TrackerEvent onTrackerData = new TrackerEvent();
		public RawSkeletonEvent onRawSkeletonData = new RawSkeletonEvent();
		public RawDeviceDataEvent onRawDeviceData = new RawDeviceDataEvent();
		public GestureEvent onGestureData = new GestureEvent();

		public UnityEvent onConnectedToCore = new UnityEvent();
		public UnityEvent onDisconnectedFromCore = new UnityEvent();
		public UnityEvent onHostsFound = new UnityEvent();

		public static Landscape landscape { get; private set; }
		public static ErgonomicsStream ergonomicsData { get; protected set; }
		public static uint completeSkeletonIndex { get; protected set; }

		protected Dictionary<uint, Skeleton> m_Skeletons = new Dictionary<uint, Skeleton>();
		protected List<uint> m_LoadableSkeletons = new List<uint>();
		protected HashSet<TrackedObject> m_TrackedObjects = new HashSet<TrackedObject>();
		Mutex m_SkeletonDataQueueMutex = new Mutex();
		protected Queue<CoreSDK.SkeletonStream> m_SkeletonData = new Queue<CoreSDK.SkeletonStream>();
		protected CoreSDK.TrackerStream m_TrackerData = new CoreSDK.TrackerStream();
		protected CoreSDK.RawSkeletonStream m_RawSkeletonData = new CoreSDK.RawSkeletonStream();
		protected Dictionary<string, CustomTracker> m_CustomTrackers = new Dictionary<string, CustomTracker>();

		protected bool m_HandsChanged = false;
		protected static bool s_Active = false;

		protected float m_SeachTimer = s_HostFetchWaitDelay;
		protected const uint s_HostFetchWaitDelay = 1;
		protected const float s_HostAutoFindInterval = 10.0f;

		protected bool m_SearchingForHosts = false;
		protected List<CoreSDK.ManusHost> m_HostsFound = new List<CoreSDK.ManusHost>();
		protected CoreSDK.ManusHost? m_CurrentHost = new CoreSDK.ManusHost();
		protected CoreSDK.ManusHost m_PreviousHost = new CoreSDK.ManusHost();

		Mutex m_SkeletonQueueMutex = new Mutex();
		protected List<Skeleton> m_SkeletonQueue = new List<Skeleton>();

		Mutex m_TrackedObjectsQueueMutex = new Mutex();
		protected List<TrackedObject> m_TrackedObjectsQueue = new List<TrackedObject>();
		protected uint m_SessionId;
		protected List<Skeleton> m_SentTempSkeletons = new List<Skeleton>();

		protected volatile State m_NextState;
		protected volatile State m_RequestedNextState;
		public State currentState { get; protected set; }
		public CoreSDK.ManusHost? currentHost { get { return m_CurrentHost; } }

		Thread m_UpdateThread;

		bool m_Playmode = true;
		volatile bool m_RunThread = false;

		// Use a Remote core, or the new Core Lite?
		protected bool m_RemoteConnection = true;

		/// <summary>
		/// Initial setup for communication hub.
		/// </summary>
		public CommunicationHub()
		{
			StartUp();
		}

		~CommunicationHub()
		{
			ShutDown();
		}

		/// <summary>
		/// Get communication hub ready to be collected by garbage collector.
		/// </summary>
		public void Destroy()
		{
			m_RunThread = false;
			m_UpdateThread?.Join();
			ShutDown();
		}

		/// <summary>
		/// Verify the update loop thread is running else restart it.
		/// </summary>
		public void VerifyUpdateLoop()
		{
			if( m_UpdateThread == null || !m_UpdateThread.IsAlive )
			{
				m_RunThread = false;
				m_UpdateThread?.Join();
				m_RunThread = true;
				m_UpdateThread = new Thread( RunUpdateLoop );
				m_UpdateThread.Start();
			}
		}

		/// <summary>
		/// Update whether playmode is active or not.
		/// </summary>
		/// <param name="p_PlayMode">Whether playmode is active</param>
		public void UpdatePlayMode( bool p_PlayMode )
		{
			m_Playmode = p_PlayMode;
		}

		/// <summary>
		/// Starts up update thread and Manus SDK.
		/// </summary>
		virtual protected void StartUp()
		{
			InitializeSDK();

			currentState = State.Disconnected;
			m_NextState = State.Disconnected;
			m_RequestedNextState = State.Disconnected;
			VerifyUpdateLoop();
		}

		/// <summary>
		/// Shut down update thread and Manus SDK.
		/// </summary>
		protected void ShutDown()
		{
			ShutDownSDK();
		}

		/// <summary>
		/// Initialize the Manus SDK and register needed callbacks.
		/// </summary>
		virtual protected void InitializeSDK()
		{
			if( s_Active ) return;
			s_Active = true;

			CoreSDK.SDKReturnCode t_Result = CoreSDK.Initialize( CoreSDK.SessionType.UnityPlugin );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not initialize SDK" );
			}

			RegisterCallbacks();
		}

		/// <summary>
		/// Shuts down the Manus SDK.
		/// </summary>
		protected void ShutDownSDK()
		{
			if( !s_Active ) return;
			s_Active = false;

			CoreSDK.SDKReturnCode t_Result = CoreSDK.ShutDown();
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not shut down SDK" );
			}
		}

		/// <summary>
		/// Temp fix for Manus SDK lingering connection. TODO: Remove when fixed
		/// </summary>
		protected virtual void RestartSDK()
		{
			ShutDown();
			StartUp();
		}

		/// <summary>
		/// Register callbacks for the Manus SDK.
		/// </summary>
		protected virtual void RegisterCallbacks()
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.RegisterCallbackForOnConnectedToCore( OnConnectedToCore );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not initialize SDK" );
			}

			t_Result = CoreSDK.RegisterCallbackForOnDisconnectedFromCore( OnDisconnectedFromCore );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not initialize SDK" );
			}

			t_Result = CoreSDK.RegisterCallbackForLandscapeStream( OnLandscape );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not initialize SDK" );
			}

			t_Result = CoreSDK.RegisterCallbackForSkeletonStream( OnSkeletonUpdate );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not initialize SDK" );
			}

			t_Result = CoreSDK.RegisterCallbackForTrackerStream( OnTrackerUpdate );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not initialize SDK" );
			}

			t_Result = CoreSDK.RegisterCallbackForRawSkeletonStream( OnRawSkeletonUpdate );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not initialize SDK" );
			}
			
			t_Result = CoreSDK.RegisterCallbackForRawDeviceDataStream( OnRawDeviceDataUpdate );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not initialize SDK" );
			}

			t_Result = CoreSDK.RegisterCallbackForErgonomicsStream( OnErgonomicsUpdate );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not initialize SDK" );
			}
			t_Result = CoreSDK.RegisterCallbackForSystemStream( OnSystemUpdate );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not initialize SDK" );
			}

			t_Result = CoreSDK.RegisterCallbackForGestureStream( OnGestureUpdate );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not initialize SDK" );
			}
		}

		/// <summary>
		/// Process the update loop.
		/// </summary>
		protected void RunUpdateLoop()
		{
			System.DateTime t_LastFindTimestamp = System.DateTime.MinValue;
			while( m_RunThread )
			{
				Thread.Sleep( 4 );
				try
				{
					TransitionToState( m_NextState, m_RequestedNextState );

					switch( currentState )
					{
						case State.Disconnected:
							FillSkeletonSendQueue();
							if( (System.DateTime.UtcNow - t_LastFindTimestamp).TotalSeconds > s_HostAutoFindInterval &&
								ManusManager.settings.autoConnect )
							{
								t_LastFindTimestamp = System.DateTime.UtcNow;
								Task<bool> t_Task = FindConnection();
								t_Task.Wait();
								if( t_Task.Result )
								{
									GoToState( State.Connecting );
								}
							}
							break;
						case State.Connecting:
							{
								bool t_Success = false;
								if( m_CurrentHost != null )
								{
									t_Success = ConnectToHost( m_CurrentHost.Value );
								}
								else
								{
									if( ManusManager.settings.connectGRPC )
									{
										t_Success = ConnectGRPC();
									}
								}
								if( !t_Success )
								{
									GoToState( State.Disconnected );
									break;
								}

								ManusManager.settings.lastConnectedHost = m_CurrentHost.Value.hostName;
								ManusManager.settings.Save();

								RetrieveSessionId();
							}
							break;
						case State.Connected:
							if( !m_Playmode ) break;

							UnityMainThreadDispatcher.instance?.Enqueue( () => UpdateSkeletonData() );
							UnityMainThreadDispatcher.instance?.Enqueue( () => UpdateTrackedObjectsData() );
							SendCustomTracker();

							//Go through skeleton queue
							m_SkeletonQueueMutex.WaitOne();
							var t_SklQueue = m_SkeletonQueue;
							m_SkeletonQueue = new List<Skeleton>();
							m_SkeletonQueueMutex.ReleaseMutex();

							for( int i = 0; i < t_SklQueue.Count; i++ )
							{
								SetupSkeleton( t_SklQueue[i] );
							}

							m_TrackedObjectsQueueMutex.WaitOne();
							var t_TrkObjectQueue = m_TrackedObjectsQueue;
							m_TrackedObjectsQueue = new List<TrackedObject>();
							m_TrackedObjectsQueueMutex.ReleaseMutex();
							//Go through tracked object queue
							for( int i = 0; i < t_TrkObjectQueue.Count; i++ )
							{
								RegisterTrackedObject( t_TrkObjectQueue[i] );
							}
							break;
						default:
							GoToState( State.Disconnected );
							break;
					}
				}
				catch( System.Threading.ThreadAbortException )
				{
					m_RunThread = false;
				}
				catch( System.Exception p_Ex )
				{
					Debug.LogError( p_Ex.Message );
				}
			}
		}

		/// <summary>
		/// Change to new state.
		/// There are two ways that this can happen, the first is internal.
		/// The second is requested by a user interaction, this one doesn't have prio over the internal one.
		/// If both states change at the same miraculous time, first handle the internal one,
		/// next time we call this the requested one will probably happen.
		/// </summary>
		/// <param name="p_NextState">New state</param>
		/// <param name="p_RequestedState">Requested state</param>
		void TransitionToState( State p_NextState, State p_RequestedState )
		{
			if( p_NextState == currentState )
			{
				if( currentState == p_RequestedState )
				{
					return;
				}
				p_NextState = p_RequestedState;
			}

			switch( p_NextState )
			{
				case State.Disconnected:
					RestartSDK();
					break;
				default:
					break;
			}

			//so if this state aint triggered by a user, just adjust it.
			if( p_RequestedState == currentState )
			{
				m_RequestedNextState = p_NextState;
			}
			currentState = p_NextState;
		}

		/// <summary>
		/// Set next state for communication hub.
		/// </summary>
		/// <param name="p_State">Next state</param>
		public void GoToState( State p_State )
		{
			m_NextState = p_State;
		}

		/// <summary>
		/// User requests a next state for communication hub.
		/// </summary>
		/// <param name="p_State">Next state</param>
		public void RequestState( State p_State )
		{
			m_RequestedNextState = p_State;
		}

		/// <summary>
		/// Start searching for Manus Core hosts on network.
		/// </summary>
		public bool AutoConnect()
		{
			if( LookForHosts( s_HostFetchWaitDelay, ManusManager.settings.localOnly ) )
			{
				var t_Hosts = GetAvailableHostsFound();
				if( t_Hosts.Count == 0 ) return false;

				m_CurrentHost = null;
				if( ManusManager.settings.localOnly )
				{
					m_CurrentHost = t_Hosts[0];
				}
				else
				{
					string t_LastHostName = ManusManager.settings.lastConnectedHost;
					for( int i = 0; i < t_Hosts.Count; i++ )
					{
						if( t_Hosts[i].hostName.Contains( t_LastHostName ) )
						{
							m_CurrentHost = t_Hosts[i];
							break;
						}
					}
				}

				if( m_CurrentHost == null )
					m_CurrentHost = t_Hosts[0];

				RequestState( State.Connecting ); // this is a user function. so request, don't go to.
				return true;
			}
			return false;
		}

		/// <summary>
		/// Start searching for Manus Core hosts on network.
		/// </summary>
		public async Task<bool> SearchAndFetchHosts()
		{
			if( !await LookForHostsAsync( s_HostFetchWaitDelay, ManusManager.settings.localOnly ) )
			{
				return false;
			}
			m_HostsFound = await GetAvailableHostsFoundAsync();
			UnityMainThreadDispatcher.instance?.Enqueue( () => onHostsFound.Invoke() );

			return true;
		}

		/// <summary>
		/// Handle connection to Manus Core.
		/// </summary>
		protected async Task<bool> FindConnection()
		{
			//Search for available hosts
			if( !await SearchAndFetchHosts() )
			{
				return false;
			}

			if( ManusManager.settings.autoConnect && currentState == m_NextState )
			{
				//Attempt to connect, if succeeds don't bother waiting to try again
				if( m_HostsFound.Count != 0 )
				{
					m_CurrentHost = null;

					if( ManusManager.settings.localOnly )
					{
						m_CurrentHost = m_HostsFound[0];
					}
					else
					{
						string t_LastHostName = ManusManager.settings.lastConnectedHost;
						for( int i = 0; i < m_HostsFound.Count; i++ )
						{
							if( m_HostsFound[i].hostName.Contains( t_LastHostName ) )
							{
								m_CurrentHost = m_HostsFound[i];
								break;
							}
						}
					}

					if( m_CurrentHost == null )
						m_CurrentHost = m_HostsFound[0];

					m_HostsFound.Clear();
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Connect to a specific Manus Core host.
		/// </summary>
		/// <param name="p_Host">Host to connect to</param>
		public void Connect( CoreSDK.ManusHost p_Host )
		{
			m_CurrentHost = p_Host;
			RequestState( State.Connecting ); // this is a user function. so request, don't go to.
		}

		/// <summary>
		/// Set Communication hub to disconnect from Manus Core.
		/// </summary>
		public void Disconnect()
		{
			RequestState( State.Disconnected ); // this is a user function. so request, don't go to.
			onDisconnectedFromCore.Invoke();
		}

		/// <summary>
		/// Unload a skeleton from Manus Core.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to unload</param>
		public void UnloadSkeleton( Skeleton p_Skeleton )
		{
			if( currentState != State.Connected )
			{
				m_Skeletons.Remove( p_Skeleton.skeletonData.id );
				return;
			}

			CoreSDK.SDKReturnCode t_Result = CoreSDK.UnloadSkeleton( p_Skeleton.skeletonData.id );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not remove unused skeleton." );
				return;
			}
			m_Skeletons.Remove( p_Skeleton.skeletonData.id );
		}

		/// <summary>
		/// Setup skeleton for Manus Core and load it.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to setup and load</param>
		public void SetupSkeleton( Skeleton p_Skeleton )
		{
			if( !BuildSkeletonSetup( p_Skeleton, out uint t_SklSetupIdx, true ) )
			{
				return;
			}

			CoreSDK.SDKReturnCode t_Result = CoreSDK.LoadSkeleton( t_SklSetupIdx, out uint t_SkeletonId );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not load skeleton." );
				return;
			}

			p_Skeleton.skeletonData.id = t_SkeletonId;
			p_Skeleton.sklSetupIdx = null;

			if( !m_Skeletons.ContainsKey( t_SkeletonId ) )
			{
				m_Skeletons.Add( t_SkeletonId, p_Skeleton );
			}
		}

		/// <summary>
		/// Get all temporary skeletons from Manus Core.
		/// </summary>
		/// <returns>All temporary skeletons in Manus Core</returns>
		public List<CoreSDK.TemporarySkeletonsInfoForSession> GetTemporarySkeletons()
		{
			List<CoreSDK.TemporarySkeletonsInfoForSession> t_SkeletonsSessions = new List<CoreSDK.TemporarySkeletonsInfoForSession>();
			CoreSDK.TemporarySkeletonCountForAllSessions t_TemporarySkeletonCountForSessions = new CoreSDK.TemporarySkeletonCountForAllSessions();
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GetTemporarySkeletonCountForAllSessions( ref t_TemporarySkeletonCountForSessions );
			if( t_Result == CoreSDK.SDKReturnCode.NotConnected || t_Result == CoreSDK.SDKReturnCode.SdkNotAvailable )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get temporary skeleton count for sessions." );
				return null;
			}
			if( t_TemporarySkeletonCountForSessions.temporarySkeletonCountForSessions == null ) return t_SkeletonsSessions;

			for( int i = 0; i < t_TemporarySkeletonCountForSessions.sessionsCount; i++ )
			{
				if( t_TemporarySkeletonCountForSessions.temporarySkeletonCountForSessions[i].skeletonCount == 0 ) continue;

				CoreSDK.TemporarySkeletonsInfoForSession t_TempSkeletonsInfo = new CoreSDK.TemporarySkeletonsInfoForSession();
				t_Result = CoreSDK.GetTemporarySkeletonsInfoForSession( t_TemporarySkeletonCountForSessions.temporarySkeletonCountForSessions[i].sessionId, ref t_TempSkeletonsInfo );
				if( t_Result != CoreSDK.SDKReturnCode.Success )
				{
					Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get temporary skeletons for sessions {t_TemporarySkeletonCountForSessions.temporarySkeletonCountForSessions[i].sessionId}" +
						$" - {t_TemporarySkeletonCountForSessions.temporarySkeletonCountForSessions[i].sessionName}." );
					continue;
				}

				uint t_SessionId = t_TemporarySkeletonCountForSessions.temporarySkeletonCountForSessions[i].sessionId;
				string t_SessionName = t_TemporarySkeletonCountForSessions.temporarySkeletonCountForSessions[i].sessionName;
				List<CoreSDK.TemporarySkeletonInfo> t_SkeletonInfos = new List<CoreSDK.TemporarySkeletonInfo>();
				for( int j = 0; j < t_TemporarySkeletonCountForSessions.temporarySkeletonCountForSessions[i].skeletonCount; j++ )
				{
					t_SkeletonInfos.Add( new CoreSDK.TemporarySkeletonInfo
					{
						name = t_TempSkeletonsInfo.skeletonInfo[j].name,
						index = t_TempSkeletonsInfo.skeletonInfo[j].index
					} );
				}
				t_SkeletonsSessions.Add( new CoreSDK.TemporarySkeletonsInfoForSession
				{
					sessionId = t_SessionId,
					sessionName = t_SessionName,
					skeletonCount = t_TemporarySkeletonCountForSessions.temporarySkeletonCountForSessions[i].skeletonCount,
					skeletonInfo = t_SkeletonInfos.ToArray()
				} );
			}
			return t_SkeletonsSessions;
		}

		/// <summary>
		/// Build a temporary skeleton and save to Manus Core.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to build and load</param>
		/// <param name="p_IsSkeletonModified">Whether skeleton has been modified</param>
		public bool SaveTemporarySkeleton( Skeleton p_Skeleton, bool p_IsSkeletonModified )
		{
			if( !BuildSkeletonSetup( p_Skeleton, out uint t_SklSetupIdx, currentState != State.Connected ) )
			{
				return false;
			}
			p_Skeleton.sklSetupIdx = t_SklSetupIdx;
			m_SentTempSkeletons.Add( p_Skeleton );
			if( p_Skeleton.sessionId == null )
			{
				p_Skeleton.sessionId = m_SessionId;
			}

			CoreSDK.SDKReturnCode t_Result =
				CoreSDK.SaveTemporarySkeleton( p_Skeleton.sklSetupIdx.Value, p_Skeleton.sessionId.Value, p_IsSkeletonModified );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not save TemporarySkeleton." );
				return false;
			}
			Debug.Log( $"MANUS-DEBUG: Saved TemporarySkeleton in session: {p_Skeleton.sessionId} with index: {p_Skeleton.sklSetupIdx}" );
			return true;
		}

		/// <summary>
		/// Build a temporary skeleton and save to Manus Core.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to build and load</param>
		/// <param name="p_Meshes">Meshes that belong to the skeleton and you want to load</param>
		/// <param name="p_IsSkeletonModified">Whether skeleton has been modified</param>
		public bool SaveTemporarySkeleton( Skeleton p_Skeleton, List<MeshSetup> p_Meshes, bool p_IsSkeletonModified )
		{
			p_Skeleton.skeletonData.meshes = p_Meshes;
			if( !BuildSkeletonSetup( p_Skeleton, out uint t_SklSetupIdx, currentState != State.Connected ) )
			{
				p_Skeleton.skeletonData.meshes.Clear();
				return false;
			}
			p_Skeleton.sklSetupIdx = t_SklSetupIdx;
			m_SentTempSkeletons.Add( p_Skeleton );
			if( p_Skeleton.sessionId == null )
			{
				p_Skeleton.sessionId = m_SessionId;
			}

			CoreSDK.SDKReturnCode t_Result =
				CoreSDK.SaveTemporarySkeleton( p_Skeleton.sklSetupIdx.Value, p_Skeleton.sessionId.Value, p_IsSkeletonModified );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not save TemporarySkeleton." );
				return false;
			}
			Debug.Log( $"MANUS-DEBUG: Saved TemporarySkeleton in session: {p_Skeleton.sessionId} with index: {p_Skeleton.sklSetupIdx}" );
			return true;
		}

		public bool SaveTemporarySkeletonToFile( Skeleton p_Skeleton, string p_PathName )
		{
			if( !BuildSkeletonSetup( p_Skeleton, out uint t_SklSetupIdx, currentState != State.Connected ) )
			{
				return false;
			}
			p_Skeleton.sklSetupIdx = t_SklSetupIdx;
			m_SentTempSkeletons.Add( p_Skeleton );
			if( p_Skeleton.sessionId == null )
			{
				p_Skeleton.sessionId = m_SessionId;
			}
			byte[] t_Bytes;
			CoreSDK.SDKReturnCode t_Result =
				CoreSDK.GetCompressedTemporarySkeletonData( p_Skeleton.sklSetupIdx.Value, p_Skeleton.sessionId.Value, out t_Bytes );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not compress temporary skeleton data." );
				return false;
			}
			try
			{
				System.IO.File.WriteAllBytes( p_PathName, t_Bytes );
			}
			catch( System.Exception )
			{
				Debug.LogError( $"MANUS-ERROR: file could not be written to ({p_PathName})." );
				return false;
			}
			Debug.Log( $"MANUS-DEBUG: TemporarySkeleton in session: {p_Skeleton.sessionId} with index: {p_Skeleton.sklSetupIdx} compressed" );
			return true;
		}

		public bool LoadTemporarySkeletonFromFile( Skeleton p_Skeleton, string p_PathName )
		{
			byte[] t_Data;
			try
			{
				t_Data = System.IO.File.ReadAllBytes( p_PathName );
			}
			catch( System.Exception )
			{
				Debug.LogError( $"MANUS-ERROR: File could not be read ({p_PathName})." );
				return false;
			}

			// create a skeleton setup where to save the retrieved skeleton
			if( !BuildSkeletonSetup( p_Skeleton, out uint t_SklSetupIdx, false ) )
			{
				return false;
			}
			p_Skeleton.sklSetupIdx = t_SklSetupIdx;
			m_SentTempSkeletons.Add( p_Skeleton );
			if( p_Skeleton.sessionId == null )
			{
				p_Skeleton.sessionId = m_SessionId;
			}

			CoreSDK.SDKReturnCode t_Result =
				CoreSDK.GetTemporarySkeletonFromCompressedData( p_Skeleton.sklSetupIdx.Value, p_Skeleton.sessionId.Value, t_Data );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get temporary skeleton from compressed data." );
				return false;
			}

			if( !GetSkeletonData( p_Skeleton, p_Skeleton.sklSetupIdx.Value, p_Skeleton.sessionId.Value ) )
			{
				return false;
			}
			return true;
		}

		public virtual bool GetSkeletonData( Skeleton p_Skeleton, uint p_SklSetupIdx, uint p_SessionId )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GetSkeletonSetupInfo( p_SklSetupIdx, out CoreSDK.SkeletonSetupInfo p_SkeletonSetupInfo );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Skeleton Setup Info." );
				return false;
			}
			p_Skeleton.skeletonData.id = p_SkeletonSetupInfo.id;
			p_Skeleton.skeletonData.type = p_SkeletonSetupInfo.type;
			p_Skeleton.skeletonData.settings = p_SkeletonSetupInfo.settings;
			p_Skeleton.skeletonData.name = p_SkeletonSetupInfo.name;

			t_Result = CoreSDK.GetSkeletonSetupNodes( p_SklSetupIdx, out CoreSDK.NodeSetup[] t_NodeSetups );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Nodes Setups." );
				return false;
			}

			List<Node> t_Nodes = new List<Node>();
			foreach( CoreSDK.NodeSetup t_NodeSetup in t_NodeSetups )
			{
				Node t_Node = t_NodeSetup.FromNodeSetup( t_Nodes, p_Skeleton.skeletonData );
				if( t_Node == null )
				{
					Debug.LogError( $"MANUS-ERROR: Could not get skeleton data, node mismatch." );
					return false;
				}
				t_Nodes.Add( t_Node );
			}

			p_Skeleton.skeletonData.nodes = t_Nodes;

			t_Result = CoreSDK.GetSkeletonSetupChains( p_SklSetupIdx, out CoreSDK.ChainSetup[] t_ChainSetups );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Chains Setups." );
				return false;
			}

			List<Chain> t_Chains = new List<Chain>();
			foreach( CoreSDK.ChainSetup t_ChainSetup in t_ChainSetups )
			{
				t_Chains.Add( t_ChainSetup.FromChainSetup() );
			}

			p_Skeleton.skeletonData.chains = t_Chains;

			t_Result = CoreSDK.GetSkeletonSetupColliders( p_SklSetupIdx, out CoreSDK.ColliderSetup[] t_ColliderSetups );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Collider Setups." );
				return false;
			}

			List<ColliderSetup> t_Colliders = new List<ColliderSetup>();
			foreach( CoreSDK.ColliderSetup t_ColliderSetup in t_ColliderSetups )
			{
				t_Colliders.Add( t_ColliderSetup.FromColliderSetup() );
			}

			p_Skeleton.skeletonData.colliders = t_Colliders;

			return true;
		}

		/// <summary>
		/// Load temporary skeleton from Manus Core to plugin skeleton.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to load to</param>
		/// <param name="p_SklSetupIdx">Skeleton setup index to load from</param>
		/// <param name="p_SessionId">Session ID to load from</param>
		/// <returns>Whether action succeeded</returns>
		public bool LoadTemporarySkeleton( Skeleton p_Skeleton, uint p_SklSetupIdx, uint p_SessionId )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GetTemporarySkeleton( p_SklSetupIdx, p_SessionId );
			if( t_Result == CoreSDK.SDKReturnCode.NotConnected || t_Result == CoreSDK.SDKReturnCode.SdkNotAvailable )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not Load Temporary Skeleton." );
				return false;
			}

			if( !GetSkeletonData( p_Skeleton, p_SklSetupIdx, p_SessionId ) )
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Load in skeleton from development dashboard.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to load to</param>
		/// <param name="p_SklSetupIdx">Skeleton setup index to load from</param>
		public void LoadSkeletonFromTool( Skeleton p_Skeleton, uint p_SklSetupIdx )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GetTemporarySkeleton( p_SklSetupIdx, m_SessionId );
			if( t_Result == CoreSDK.SDKReturnCode.NotConnected || t_Result == CoreSDK.SDKReturnCode.SdkNotAvailable )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not Load Temporary Skeleton." );
				return;
			}

			//todo: replace it with GetSkeletonData

			t_Result = CoreSDK.GetSkeletonSetupNodes( p_SklSetupIdx, out CoreSDK.NodeSetup[] t_NodeSetups );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Nodes Setups." );
				return;
			}

			//Extract node setups and settings
			List<Node> t_Nodes = new List<Node>();
			foreach( CoreSDK.NodeSetup t_NodeSetup in t_NodeSetups )
			{
				p_Skeleton.skeletonData.GetNodeWithId( t_NodeSetup.id ).settings = t_NodeSetup.settings;
			}

			t_Result = CoreSDK.GetSkeletonSetupChains( p_SklSetupIdx, out CoreSDK.ChainSetup[] t_ChainSetups );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Chains Setups." );
				return;
			}

			List<Chain> t_Chains = new List<Chain>();
			foreach( CoreSDK.ChainSetup t_ChainSetup in t_ChainSetups )
			{
				t_Chains.Add( t_ChainSetup.FromChainSetup() );
			}
			p_Skeleton.skeletonData.chains = t_Chains;
		}

		/// <summary>
		/// Adds a manually created chain to Manus Core skeleton.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to add chain to</param>
		/// <param name="p_ChainSetup">Chain setup to add to skeleton</param>
		public void AddChain( Skeleton p_Skeleton, CoreSDK.ChainSetup p_ChainSetup )
		{
			Debug.Log( $"[CommunicationHub] Adding Chain with {p_ChainSetup.nodeIds.Length} nodes" );

			CoreSDK.SDKReturnCode t_Result = CoreSDK.AddChainToSkeletonSetup( p_Skeleton.sklSetupIdx.Value, p_ChainSetup );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not Manually add Chain." );
				return;
			}

			t_Result = CoreSDK.GetSkeletonSetupChains( p_Skeleton.sklSetupIdx.Value, out CoreSDK.ChainSetup[] t_ChainSetups );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Chains Setups." );
				return;
			}

			List<Chain> t_Chains = new List<Chain>();
			foreach( CoreSDK.ChainSetup t_CoreChainSetup in t_ChainSetups )
			{
				t_Chains.Add( t_CoreChainSetup.FromChainSetup() );
			}

			p_Skeleton.skeletonData.chains = t_Chains;
		}

		/// <summary>
		/// Overwrite a chain within Manus Core skeleton.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to modify chain in</param>
		/// <param name="p_ChainSetup">Chain setup to add to skeleton</param>
		public void OverwriteChain( Skeleton p_Skeleton, CoreSDK.ChainSetup p_ChainSetup )
		{
			Debug.Log( $"[CommunicationHub] Overwriting Chain with {p_ChainSetup.nodeIds.Length} nodes" );
			CoreSDK.SDKReturnCode t_Result = CoreSDK.OverwriteChainToSkeletonSetup( p_Skeleton.sklSetupIdx.Value, p_ChainSetup );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not Manually add Chain." );
				return;
			}

			t_Result = CoreSDK.GetSkeletonSetupChains( p_Skeleton.sklSetupIdx.Value, out CoreSDK.ChainSetup[] t_ChainSetups );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Chains Setups." );
				return;
			}

			List<Chain> t_Chains = new List<Chain>();
			foreach( CoreSDK.ChainSetup t_CoreChainSetup in t_ChainSetups )
			{
				t_Chains.Add( t_CoreChainSetup.FromChainSetup() );
			}

			p_Skeleton.skeletonData.chains = t_Chains;
		}


		/// <summary>
		/// Setup chains on skeleton.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to allocate chains for</param>
		public bool AllocateChains( Skeleton p_Skeleton )
		{
			if( !BuildSkeletonSetup( p_Skeleton, out uint t_SklSetupIdx, currentState != State.Connected ) )
			{
				return false;
			}
			p_Skeleton.sklSetupIdx = t_SklSetupIdx;
			m_SentTempSkeletons.Add( p_Skeleton );

			CoreSDK.SDKReturnCode t_Result = CoreSDK.AllocateChains( p_Skeleton.sklSetupIdx.Value );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not allocate chains." );
				return false;
			}

			t_Result = CoreSDK.GetSkeletonSetupNodes( p_Skeleton.sklSetupIdx.Value, out CoreSDK.NodeSetup[] p_NodeSetups );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Node Setups." );
				return false;
			}

			foreach( CoreSDK.NodeSetup t_NodeSetup in p_NodeSetups )
			{
				var t_Node = p_Skeleton.skeletonData.GetNodeWithId( t_NodeSetup.id );
				t_Node.settings = t_NodeSetup.settings;
			}

			t_Result = CoreSDK.GetSkeletonSetupChains( p_Skeleton.sklSetupIdx.Value, out CoreSDK.ChainSetup[] t_ChainSetups );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Chains Setups." );
				return false;
			}

			List<Chain> t_Chains = new List<Chain>();
			foreach( CoreSDK.ChainSetup t_ChainSetup in t_ChainSetups )
			{
				t_Chains.Add( t_ChainSetup.FromChainSetup() );
			}

			p_Skeleton.skeletonData.chains = t_Chains;
			return true;
		}

		/// <summary>
		/// Prepare skeleton.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to allocate chains for</param>
		public bool PrepareSkeleton( Skeleton p_Skeleton )
		{
			if( !BuildSkeletonSetup( p_Skeleton, out uint t_SklSetupIdx, currentState != State.Connected ) )
			{
				return false;
			}
			p_Skeleton.sklSetupIdx = t_SklSetupIdx;
			m_SentTempSkeletons.Add( p_Skeleton );

			CoreSDK.SDKReturnCode t_Result = CoreSDK.PrepareSkeletonSetup( p_Skeleton.sklSetupIdx.Value );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not allocate chains." );
				return false;
			}

			t_Result = CoreSDK.GetSkeletonSetupNodes( p_Skeleton.sklSetupIdx.Value, out CoreSDK.NodeSetup[] p_NodeSetups );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Node Setups." );
				return false;
			}

			foreach( CoreSDK.NodeSetup t_NodeSetup in p_NodeSetups )
			{
				var t_Node = p_Skeleton.skeletonData.GetNodeWithId( t_NodeSetup.id );
				t_Node.settings = t_NodeSetup.settings;
			}

			t_Result = CoreSDK.GetSkeletonSetupChains( p_Skeleton.sklSetupIdx.Value, out CoreSDK.ChainSetup[] t_ChainSetups );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get Chains Setups." );
				return false;
			}

			List<Chain> t_Chains = new List<Chain>();
			foreach( CoreSDK.ChainSetup t_ChainSetup in t_ChainSetups )
			{
				t_Chains.Add( t_ChainSetup.FromChainSetup() );
			}

			p_Skeleton.skeletonData.chains = t_Chains;
			return true;
		}

		/// <summary>
		/// Build skeleton setup for sending to Manus Core.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to build setup for</param>
		/// <param name="p_SklSetupIdx">Skeleton setup index was built for</param>
		/// <param name="p_QueueForLater">Queue again for later on fail</param>
		/// <returns>Whether action succeeded</returns>
		protected bool BuildSkeletonSetup( Skeleton p_Skeleton, out uint p_SklSetupIdx, bool p_QueueForLater )
		{
			if( !SetupSkeletonTarget( p_Skeleton ) && p_QueueForLater )
			{
				m_SkeletonQueueMutex.WaitOne();
				m_SkeletonQueue.Add( p_Skeleton );
				m_SkeletonQueueMutex.ReleaseMutex();
				p_SklSetupIdx = 0;
				return false;
			}

			CoreSDK.SkeletonSetupInfo t_SkeletonSetup = p_Skeleton.skeletonData.ToSkeletonSetup();
			CoreSDK.SDKReturnCode t_Result = CoreSDK.SDKReturnCode.Error;
			if( p_Skeleton.sklSetupIdx == null )
			{
				t_Result = CoreSDK.CreateSkeletonSetup( ref t_SkeletonSetup, out p_SklSetupIdx );
#if UNITY_EDITOR
				if( t_Result == CoreSDK.SDKReturnCode.ArgumentSizeMismatch )
				{
					if( UnityEditor.EditorUtility.DisplayDialog( "Max amount of temporary skeletons reached.",
									"Would you like to clear the existing temporary skeletons to make room for new ones?\n" +
									"You can also clear this later on from the Manus/Communication Hub menu." +
									"This should NOT happen during normal Play Mode!", "Clear", "Cancel" ) )
					{
						ClearAllTemporarySkeletons();
						t_Result = CoreSDK.CreateSkeletonSetup( ref t_SkeletonSetup, out p_SklSetupIdx );
					}
				}
#endif
			}
			else
			{
				p_SklSetupIdx = p_Skeleton.sklSetupIdx.Value;
				t_Result = CoreSDK.OverwriteSkeletonSetup( p_SklSetupIdx, ref t_SkeletonSetup );
			}

			if( t_Result != CoreSDK.SDKReturnCode.Success && p_QueueForLater )
			{
				m_SkeletonQueueMutex.WaitOne();
				Debug.Log( $"MANUS-DEBUG: Adding Skeleton to the queue ({m_SkeletonQueue.Count})" );
				m_SkeletonQueue.Add( p_Skeleton );
				m_SkeletonQueueMutex.ReleaseMutex();
				return false;
			}
			Debug.Log( $"MANUS-DEBUG: Created SkeletonSetup with index {p_SklSetupIdx}" );


			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not create skeleton setup." );
				return false;
			}

			foreach( var t_Node in p_Skeleton.skeletonData.nodes )
			{
				CoreSDK.NodeSetup t_NodeSetup = t_Node.ToNodeSetup();
				t_Result = CoreSDK.AddNodeToSkeletonSetup( p_SklSetupIdx, t_NodeSetup );
				if( t_Result != CoreSDK.SDKReturnCode.Success )
				{
					Debug.LogError( $"MANUS-ERROR: {t_Result}, could not add node to skeleton setup." );
					return false;
				}
			}

			foreach( var t_Chain in p_Skeleton.skeletonData.chains )
			{
				CoreSDK.ChainSetup t_ChainSetup = t_Chain.ToChainSetup();
				t_Result = CoreSDK.AddChainToSkeletonSetup( p_SklSetupIdx, t_ChainSetup );
				if( t_Result != CoreSDK.SDKReturnCode.Success )
				{
					Debug.LogError( $"MANUS-ERROR: {t_Result}, could not add chain to skeleton setup." );
					return false;
				}
			}

			foreach( var t_Collider in p_Skeleton.skeletonData.colliders )
			{
				CoreSDK.ColliderSetup t_ColliderSetup = t_Collider.ToColliderSetup();
				t_Result = CoreSDK.AddColliderToSkeletonSetup( p_SklSetupIdx, t_ColliderSetup );
				if( t_Result != CoreSDK.SDKReturnCode.Success )
				{
					Debug.LogError( $"MANUS-ERROR: {t_Result}, could not add collider to skeleton setup." );
					return false;
				}
			}

			if( p_Skeleton.skeletonData.meshes != null )
			{
				foreach( var t_Mesh in p_Skeleton.skeletonData.meshes )
				{
					t_Result = CoreSDK.AddMeshSetupToSkeletonSetup( p_SklSetupIdx, t_Mesh.nodeId, out uint t_MeshSetupIndex );
					if( t_Result != CoreSDK.SDKReturnCode.Success )
					{
						Debug.LogError( $"MANUS-ERROR: {t_Result}, could not add mesh setup to skeleton setup." );
						return false;
					}

					foreach( var t_Vertex in t_Mesh.vertices )
					{
						t_Result = CoreSDK.AddVertexToMeshSetup( p_SklSetupIdx, t_MeshSetupIndex, t_Vertex );
						if( t_Result != CoreSDK.SDKReturnCode.Success )
						{
							Debug.LogError( $"MANUS-ERROR: {t_Result}, could not add vertex to mesh setup." );
							return false;
						}
					}

					foreach( var t_Triangle in t_Mesh.triangles )
					{
						t_Result = CoreSDK.AddTriangleToMeshSetup( p_SklSetupIdx, t_MeshSetupIndex, t_Triangle );
						if( t_Result != CoreSDK.SDKReturnCode.Success )
						{
							Debug.LogError( $"MANUS-ERROR: {t_Result}, could not add triangle to mesh setup." );
							return false;
						}
					}
				}
				p_Skeleton.skeletonData.meshes.Clear();
			}

			return true;
		}

		/// <summary>
		/// Setup skeleton targeting settings for Manus Core.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to setup settings for</param>
		/// <returns>Whether action succeeded</returns>
		public bool SetupSkeletonTarget( Skeleton p_Skeleton )
		{
			CoreSDK.SDKReturnCode t_Result;
			bool t_CoreConnected;
			t_Result = CoreSDK.GetIsConnectedToCore( out t_CoreConnected );
			if( t_Result != CoreSDK.SDKReturnCode.Success || !t_CoreConnected ) return false;
			switch( p_Skeleton.skeletonData.settings.targetType )
			{
				case CoreSDK.SkeletonTargetType.Invalid:
					break;
				case CoreSDK.SkeletonTargetType.UserData:
					if( p_Skeleton.skeletonData.settings.skeletonTargetUserData.userID == 0 )
					{
						t_Result = CoreSDK.GetNumberOfAvailableUsers( out uint t_UserCount );
						if( t_Result != CoreSDK.SDKReturnCode.Success )
						{
							return false;
						}
						if( t_UserCount == 0 )
						{
							return false;
						}
						t_Result = CoreSDK.GetIdsOfAvailableUsers( out uint[] t_UserIDs, t_UserCount );
						if( t_Result != CoreSDK.SDKReturnCode.Success )
						{
							return false;
						}
						p_Skeleton.skeletonData.settings.targetType = CoreSDK.SkeletonTargetType.UserData;
						p_Skeleton.skeletonData.settings.skeletonTargetUserData.userID = t_UserIDs[0];
						return true;
					}
					break;
				case CoreSDK.SkeletonTargetType.UserIndexData:
					break;
				case CoreSDK.SkeletonTargetType.AnimationData:
					break;
				case CoreSDK.SkeletonTargetType.GloveData:
					break;
			}
			return true;
		}

		/// <summary>
		/// Fetch first available user ID from Manus Core.
		/// </summary>
		/// <returns>First available user id</returns>
		public uint FirstAvailableUser()
		{
			if( currentState != State.Connected )
			{
				return 0;
			}

			CoreSDK.SDKReturnCode t_Result;
			t_Result = CoreSDK.GetNumberOfAvailableUsers( out uint t_UserCount );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not fetch users count." );
				return 0;
			}
			if( t_UserCount == 0 )
			{
				return 0;
			}
			t_Result = CoreSDK.GetIdsOfAvailableUsers( out uint[] t_UserIDs, t_UserCount );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not fetch user IDs." );
				return 0;
			}
			return t_UserIDs[0];
		}

		/// <summary>
		/// Pair glove to first availabled dongle asynchroniously.
		/// </summary>
		/// <param name="p_GloveID">Glove id</param>
		public async Task<bool> PairGloveAsync( uint p_GloveId )
		{
			return await Task.Run( () =>
			{
				bool t_Return = PairGlove( p_GloveId);
				return t_Return;
			} );
		}

		/// <summary>
		/// Pair glove to first available dongle.
		/// </summary>
		/// <param name="p_GloveID">Glove id</param>
		public bool PairGlove( uint p_GloveID )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.PairGlove( p_GloveID, out bool p_Success);
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not pair glove." );
			}
			return p_Success;
		}

		/// <summary>
		/// Pair glove to specified dongle asynchroniously.
		/// </summary>
		/// <param name="p_GloveID">Glove id</param>
		public async Task<bool> PairGloveAsync( uint p_GloveId, uint p_DongleID )
		{
			return await Task.Run( () =>
			{
				bool t_Return = PairGlove( p_GloveId, p_DongleID );
				return t_Return;
			} );
		}


		/// <summary>
		/// Pair glove to specified dongle.
		/// </summary>
		/// <param name="p_GloveID">Glove id</param>
		/// <param name="p_DongleID">Glove id</param>
		public bool PairGlove( uint p_GloveID, uint p_DongleID )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.PairGloveToDongle( p_GloveID, p_DongleID, out bool p_Success);
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not pair glove." );
			}
			return p_Success;
		}

		/// <summary>
		/// Unpair glove asynchroniously.
		/// </summary>
		/// <param name="p_GloveID">Glove id</param>
		public async Task<bool> UnpairGloveAsync( uint p_GloveId )
		{
			return await Task.Run( () =>
			{
				bool t_Return = UnpairGlove( p_GloveId );
				return t_Return;
			} );
		}

		/// <summary>
		/// Unpair glove
		/// </summary>
		/// <param name="p_GloveID">Glove id</param>
		public bool UnpairGlove( uint p_GloveID )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.UnpairGlove( p_GloveID, out bool p_Success);
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not unpair glove." );
			}
			return p_Success;
		}

		/// <summary>
		/// Set dongle license asynchroniously.
		/// </summary>
		/// <param name="p_DongleID">Dongle id</param>
		/// <param name="p_LicenseString">license string</param>
		public async Task<CommandResponse> SetLicenseAsync( uint p_DongleID, string p_LicenseString )
		{
			return await Task.Run( () =>
			{
				return SetLicense( p_DongleID, p_LicenseString );
			} );
		}

		/// <summary>
		/// Set dongle license
		/// </summary>
		/// <param name="p_DongleID">Dongle id</param>
		/// <param name="p_LicenseString">license string</param>
		public CommandResponse SetLicense( uint p_DongleID, string p_LicenseString )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.SetLicense( p_DongleID, p_LicenseString, out bool p_Result, out string p_Response);
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not set dongle license." );
			}
			return new CommandResponse(p_Result, p_Response);
		}

		/// <summary>
		/// Update device firmware asynchroniously.
		/// </summary>
		/// <param name="p_GloveID">Glove id</param>
		public async Task<CommandResponse> UpdateFirmwareAsync( uint p_DeviceId )
		{
			return await Task.Run( () =>
			{
				return UpdateFirmware( p_DeviceId );
			} );
		}

		/// <summary>
		/// Update device firmware 
		/// </summary>
		/// <param name="p_GloveID">Glove id</param>
		public CommandResponse UpdateFirmware( uint p_DeviceId )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.UpdateFirmware( p_DeviceId, out bool p_Result, out string p_Response);
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not update device firmware." );
			}
			return new CommandResponse( p_Result, p_Response );
		}


		/// <summary>
		/// Do we have haptics support on a specific hand given the skeleton id.
		/// </summary>
		/// <param name="p_SkeletonId">Skeleton id</param>
		/// <param name="p_HandType">Hand type to send to</param>
		public bool DoesSkeletonGloveSupportHaptics( uint p_SkeletonID, CoreSDK.Side p_Side )
		{
			bool t_Haptics;
			CoreSDK.SDKReturnCode t_Result = CoreSDK.DoesSkeletonGloveSupportHaptics( p_SkeletonID, p_Side, out t_Haptics );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not fetch haptic support for skeleton glove." );
			}
			return t_Haptics;
		}

		/// <summary>
		/// Activate haptics on specific hand given the skeleton id.
		/// </summary>
		/// <param name="p_SkeletonId">Skeleton id</param>
		/// <param name="p_HandType">Hand type to send to</param>
		/// <param name="p_Powers">Strength to send to hand</param>
		public void SendHapticDataForSkeleton( uint p_SkeletonId, CoreSDK.Side p_HandType, float[] p_Powers )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.VibrateFingersForSkeleton( p_SkeletonId, p_HandType, p_Powers );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not vibrate fingers." );
				return;
			}
		}

		/// <summary>
		/// Activate haptics on specific hand given the glove id.
		/// </summary>
		/// <param name="p_SkeletonId">Glove id</param>
		/// <param name="p_Powers">Strength to send to hand</param>
		public void SendHapticDataForGlove( uint p_GloveID, float[] p_Powers )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.VibrateFingersForGlove( p_GloveID, p_Powers );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not vibrate fingers for glove." );
				return;
			}
		}

		/// Sets the hand motion of all raw skeletons in core
		/// </summary>
		/// <param name="p_HandMotions">Type of motion</param>
		/// <returns></returns>
		public bool SetRawSkeletonHandMotion( CoreSDK.HandMotion p_HandMotions )
		{
			var t_Result = CoreSDK.SetRawSkeletonHandMotion( p_HandMotions );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not set raw skeleton hand motion: {p_HandMotions}." );
				return false;
			}

			return true;
		}

		/// Sets the hand motion of all raw skeletons in core
		/// </summary>
		/// <param name="p_HandMotions">Type of motion</param>
		/// <returns></returns>
		public bool GetRawSkeletonHandMotion( out CoreSDK.HandMotion p_HandMotions )
		{
			var t_Result = CoreSDK.GetRawSkeletonHandMotion( out p_HandMotions );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not set raw skeleton hand motion: {p_HandMotions}." );
				return false;
			}

			return true;
		}

		/// <summary>
		/// Get the node info  for the raw skeleton of a glove
		/// </summary>
		/// <param name="p_GloveId">Glove id</param>
		/// <param name="p_NodesInfo">Array of all the nodes and its parent</param>
		/// <returns></returns>
		public bool GetRawSkeletonNodeInfo( uint p_GloveId, out CoreSDK.NodeInfo[] p_NodesInfo )
		{
			var t_Result = CoreSDK.GetRawSkeletonNodeInfo( p_GloveId, out p_NodesInfo );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get node info for raw skeleton." );
				return false;
			}

			return true;
		}

		/// <summary>
		/// Fill skeleton queue to later resend to Manus Core when connection is lost.
		/// </summary>
		public void FillSkeletonSendQueue()
		{
			if( m_Skeletons.Count == 0 ) return;
			m_SkeletonQueueMutex.WaitOne();
			foreach( KeyValuePair<uint, Skeleton> t_SkeletonKey in m_Skeletons )
			{
				m_SkeletonQueue.Add( t_SkeletonKey.Value );
			}
			m_SkeletonQueueMutex.ReleaseMutex();

			m_Skeletons.Clear();
		}

		/// <summary>
		/// Removes all temporary skeletons from the temporary skeleton list.
		/// If you actually get here you've probably been messing with too many skeletons without finishing them.
		/// </summary>
		public void ClearAllTemporarySkeletons()
		{
			for( int i = 0; i < m_SentTempSkeletons.Count; i++ )
			{
				m_SentTempSkeletons[i].sklSetupIdx = null;
				m_SentTempSkeletons[i].sessionId = null;
			}
			m_SentTempSkeletons.Clear();
			CoreSDK.ClearAllTemporarySkeletons();
		}

		/// <summary>
		/// This function gets called when connection to Manus Core is established.
		/// </summary>
		/// <param name="p_Host">Host connected to</param>
		protected void OnConnectedToCore( CoreSDK.ManusHost p_Host )
		{
			GoToState( State.Connected );
			UnityMainThreadDispatcher.instance?.Enqueue( () => onConnectedToCore.Invoke() );
		}

		/// <summary>
		/// This function gets called when connection to Manus Core is lost.
		/// </summary>
		/// <param name="p_Host">Host disconnected from</param>
		protected void OnDisconnectedFromCore( CoreSDK.ManusHost p_Host )
		{
			GoToState( State.Disconnected );
			ClearAllTemporarySkeletons();
			UnityMainThreadDispatcher.instance?.Enqueue( () => onDisconnectedFromCore.Invoke() );
		}

		/// <summary>
		/// This function gets called when the landscape data is received.
		/// </summary>
		/// <param name="p_Landscape">Landscape data from Manus Core</param>
		private void OnLandscape( CoreSDK.Landscape p_Landscape )
		{
			Landscape t_Landscape = new Landscape( p_Landscape.settings, p_Landscape.time );

			//Dongles
			for( int t_Index = 0; t_Index < p_Landscape.gloveDevices.dongleCount; t_Index++ )
			{
				t_Landscape.dongles.Add( p_Landscape.gloveDevices.dongles[t_Index] );
			}
			//Gloves
			for( int t_Index = 0; t_Index < p_Landscape.gloveDevices.gloveCount; t_Index++ )
			{
				t_Landscape.gloves.Add( p_Landscape.gloveDevices.gloves[t_Index] );
			}
			//Users
			for( int t_Index = 0; t_Index < p_Landscape.users.userCount; t_Index++ )
			{
				t_Landscape.users.Add( p_Landscape.users.users[t_Index] );
			}
			//Skeletons
			for( int t_Index = 0; t_Index < p_Landscape.skeletons.skeletonCount; t_Index++ )
			{
				t_Landscape.skeletons.Add( p_Landscape.skeletons.skeletons[t_Index] );
			}
			//Trackers
			for( int t_Index = 0; t_Index < p_Landscape.trackers.trackerCount; t_Index++ )
			{
				t_Landscape.trackers.Add( p_Landscape.trackers.trackers[t_Index] );
			}
			// Gestures
			var t_Res = CoreSDK.GetGestureLandscapeData( p_Landscape.gestureCount, out var t_GestureData );
			if( t_Res == CoreSDK.SDKReturnCode.Success )
			{
				for( int i = 0; i < t_GestureData.Length; i++ )
				{
					t_Landscape.gestures.Add( t_GestureData[i] );
				}
			}
			//Netdevices
			for( int t_Index = 0; t_Index < p_Landscape.netDevices.numberOfNetDevices; t_Index++ )
			{
				t_Landscape.netDevices.Add( p_Landscape.netDevices.netDevices[t_Index] );
			}

			landscape = t_Landscape;
			UnityMainThreadDispatcher.instance?.Enqueue( () => onLandscapeEvent.Invoke( t_Landscape ) );
		}

		/// <summary>
		/// This function gets called when skeleton data is received.
		/// </summary>
		/// <param name="p_SkeletonStream">Skeleton data from Manus Core</param>
		protected void OnSkeletonUpdate( CoreSDK.SkeletonStream p_SkeletonStream )
		{
			m_SkeletonDataQueueMutex.WaitOne();
			m_SkeletonData.Enqueue( p_SkeletonStream );
			m_SkeletonDataQueueMutex.ReleaseMutex();
			UnityMainThreadDispatcher.instance?.Enqueue( () => onSkeletonData.Invoke( p_SkeletonStream ) );
		}

		/// <summary>
		/// This function gets called when tracker data is received.
		/// </summary>
		/// <param name="p_TrackerStream">Tracker data from Manus Core</param>
		protected void OnTrackerUpdate( CoreSDK.TrackerStream p_TrackerStream )
		{
			m_TrackerData = p_TrackerStream;
			UnityMainThreadDispatcher.instance?.Enqueue( () => onTrackerData.Invoke( p_TrackerStream ) );
		}

		/// <summary>
		/// This function gets called when raw skeleton data is received.
		/// </summary>
		/// <param name="p_SkeletonStream">Skeleton data from Manus Core</param>
		protected void OnRawSkeletonUpdate( CoreSDK.RawSkeletonStream p_SkeletonStream )
		{
			m_RawSkeletonData = p_SkeletonStream;
			UnityMainThreadDispatcher.instance?.Enqueue( () => onRawSkeletonData.Invoke( p_SkeletonStream ) );
		}
		
		/// <summary>
		/// This function gets called when raw device data is received.
		/// </summary>
		/// <param name="p_RawDeviceData">Raw device data from Manus Core</param>
		protected void OnRawDeviceDataUpdate( CoreSDK.RawData p_RawDeviceData )
		{
			UnityMainThreadDispatcher.instance?.Enqueue( () => onRawDeviceData.Invoke( p_RawDeviceData ) );
		}

		/// <summary>
		/// This function gets called when gesture data is received.
		/// </summary>
		/// <param name="p_GestureStream">Gesture data from Manus Core</param>
		protected void OnGestureUpdate( CoreSDK.GestureStream p_GestureStream )
		{
			UnityMainThreadDispatcher.instance?.Enqueue( () => onGestureData.Invoke( p_GestureStream ) );
		}

		/// <summary>
		/// This function gets called when Ergonomics data is received.
		/// </summary>
		/// <param name="p_Ergonomics">Ergonomics data received</param>
		protected void OnErgonomicsUpdate( CoreSDK.ErgonomicsStream p_Ergonomics )
		{
			ErgonomicsStream t_ErgonomicsStream = new ErgonomicsStream( p_Ergonomics.publishTime );
			for( int t_Index = 0; t_Index < p_Ergonomics.dataCount; t_Index++ )
			{
				t_ErgonomicsStream.data.Add( p_Ergonomics.data[t_Index] );
			}

			ergonomicsData = t_ErgonomicsStream;
			UnityMainThreadDispatcher.instance?.Enqueue( () => onErgonomicsData.Invoke( t_ErgonomicsStream ) );
		}

		/// <summary>
		/// This function is being called when a temporary skeleton has been changed by the tool
		/// </summary>
		/// <param name="p_SystemMessage"></param>
		public void OnSystemUpdate( CoreSDK.SystemMessage p_SystemMessage )
		{
			if( p_SystemMessage.type == CoreSDK.SystemMessageType.TemporarySkeletonModified )
			{
				m_LoadableSkeletons.Add( p_SystemMessage.infoUInt );
			}
			UnityMainThreadDispatcher.instance?.Enqueue( () => onSystemMessageEvent.Invoke( p_SystemMessage ) );
		}

		/// <summary>
		/// Checks if the temporary skeleton has been updated in the tool
		/// </summary>
		/// <param name="p_TempIdx"></param>
		/// <returns></returns>
		public bool HasLoadableSkeleton( uint p_TempIdx )
		{
			lock( m_LoadableSkeletons )
			{
				foreach( uint t_SkeletonIndx in m_LoadableSkeletons )
				{
					if( t_SkeletonIndx == p_TempIdx )
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Remove temporary skeleton after it's been loaded
		/// </summary>
		/// <param name="p_TempIdx"></param>
		public void RemoveLoadableSkeleton( uint p_TempIdx )
		{
			lock( m_LoadableSkeletons )
			{
				m_LoadableSkeletons.Remove( p_TempIdx );
			}
		}

		/// <summary>
		/// Check for any nonexistent skeletons and remove and unload them and apply data to existing ones.
		/// </summary>
		private void UpdateSkeletonData()
		{
			if( m_SkeletonData.Count == 0 ) return;

			m_SkeletonDataQueueMutex.WaitOne();
			var t_NewSkeletonDataQueue = m_SkeletonData;
			m_SkeletonData = new Queue<CoreSDK.SkeletonStream>();
			m_SkeletonDataQueueMutex.ReleaseMutex();

			CoreSDK.SkeletonStream t_LatestSkeletonData;
			while( t_NewSkeletonDataQueue.Count > 0 )
			{
				t_LatestSkeletonData = t_NewSkeletonDataQueue.Dequeue();
				if( t_LatestSkeletonData.skeletons == null ) continue;

				foreach( var t_SkeletonData in t_LatestSkeletonData.skeletons )
				{
					if( m_Skeletons.TryGetValue( t_SkeletonData.id, out var t_Skeleton ) )
					{
						if( t_Skeleton == null )
						{
							CoreSDK.SDKReturnCode t_Result = CoreSDK.UnloadSkeleton( t_SkeletonData.id );
							if( t_Result != CoreSDK.SDKReturnCode.Success )
							{
								Debug.LogError( $"MANUS-ERROR: {t_Result}, could not remove unused skeleton." );
								return;
							}
							m_Skeletons.Remove( t_SkeletonData.id );
						}
						else
						{
							t_Skeleton.ApplyData( t_SkeletonData, t_LatestSkeletonData.publishTime );
						}
					}
				}

			}
		}

		/// <summary>
		/// Update stored tracked objects.
		/// </summary>
		private void UpdateTrackedObjectsData()
		{
			if( m_TrackedObjects.Count > 0 )
			{
				foreach( TrackedObject t_TrackedObject in m_TrackedObjects )
				{
					if( t_TrackedObject.tracker == null ) return;
					CoreSDK.SDKReturnCode t_Result = CoreSDK.GetDataForTracker_UsingIdAndType( (uint)t_TrackedObject.userId,
						t_TrackedObject.type,
						out CoreSDK.TrackerData t_TrackerData );
					if( t_Result != CoreSDK.SDKReturnCode.Success ) continue;
					t_TrackedObject.ApplyTrackerData( t_TrackerData, t_Result == CoreSDK.SDKReturnCode.Success );
				}
			}
		}

		/// <summary>
		/// Fetch current session id from Manus Core.
		/// </summary>
		private void RetrieveSessionId()
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GetSessionId( out m_SessionId );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get session id." );
			}
			Debug.Log( $"Connected to core with Session id: {m_SessionId}" );
		}

		/// <summary>
		/// Change Manus Manager settings.
		/// </summary>
		/// <param name="p_AutoConnect">Whether to auto connect on host found</param>
		/// <param name="p_ConnectGRPC">Whether to connect via GRPC</param>
		/// <param name="p_ConnectLocallyOnly">Whether to search only local machine for Manus Core instances</param>
		public void ChangeSettings( bool p_AutoConnect, bool p_ConnectGRPC, bool p_ConnectLocallyOnly )
		{
			ManusManager.settings.autoConnect = p_AutoConnect;
			ManusManager.settings.connectGRPC = p_ConnectGRPC;
			ManusManager.settings.localOnly = p_ConnectLocallyOnly;
			ManusManager.settings.Save();
		}

		/// <summary>
		/// Fetch found hosts.
		/// </summary>
		/// <returns>Found hosts</returns>
		public List<CoreSDK.ManusHost> GetHosts()
		{
			return m_HostsFound;
		}

		/// <summary>
		/// Register custom tracker to keep track of and update.
		/// </summary>
		/// <param name="p_Tracker">Custom tracker to register</param>
		public bool RegisterCustomTracker( CustomTracker p_Tracker )
		{
			if( m_CustomTrackers.ContainsKey( p_Tracker.trackerID ) )
			{
				Debug.LogWarning( $"Tried to add a custom tracker with same ID ({p_Tracker.trackerID}), please change the ID." );
				return false;
			}
			else m_CustomTrackers.Add( p_Tracker.trackerID, p_Tracker );
			return true;
		}

		/// <summary>
		/// Unregister tracked object to stop updating.
		/// </summary>
		/// <param name="p_Tracker"></param>
		public void UnregisterCustomTracker( CustomTracker p_Tracker )
		{
			m_CustomTrackers.Remove( p_Tracker.trackerID );
		}

		/// <summary>
		/// Send custom trackers to Manus Core.
		/// </summary>
		protected void SendCustomTracker()
		{
			if( m_CustomTrackers.Count == 0 ) return;
			CoreSDK.SDKReturnCode t_Result = CoreSDK.SDKReturnCode.Success;

			//Create tracker array
			CoreSDK.TrackerData[] t_TrackerArray = new CoreSDK.TrackerData[m_CustomTrackers.Count];
			uint t_TrackerIndex = 0;
			foreach( var t_Tracker in m_CustomTrackers )
			{
				t_Result = CoreSDK.GetDataForTracker_UsingTrackerId( new CoreSDK.TrackerId( t_Tracker.Key ), out CoreSDK.TrackerData t_TrackerData );
				if( t_Result == CoreSDK.SDKReturnCode.Success )
				{
					m_CustomTrackers[t_Tracker.Key].type = t_TrackerData.trackerType;
					m_CustomTrackers[t_Tracker.Key].userId = t_TrackerData.userId;
				}
				else if( t_Result != CoreSDK.SDKReturnCode.DataNotAvailable )
				{
					Debug.LogError( $"MANUS-ERROR: {t_Result}, could not fetch tracker data from core to update." );
				}
				t_TrackerArray[t_TrackerIndex] = new CoreSDK.TrackerData( t_TrackerData.lastUpdateTime,
					new CoreSDK.TrackerId( t_Tracker.Value.trackerID ),
					t_Tracker.Value.userId,
					false,
					t_Tracker.Value.type,
					t_Tracker.Value.rotation,
					t_Tracker.Value.position,
					CoreSDK.TrackingQuality.Trackable );
				t_TrackerIndex++;
			}
			t_Result = CoreSDK.SendDataForTrackers( ref t_TrackerArray, t_TrackerIndex );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not send custom trackers to core." );
			}
		}


		/// <summary>
		/// Register tracked object to keep track of and update.
		/// </summary>
		/// <param name="p_TrackedObject">Tracked object to register</param>
		public void RegisterTrackedObject( TrackedObject p_TrackedObject )
		{
			if( p_TrackedObject.userId == 0 )
			{
				uint t_AvailableUserId = FirstAvailableUser();
				if( t_AvailableUserId == 0 )
				{
					m_TrackedObjectsQueueMutex.WaitOne();
					m_TrackedObjectsQueue.Add( p_TrackedObject );
					m_TrackedObjectsQueueMutex.ReleaseMutex();
					return;
				}
				p_TrackedObject.userId = t_AvailableUserId;
			}
			m_TrackedObjects.Add( p_TrackedObject );
		}

		/// <summary>
		/// Unregister tracked object to stop updating.
		/// </summary>
		/// <param name="p_TrackedObject">Tracked object to remove</param>
		public void UnregisterTrackedObject( TrackedObject p_TrackedObject )
		{
			m_TrackedObjects.Remove( p_TrackedObject );
		}

		public static bool SendDataForTrackers( List<CoreSDK.TrackerData> p_TrackerData )
		{
			CoreSDK.TrackerData[] t_TrackerData = p_TrackerData.ToArray();
			CoreSDK.SDKReturnCode t_Result = CoreSDK.SendDataForTrackers( ref t_TrackerData, (uint)t_TrackerData.Length );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not send tracker data for tracker(s)." );
				return false;
			}
			return true;
		}

		/// <summary>
		/// Set tracker offset for a specific user
		/// </summary>
		/// <param name="p_UserId">ID of the user</param>
		/// <param name="p_TrackerOffset">Tracker offset</param>
		public static bool SetTrackerOffset( uint p_UserId, CoreSDK.TrackerOffset p_TrackerOffset )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.SetTrackerOffset( p_UserId, ref p_TrackerOffset );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not set tracker offset." );
				return false;
			}
			return true;
		}

		// --

		public static async Task<bool> GloveCalibrationStartAsync( CoreSDK.GloveCalibrationArgs p_Args )
		{
			return await Task.Run( () => GloveCalibrationStart( p_Args ) );
		}
		public static bool GloveCalibrationStart( CoreSDK.GloveCalibrationArgs p_Args )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GloveCalibrationStart( p_Args, out bool t_Complete );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not start glove calibration." );
			}
			return t_Complete;
		}

		// -- 

		public static async Task<bool> GloveCalibrationStopAsync( CoreSDK.GloveCalibrationArgs p_Args )
		{
			return await Task.Run( () => GloveCalibrationStop( p_Args ) );
		}
		public static bool GloveCalibrationStop( CoreSDK.GloveCalibrationArgs p_Args )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GloveCalibrationStop( p_Args, out bool t_Complete );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not stop glove calibration." );
			}
			return t_Complete;
		}

		// -- 

		public static async Task<bool> GloveCalibrationFinishAsync( CoreSDK.GloveCalibrationArgs p_Args )
		{
			return await Task.Run( () => GloveCalibrationFinish( p_Args ) );
		}
		public static bool GloveCalibrationFinish( CoreSDK.GloveCalibrationArgs p_Args )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GloveCalibrationFinish( p_Args, out bool t_Complete );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not finish glove calibration." );
			}
			return t_Complete;
		}

		// -- 

		public static async Task<uint> GloveCalibrationGetNumberOfStepsAsync( CoreSDK.GloveCalibrationArgs p_Args )
		{
			return await Task.Run( () => GloveCalibrationGetNumberOfSteps( p_Args ) );
		}
		public static uint GloveCalibrationGetNumberOfSteps( CoreSDK.GloveCalibrationArgs p_Args )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GloveCalibrationGetNumberOfSteps( p_Args, out uint t_NumberOfSteps );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get number of steps in glove calibration." );
			}
			return t_NumberOfSteps;
		}

		// -- 

		public static async Task<CoreSDK.GloveCalibrationStepData> GloveCalibrationGetStepDataAsync( CoreSDK.GloveCalibrationStepArgs p_Args )
		{
			return await Task.Run( () => GloveCalibrationGetStepData( p_Args ) );
		}
		public static CoreSDK.GloveCalibrationStepData GloveCalibrationGetStepData( CoreSDK.GloveCalibrationStepArgs p_Args )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GloveCalibrationGetStepData( p_Args, out CoreSDK.GloveCalibrationStepData t_StepData );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get glove calibration step data." );
			}
			return t_StepData;
		}

		// -- 

		public static async Task<bool> GloveCalibrationStartStepAsync( CoreSDK.GloveCalibrationStepArgs p_Args )
		{
			return await Task.Run( () => GloveCalibrationStartStep( p_Args ) );
		}
		public static bool GloveCalibrationStartStep( CoreSDK.GloveCalibrationStepArgs p_Args )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GloveCalibrationStartStep( p_Args, out bool t_Complete );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not start glove calibration step." );
			}
			return t_Complete;
		}

		#region Users
		public static async Task<uint> AddUserAsync( string p_Name )
		{
			return await Task.Run( () => AddUser( p_Name ) );
		}

		public static uint AddUser( string p_Name )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.AddUser( p_Name, out uint t_User );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could add user." );
			}
			return t_User;
		}

		public static async Task<CoreSDK.SDKReturnCode> RemoveUserAsync( uint p_UserId )
		{
			return await Task.Run( () => RemoveUser( p_UserId ) );
		}
		public static CoreSDK.SDKReturnCode RemoveUser( uint p_UserId )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.RemoveUser( p_UserId );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not remove user {p_UserId}." );
			}
			return t_Result;
		}

		public static async Task<CoreSDK.SDKReturnCode> SetUserNameAsync( uint p_UserID, string p_Name )
		{
			return await Task.Run( () => SetUserName( p_UserID, p_Name ) );
		}

		public static CoreSDK.SDKReturnCode SetUserName( uint p_UserID, string p_Name )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.SetUserName( p_UserID, p_Name );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not set name {p_Name} of user {p_UserID}." );
			}
			return t_Result;
		}

		public static async Task<CoreSDK.SDKReturnCode> AssignDongleToUserAsync( uint p_UserID, uint p_DongleId )
		{
			return await Task.Run( () => AssignDongleToUser( p_UserID, p_DongleId ) );
		}
		public static CoreSDK.SDKReturnCode AssignDongleToUser( uint p_UserID, uint p_DongleId )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.AssignDongleToUser(p_UserID, p_DongleId);
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not assign dongle {p_DongleId} to user {p_UserID}." );
			}
			return t_Result;
		}

		public static async Task<CoreSDK.SDKReturnCode> AssignGloveToUserAsync( uint p_UserID, CoreSDK.Side p_HandOrientation, uint p_GloveId )
		{
			return await Task.Run( () => AssignGloveToUser( p_UserID, p_HandOrientation, p_GloveId ) );
		}
		public static CoreSDK.SDKReturnCode AssignGloveToUser( uint p_UserID, CoreSDK.Side p_HandOrientation, uint p_GloveId )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.AssignGloveToUser( p_UserID, p_GloveId, p_HandOrientation);
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not assign glove {p_GloveId} to user {p_UserID}." );
			}
			return t_Result;
		}
		public static async Task<bool> MoveUserUpAsync( uint p_UserID )
		{
			return await Task.Run( () => MoveUserUp( p_UserID ) );
		}
		public static bool MoveUserUp( uint p_UserID )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.MoveUserUp( p_UserID );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not move user {p_UserID} up." );
				return true;
			}
			return false;
		}

		public static async Task<bool> MoveUserDownAsync( uint p_UserId )
		{
			return await Task.Run( () => MoveUserDown( p_UserId ) );
		}
		public static bool MoveUserDown( uint p_UserId )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.MoveUserDown( p_UserId );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not move user {p_UserId} down." );
				return true;
			}
			return false;
		}
		#endregion

		#region Connection handling async

		/// <summary>
		/// Look for Manus Core hosts asynchronously.
		/// </summary>
		/// <param name="p_WaitSeconds">Amount of time to search for in seconds</param>
		/// <param name="p_LoopbackOnly">Whether to check for only local Manus Core instances</param>
		/// <returns>Whether action s</returns>
		public async Task<bool> LookForHostsAsync( uint p_WaitSeconds = s_HostFetchWaitDelay, bool p_LoopbackOnly = false )
		{
			return await Task.Run( () => LookForHosts( p_WaitSeconds, p_LoopbackOnly ) );
		}

		/// <summary>
		/// Look for Manus Core hosts on the network.
		/// </summary>
		/// <param name="p_WaitSeconds">Amount of time to search for in seconds</param>
		/// <param name="p_LocalHostOnly">Whether to check for only local Manus Core instances</param>
		/// <returns>Whether action failed or not</returns>
		public bool LookForHosts( uint p_WaitSeconds = s_HostFetchWaitDelay, bool p_LocalHostOnly = false )
		{
			if( m_SearchingForHosts ) return false;
			m_SearchingForHosts = true;
			CoreSDK.SDKReturnCode t_Result = CoreSDK.LookForHosts( p_WaitSeconds, p_LocalHostOnly );
			m_SearchingForHosts = false;
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not look for hosts." );
				return false;
			}
			return true;
		}

		/// <summary>
		/// Get number of available hosts found from previous search asynchronously.
		/// </summary>
		/// <returns>Number of available hosts found</returns>
		public async Task<uint> GetNumberOfAvailableHostsFoundAsync()
		{
			return await Task.Run( () => GetNumberOfAvailableHostsFound() );
		}

		/// <summary>
		/// Get number of available hosts found from previous search.
		/// </summary>
		/// <returns>Number of available hosts found</returns>
		public uint GetNumberOfAvailableHostsFound()
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GetNumberOfAvailableHostsFound( out uint p_NumberOfAvailableHostsFound );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get number of available hosts." );
			}
			return p_NumberOfAvailableHostsFound;
		}

		/// <summary>
		/// Get available hosts found from previous search asynchronously.
		/// </summary>
		/// <returns>List of hosts found</returns>
		public async Task<List<CoreSDK.ManusHost>> GetAvailableHostsFoundAsync()
		{
			return await Task.Run( () => GetAvailableHostsFound() );
		}

		/// <summary>
		/// Get available Manus Core hosts found.
		/// </summary>
		/// <returns>List of hosts found</returns>
		public List<CoreSDK.ManusHost> GetAvailableHostsFound()
		{
			uint p_NumberOfAvailableHostsFound = GetNumberOfAvailableHostsFound();
			if( p_NumberOfAvailableHostsFound == 0 )
			{
				return new List<CoreSDK.ManusHost>();
			}

			CoreSDK.SDKReturnCode t_Result = CoreSDK.GetAvailableHostsFound( out CoreSDK.ManusHost[] p_Hosts, p_NumberOfAvailableHostsFound );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get available hosts." );
			}
			return p_Hosts.ToList();
		}

		/// <summary>
		/// Get whether Manus SDK is connected to Manus Core asynchronously.
		/// </summary>
		/// <returns>Connected to core</returns>
		public async Task<bool> GetIsConnectedToCoreAsync()
		{
			return await Task.Run( () => GetIsConnectedToCore() );
		}

		/// <summary>
		/// Get whether Manus SDK is connected to Manus Core.
		/// </summary>
		/// <returns>Connected to core</returns>
		public bool GetIsConnectedToCore()
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.GetIsConnectedToCore( out bool p_ConnectedToCore );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not get is connected to core value." );
			}
			return p_ConnectedToCore;
		}

		/// <summary>
		/// Connect to Manus Core via GRPC port asynchronously.
		/// </summary>
		/// <returns>Whether action succeeded</returns>
		public async Task<bool> ConnectGRPCAsync()
		{
			return await Task.Run( () => ConnectGRPC() );
		}

		/// <summary>
		/// Connect to Manus Core via GRPC port.
		/// </summary>
		/// <returns>Whether action succeeded</returns>
		public bool ConnectGRPC()
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.ConnectGRPC();
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not connect to GRPC port." );
				return false;
			}
			return true;
		}

		/// <summary>
		/// Connect to Manus Core host asynchronously.
		/// </summary>
		/// <param name="p_Host">Manus Core host to connect to</param>
		/// <returns>Whether action succeeded</returns>
		public async Task<bool> ConnectToHostAsync( CoreSDK.ManusHost p_Host )
		{
			return await Task.Run( () => ConnectToHost( p_Host ) );
		}

		/// <summary>
		/// Connect to Manus Core host.
		/// </summary>
		/// <param name="p_Host">Manus Core host to connect to</param>
		/// <returns>Whether action succeeded</returns>
		public bool ConnectToHost( CoreSDK.ManusHost p_Host )
		{
			CoreSDK.SDKReturnCode t_Result = CoreSDK.ConnectToHost( p_Host );
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				Debug.LogError( $"MANUS-ERROR: {t_Result}, could not connect to host {p_Host.hostName} at {p_Host.ipAddress}." );
				return false;
			}

			return true;
		}

		#endregion
	}
}
