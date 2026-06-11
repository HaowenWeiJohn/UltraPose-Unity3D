using System;
using System.Collections.Generic;
using System.Text;

using NetMQ;
using NetMQ.Sockets;
using UnityEngine;

namespace UltraPose
{
	/// <summary>
	/// Owns a single NetMQ PUB socket bound on construction. Send-only and main-thread only
	/// (NetMQ sockets are not thread-safe). Messages go out as multipart [topic][message],
	/// both frames UTF-8 bytes, so subscribers can prefix-filter on the topic frame.
	///
	/// PUB semantics: sends never block — frames drop when no subscriber is connected or a
	/// subscriber exceeds its high-water mark, and late joiners miss earlier messages.
	///
	/// Dispose() must run before play mode exits or the Editor hangs on NetMQ's background
	/// threads; the constructor registers beforeAssemblyReload to cover mid-play recompiles,
	/// where OnDestroy never fires.
	/// </summary>
	public sealed class ZmqPublisher : IDisposable
	{
		/// <summary>Bound PUB socket; null means bind failed or disposed — Publish becomes a no-op.</summary>
		private PublisherSocket m_Socket;

		/// <summary>UTF-8 topic frames, cached per stream name (the topic set is small and fixed).</summary>
		private readonly Dictionary<string, byte[]> m_TopicBytes = new Dictionary<string, byte[]>();

		/// <summary>Bind a PUB socket on p_Address (e.g. "tcp://127.0.0.1:5560"). On bind failure
		/// (typically the port is already in use) the publisher stays a no-op instead of throwing.</summary>
		public ZmqPublisher( string p_Address, int p_SendHighWaterMark )
		{
			AsyncIO.ForceDotNet.Force(); // required before any socket creation under Unity/Mono

			var t_Socket = new PublisherSocket();
			t_Socket.Options.Linger = TimeSpan.Zero;
			t_Socket.Options.SendHighWatermark = p_SendHighWaterMark;
			try
			{
				t_Socket.Bind( p_Address );
				m_Socket = t_Socket;
				Debug.Log( $"[ZmqPublisher] Bound PUB socket at {p_Address}" );
			}
			catch( NetMQException t_E )
			{
				t_Socket.Dispose();
				NetMQConfig.Cleanup( false );
				Debug.LogError( $"[ZmqPublisher] Bind failed on {p_Address} (port in use?): {t_E.Message}" );
			}

#if UNITY_EDITOR
			UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Dispose;
#endif
		}

		/// <summary>Send a message as [topic][message] UTF-8 frames. No-op if the socket failed to bind.</summary>
		public void Publish( string p_Topic, string p_Message )
		{
			if( m_Socket == null ) return;

			if( !m_TopicBytes.TryGetValue( p_Topic, out byte[] t_Topic ) )
				m_TopicBytes[p_Topic] = t_Topic = Encoding.UTF8.GetBytes( p_Topic );

			// Explicit UTF-8 bytes — NetMQ's string overloads encode as ASCII and would mangle non-ASCII JSON.
			m_Socket.SendMoreFrame( t_Topic ).SendFrame( Encoding.UTF8.GetBytes( p_Message ) );
		}

		/// <summary>Close the socket, then tear down the NetMQ context. Idempotent.</summary>
		public void Dispose()
		{
#if UNITY_EDITOR
			UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
#endif
			if( m_Socket == null ) return;

			m_Socket.Dispose();
			m_Socket = null;
			// Cleanup is process-global and must follow socket disposal (reversed order blocks forever).
			// Fine while this is the project's only NetMQ user; revisit if a second socket appears.
			NetMQConfig.Cleanup( false );
		}
	}
}
