using System.Collections.Generic;

using Manus;
using UnityEngine;

namespace UltraPose
{
	/// <summary>
	/// Subscribes to every Manus Core data + connection stream on
	/// <see cref="ManusManager.communicationHub"/>, converts each payload to JSON via
	/// <see cref="JsonUtility"/> (original field names preserved), wraps it in a
	/// { "stream": "...", "payload": {...} } envelope, and routes it.
	///
	/// Logging and publishing are independent, controlled per stream via <see cref="StreamOutput"/>:
	/// - log     -> Debug.Log to the Console (see <see cref="Log"/>)
	/// - publish -> send to the external process / transport (see <see cref="Publish"/>)
	///
	/// All handlers run on the Unity main thread (JsonUtility is main-thread only — fine). Enums
	/// serialize as integers; mirror the enum tables in ManusSDKTypes.cs on the Python side.
	/// </summary>
	public class ManusDataStreamReceiver : MonoBehaviour
	{
		/// <summary>Per-stream output control: log to Console and/or publish to transport, independently.</summary>
		[System.Serializable]
		public struct StreamOutput
		{
			public bool log;
			public bool publish;
			public bool Any => log || publish;
		}

		[Header( "Data streams (log / publish)" )]
		[SerializeField] private StreamOutput m_Skeleton;
		[SerializeField] private StreamOutput m_Ergonomics;
		[SerializeField] private StreamOutput m_RawSkeleton;
		[SerializeField] private StreamOutput m_RawDevice;
		[SerializeField] private StreamOutput m_Gesture;
		[SerializeField] private StreamOutput m_Tracker;
		[SerializeField] private StreamOutput m_Landscape;
		[SerializeField] private StreamOutput m_SystemMessage;

		[Header( "Connection (log / publish)" )]
		[SerializeField] private StreamOutput m_Connected;
		[SerializeField] private StreamOutput m_Disconnected;
		[SerializeField] private StreamOutput m_HostsFound;

		/// <summary>
		/// Subscribe to all streams. The communication hub is runtime-only and survives scene loads,
		/// so we (un)subscribe per enable/disable.
		/// </summary>
		private void OnEnable()
		{
			var t_Hub = ManusManager.communicationHub;
			if( t_Hub == null ) return;

			t_Hub.onSkeletonData.AddListener( OnSkeletonData );
			t_Hub.onErgonomicsData.AddListener( OnErgonomicsData );
			t_Hub.onRawSkeletonData.AddListener( OnRawSkeletonData );
			t_Hub.onRawDeviceData.AddListener( OnRawDeviceData );
			t_Hub.onGestureData.AddListener( OnGestureData );
			t_Hub.onTrackerData.AddListener( OnTrackerData );
			t_Hub.onLandscapeEvent.AddListener( OnLandscape );
			t_Hub.onSystemMessageEvent.AddListener( OnSystemMessage );

			t_Hub.onConnectedToCore.AddListener( OnConnectedToCore );
			t_Hub.onDisconnectedFromCore.AddListener( OnDisconnectedFromCore );
			t_Hub.onHostsFound.AddListener( OnHostsFound );
		}

		private void OnDisable()
		{
			var t_Hub = ManusManager.communicationHub;
			if( t_Hub == null ) return;

			t_Hub.onSkeletonData.RemoveListener( OnSkeletonData );
			t_Hub.onErgonomicsData.RemoveListener( OnErgonomicsData );
			t_Hub.onRawSkeletonData.RemoveListener( OnRawSkeletonData );
			t_Hub.onRawDeviceData.RemoveListener( OnRawDeviceData );
			t_Hub.onGestureData.RemoveListener( OnGestureData );
			t_Hub.onTrackerData.RemoveListener( OnTrackerData );
			t_Hub.onLandscapeEvent.RemoveListener( OnLandscape );
			t_Hub.onSystemMessageEvent.RemoveListener( OnSystemMessage );

			t_Hub.onConnectedToCore.RemoveListener( OnConnectedToCore );
			t_Hub.onDisconnectedFromCore.RemoveListener( OnDisconnectedFromCore );
			t_Hub.onHostsFound.RemoveListener( OnHostsFound );
		}

