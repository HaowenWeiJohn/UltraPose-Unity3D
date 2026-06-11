using System.Collections.Generic;

using Manus;
using UnityEngine;

namespace UltraPose
{
	/// <summary>
	/// Subscribes to every Manus Core data + connection stream on
	/// <see cref="ManusManager.communicationHub"/>, converts each payload to JSON via
	/// <see cref="JsonUtility"/> (original field names preserved), wraps it in a
	/// { "stream": "...", "unityEmitUnixTimeSec": ..., "unityRealtimeSinceStartupSec": ...,
	/// "payload": {...} } envelope, and routes it. Both times are captured when the envelope is
	/// built: unityEmitUnixTimeSec is wall clock (UTC seconds since 1970-01-01, millisecond
	/// precision in the fractional part, distinct from
	/// the Manus publishTime bitfield inside the payload); unityRealtimeSinceStartupSec is
	/// monotonic time since Unity started (editor launch in the Editor, app launch in builds —
	/// not since entering play mode).
	///
	/// Logging and publishing are independent, controlled per stream via <see cref="StreamOutput"/>:
	/// - log     -> Debug.Log to the Console (see <see cref="Log"/>)
	/// - publish -> ZMQ PUB socket (<see cref="ZmqPublisher"/>) as [stream name][envelope JSON]
	///   UTF-8 frames, so subscribers can filter per stream without parsing JSON
	///
	/// PUB/SUB is lossy: late subscribers miss earlier messages (slow joiner) and stalled ones
	/// drop frames past the high-water mark. Continuous streams self-heal next tick; one-shot
	/// connected/disconnected may be missed — landscape (~1 Hz) is the liveness signal.
	///
	/// All handlers run on the Unity main thread (CommunicationHub dispatches every event via
	/// UnityMainThreadDispatcher). Enums serialize as integers; mirror the enum tables in
	/// ManusSDKTypes.cs on the Python side.
	///
	/// JSON payload notes for consumers:
	/// - gesture: totalGestureCount / gestureCount are always 0 (the SDK never copies them into
	///   the published struct) — gestureData's length is the real count.
	/// - Fixed-size marshaled arrays serialize in full: rawDevice sensorData always has 5 entries
	///   (only sensorCount are valid), ergonomics data always 40 floats (index by
	///   ErgonomicsDataType), glove iMUCalibrationInfo always 6.
	/// - hostsFound re-fires every ~10 s while disconnected (host auto-find cycle), not one-shot.
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

		[Header( "ZeroMQ transport" )]
		[Tooltip( "PUB socket bind address; external subscribers connect here." )]
		[SerializeField] private string m_PublishAddress = "tcp://127.0.0.1:5560";
		[Tooltip( "Per-subscriber outbound queue limit in messages; frames drop beyond this if a consumer stalls." )]
		[SerializeField] private int m_SendHighWaterMark = 1000;

		private ZmqPublisher m_Publisher;

		/// <summary>
		/// The publisher spans the component's whole lifetime (Awake..OnDestroy) rather than
		/// enable/disable, so toggling the component doesn't rebind the TCP port.
		/// </summary>
		private void Awake()
		{
			m_Publisher = new ZmqPublisher( m_PublishAddress, m_SendHighWaterMark );
		}

		private void OnDestroy()
		{
			m_Publisher?.Dispose();
			m_Publisher = null;
		}

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
			// InvariantCulture: a comma-decimal locale would otherwise produce invalid JSON.
			string t_UnixTimeSec = ( System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 ).ToString( "F3", System.Globalization.CultureInfo.InvariantCulture );
			string t_RealtimeSec = Time.realtimeSinceStartupAsDouble.ToString( "F6", System.Globalization.CultureInfo.InvariantCulture );
			string t_Message = $"{{\"stream\":\"{p_Stream}\",\"unityEmitUnixTimeSec\":{t_UnixTimeSec},\"unityRealtimeSinceStartupSec\":{t_RealtimeSec},\"payload\":{p_PayloadJson}}}";
			if( p_Out.log ) Log( t_Message );
			if( p_Out.publish ) Publish( p_Stream, t_Message );
		}

		/// <summary>
		/// Debug-log a stream message to the Console as-is (the envelope already carries
		/// unityEmitUnixTimeSec). Independent of publishing.
		/// </summary>
		private void Log( string p_Message )
		{
			Debug.Log( p_Message );
		}

		/// <summary>Send a stream message to the external process (e.g. Python) over ZMQ as
		/// [topic = stream name][envelope JSON]. Transport only — no logging.</summary>
		private void Publish( string p_Stream, string p_Message )
		{
			m_Publisher?.Publish( p_Stream, p_Message );
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
