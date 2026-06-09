using System.Linq;

using Manus;
using UnityEngine;

namespace UltraPose
{
	/// <summary>
	/// Central subscriber for every Manus Core data + connection stream exposed by
	/// <see cref="ManusManager.communicationHub"/>.
	///
	/// Each stream has an Inspector debug toggle; when enabled, the matching handler
	/// prints a concise summary of that stream to the Console. All handlers run on the
	/// Unity main thread, so logging here is safe.
	///
	/// NOTE: skeleton / ergonomics / raw / tracker streams fire at high frequency
	/// (~60-120 Hz). Leaving those toggles on floods the Console and slows the Editor —
	/// enable only while inspecting a specific stream.
	/// </summary>
	public class ManusDataStreamReceiver : MonoBehaviour
	{
		[Header( "Debug log — data streams" )]
		[Tooltip( "Posed skeleton(s) registered with Core." )]
		[SerializeField] private bool m_LogSkeleton = false;
		[Tooltip( "Finger joint angles (20 per hand)." )]
		[SerializeField] private bool m_LogErgonomics = false;
		[Tooltip( "Per-glove hand skeleton from Core's estimation engine." )]
		[SerializeField] private bool m_LogRawSkeleton = false;
		[Tooltip( "Raw per-sensor transforms + glove quaternion." )]
		[SerializeField] private bool m_LogRawDevice = false;
		[Tooltip( "Recognized gesture probabilities." )]
		[SerializeField] private bool m_LogGesture = false;
		[Tooltip( "External tracker positions/rotations." )]
		[SerializeField] private bool m_LogTracker = false;
		[Tooltip( "Device/user/skeleton/tracker inventory." )]
		[SerializeField] private bool m_LogLandscape = false;
		[Tooltip( "System / diagnostic messages." )]
		[SerializeField] private bool m_LogSystemMessage = false;

		[Header( "Debug log — connection" )]
		[SerializeField] private bool m_LogConnected = false;
		[SerializeField] private bool m_LogDisconnected = false;
		[SerializeField] private bool m_LogHostsFound = false;

		/// <summary>
		/// Subscribe to all streams. The communication hub is runtime-only and survives
		/// scene loads, so we (un)subscribe per enable/disable.
		/// </summary>
		private void OnEnable()
		{
			var t_Hub = ManusManager.communicationHub;
			if( t_Hub == null ) return; // null while compiling / shutting down

			// Data streams
			t_Hub.onSkeletonData.AddListener( OnSkeletonData );
			t_Hub.onErgonomicsData.AddListener( OnErgonomicsData );
			t_Hub.onRawSkeletonData.AddListener( OnRawSkeletonData );
			t_Hub.onRawDeviceData.AddListener( OnRawDeviceData );
			t_Hub.onGestureData.AddListener( OnGestureData );
			t_Hub.onTrackerData.AddListener( OnTrackerData );
			t_Hub.onLandscapeEvent.AddListener( OnLandscape );
			t_Hub.onSystemMessageEvent.AddListener( OnSystemMessage );

			// Connection lifecycle
			t_Hub.onConnectedToCore.AddListener( OnConnectedToCore );
			t_Hub.onDisconnectedFromCore.AddListener( OnDisconnectedFromCore );
			t_Hub.onHostsFound.AddListener( OnHostsFound );
		}

		/// <summary>
		/// Unsubscribe from all streams. Required because the hub outlives this component —
		/// leaving listeners attached triggers "listener on destroyed object" errors.
		/// </summary>
		private void OnDisable()
		{
			var t_Hub = ManusManager.communicationHub;
			if( t_Hub == null ) return;

			// Data streams
			t_Hub.onSkeletonData.RemoveListener( OnSkeletonData );
			t_Hub.onErgonomicsData.RemoveListener( OnErgonomicsData );
			t_Hub.onRawSkeletonData.RemoveListener( OnRawSkeletonData );
			t_Hub.onRawDeviceData.RemoveListener( OnRawDeviceData );
			t_Hub.onGestureData.RemoveListener( OnGestureData );
			t_Hub.onTrackerData.RemoveListener( OnTrackerData );
			t_Hub.onLandscapeEvent.RemoveListener( OnLandscape );
			t_Hub.onSystemMessageEvent.RemoveListener( OnSystemMessage );

			// Connection lifecycle
			t_Hub.onConnectedToCore.RemoveListener( OnConnectedToCore );
			t_Hub.onDisconnectedFromCore.RemoveListener( OnDisconnectedFromCore );
			t_Hub.onHostsFound.RemoveListener( OnHostsFound );
		}

		// ---- Data stream handlers (invoked on the Unity main thread) ----

		/// <summary>Posed skeleton(s) registered with Manus Core (node transforms).</summary>
		private void OnSkeletonData( CoreSDK.SkeletonStream p_Stream )
		{
			if( !m_LogSkeleton ) return;
			string t_Detail = p_Stream.skeletons == null ? "" :
				string.Join( ", ", p_Stream.skeletons.Select( s => $"id={s.id} nodes={s.nodes?.Length ?? 0}" ) );
			Debug.Log( $"[Manus] Skeleton t={p_Stream.publishTime.time} count={p_Stream.skeletons?.Count ?? 0} [{t_Detail}]" );
		}