		// ---- Emission: log and publish are independent ----

		/// <summary>Serialize + route a stream payload. Builds JSON once, and only if log or publish is on.</summary>
		private void Emit<T>( StreamOutput p_Out, string p_Stream, T p_Payload )
		{
			if( !p_Out.Any ) return;
			Emit( p_Out, p_Stream, JsonUtility.ToJson( p_Payload ) );
		}

		/// <summary>Route an already-serialized payload (used by connection events that build JSON manually).</summary>
		private void Emit( StreamOutput p_Out, string p_Stream, string p_PayloadJson )
		{
			string t_Message = $"{{\"stream\":\"{p_Stream}\",\"payload\":{p_PayloadJson}}}";
			if( p_Out.log ) Log( t_Message );
			if( p_Out.publish ) Publish( t_Message );
		}

		/// <summary>
		/// Debug-log a stream message to the Console, prefixed with "Unix Time:" and the Unix time
		/// in milliseconds (since 1970-01-01 UTC) at which Unity logged it. Independent of publishing.
		/// </summary>
		private void Log( string p_Message )
		{
			long t_UnixTimeMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			Debug.Log( $"Unix Time: {t_UnixTimeMs}, {p_Message}" );
		}

		/// <summary>Send a stream message to the external process (e.g. Python). Transport only — no logging.</summary>
		private void Publish( string p_Message )
		{
			// TODO transport: send p_Message to the external process here (socket / pipe / etc.).
		}

		// ---- Data stream handlers ----
		private void OnSkeletonData( CoreSDK.SkeletonStream p_Stream ) => Emit( m_Skeleton, "skeleton", p_Stream );
		private void OnErgonomicsData( CommunicationHub.ErgonomicsStream p_Stream ) => Emit( m_Ergonomics, "ergonomics", p_Stream );
		private void OnRawSkeletonData( CoreSDK.RawSkeletonStream p_Stream ) => Emit( m_RawSkeleton, "rawSkeleton", p_Stream );
		private void OnRawDeviceData( CoreSDK.RawData p_Data ) => Emit( m_RawDevice, "rawDevice", p_Data );
		private void OnGestureData( CoreSDK.GestureStream p_Stream ) => Emit( m_Gesture, "gesture", p_Stream );
		private void OnTrackerData( CoreSDK.TrackerStream p_Stream ) => Emit( m_Tracker, "tracker", p_Stream );
		private void OnLandscape( CommunicationHub.Landscape p_Landscape ) => Emit( m_Landscape, "landscape", p_Landscape );
		private void OnSystemMessage( CoreSDK.SystemMessage p_Message ) => Emit( m_SystemMessage, "system", p_Message );

		// ---- Connection handlers (no parameter — payloads constructed) ----
		private void OnConnectedToCore()
		{
			if( !m_Connected.Any ) return;
			var t_Host = ManusManager.communicationHub?.currentHost;
			Emit( m_Connected, "connected", t_Host.HasValue ? JsonUtility.ToJson( t_Host.Value ) : "{}" );
		}

		private void OnDisconnectedFromCore()
		{
			if( !m_Disconnected.Any ) return;
			Emit( m_Disconnected, "disconnected", "{}" );
		}

		private void OnHostsFound()
		{
			if( !m_HostsFound.Any ) return;
			var t_Hub = ManusManager.communicationHub;
			if( t_Hub == null ) return;
			Emit( m_HostsFound, "hostsFound", JsonUtility.ToJson( new HostsMessage { hosts = t_Hub.GetHosts() } ) );
		}

		/// <summary>Wrapper so the discovered-hosts list serializes — JsonUtility can't root a List.</summary>
		[System.Serializable]
		private struct HostsMessage
		{
			public List<CoreSDK.ManusHost> hosts;
		}
	}
}