		/// <summary>Finger joint angles, 20 per hand; index by CoreSDK.ErgonomicsDataType.</summary>
		private void OnErgonomicsData( CommunicationHub.ErgonomicsStream p_Stream )
		{
			if( !m_LogErgonomics ) return;
			if( p_Stream.data == null ) { Debug.Log( "[Manus] Ergonomics: <none>" ); return; }
			// data[] order follows CoreSDK.ErgonomicsDataType (Left 0-19, Right 20-39): per finger = MCP spread, MCP stretch, PIP stretch, DIP stretch
			foreach( var t_D in p_Stream.data )
			{
				string t_Values = t_D.data == null ? "" : string.Join( ", ", t_D.data.Select( v => v.ToString( "F1" ) ) );
				Debug.Log( $"[Manus] Ergonomics id={t_D.id} isUserID={t_D.isUserID} values=[{t_Values}]" );
			}
		}

		/// <summary>Per-glove hand skeleton straight from Core's estimation engine (keyed by gloveId).</summary>
		private void OnRawSkeletonData( CoreSDK.RawSkeletonStream p_Stream )
		{
			if( !m_LogRawSkeleton ) return;
			string t_Detail = p_Stream.skeletons == null ? "" :
				string.Join( ", ", p_Stream.skeletons.Select( s => $"glove={s.gloveId} nodes={s.nodes?.Length ?? 0}" ) );
			Debug.Log( $"[Manus] RawSkeleton t={p_Stream.publishTime.time} gloves={p_Stream.skeletons?.Count ?? 0} [{t_Detail}]" );
		}

		/// <summary>Raw per-sensor transforms (up to 5) + glove case quaternion.</summary>
		private void OnRawDeviceData( CoreSDK.RawData p_Data )
		{
			if( !m_LogRawDevice ) return;
			string t_Detail = p_Data.rawDeviceData == null ? "" :
				string.Join( ", ", p_Data.rawDeviceData.Select( d => $"id={d.id} sensors={d.sensorCount}" ) );
			Debug.Log( $"[Manus] RawDevice t={p_Data.publishTime.time} devices={p_Data.rawDeviceData?.Count ?? 0} [{t_Detail}]" );
		}

		/// <summary>Recognized gesture probabilities per user/glove.</summary>
		private void OnGestureData( CoreSDK.GestureStream p_Stream )
		{
			if( !m_LogGesture ) return;
			// NOTE: ProcessInternalGestureStream only fills id/isUserID/gestureData; gestureCount/totalGestureCount stay 0 — use gestureData.Length.
			string t_Detail = p_Stream.gestureProbabilities == null ? "" :
				string.Join( ", ", p_Stream.gestureProbabilities.Select( g => $"id={g.id} probs={g.gestureData?.Length ?? 0}" ) );
			Debug.Log( $"[Manus] Gesture t={p_Stream.publishTime.time} entries={p_Stream.gestureProbabilities?.Count ?? 0} [{t_Detail}]" );
		}

		/// <summary>External tracker positions/rotations; trackerType is the body slot, quality is tracking state.</summary>
		private void OnTrackerData( CoreSDK.TrackerStream p_Stream )
		{
			if( !m_LogTracker ) return;
			string t_Detail = p_Stream.trackers == null ? "" :
				string.Join( ", ", p_Stream.trackers.Select( t => $"{t.trackerId.id}:{t.trackerType}/{t.quality}@({t.position.x:F2},{t.position.y:F2},{t.position.z:F2})" ) );
			Debug.Log( $"[Manus] Tracker t={p_Stream.publishTime.time} count={p_Stream.trackers?.Count ?? 0} [{t_Detail}]" );
		}

		/// <summary>Inventory + state: dongles, gloves, users, skeletons, trackers.</summary>
		private void OnLandscape( CommunicationHub.Landscape p_Landscape )
		{
			if( !m_LogLandscape ) return;
			Debug.Log( $"[Manus] Landscape dongles={p_Landscape.dongles.Count} gloves={p_Landscape.gloves.Count} " +
				$"users={p_Landscape.users.Count} skeletons={p_Landscape.skeletons.Count} trackers={p_Landscape.trackers.Count}" );
		}

		/// <summary>Diagnostics / system events from Manus Core.</summary>
		private void OnSystemMessage( CoreSDK.SystemMessage p_Message )
		{
			if( !m_LogSystemMessage ) return;
			Debug.Log( $"[Manus] System {p_Message.type}: \"{p_Message.infoString}\" ({p_Message.infoUInt})" );
		}

		// ---- Connection lifecycle handlers ----

		/// <summary>Connected to a Manus Core host.</summary>
		private void OnConnectedToCore()
		{
			if( !m_LogConnected ) return;
			Debug.Log( $"[Manus] Connected to Core (host: {ManusManager.communicationHub?.currentHost?.hostName})" );
		}

		/// <summary>Disconnected from the Manus Core host.</summary>
		private void OnDisconnectedFromCore()
		{
			if( !m_LogDisconnected ) return;
			Debug.Log( "[Manus] Disconnected from Core" );
		}

		/// <summary>The list of available hosts was updated.</summary>
		private void OnHostsFound()
		{
			if( !m_LogHostsFound ) return;
			Debug.Log( $"[Manus] Hosts found: {ManusManager.communicationHub?.GetHosts()?.Count ?? 0}" );
		}
	}
}
