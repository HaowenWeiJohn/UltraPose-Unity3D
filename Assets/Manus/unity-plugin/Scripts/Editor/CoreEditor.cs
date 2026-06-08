using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace Manus.Editor
{
	/// <summary>
	/// Custom in engine inspector window for interacting with communication hub.
	/// </summary>
	public class CoreEditor : EditorWindow
	{
		private bool m_AutoConnect;
		private bool m_ConnectGRPC;
		private bool m_ConnectLocallyOnly;
		private bool m_ShowAvailableHosts = false;
		private string m_ConnectedHost = "";

		CommunicationHub.State? m_State;

		List<CoreSDK.ManusHost> m_Hosts = new List<CoreSDK.ManusHost>();

		Vector2 m_HostScrollPos = Vector2.zero;

		// Add menu named "My Window" to the Window menu
		[MenuItem( "Manus/CommunicationHub" )]
		private static void Init()
		{
			// Get existing open window or if none, make a new one:
			CoreEditor window = (CoreEditor)GetWindow(typeof(CoreEditor), false, "Communication hub");
			window.Show();
		}

		/// <summary>
		/// Update current state of communication hub.
		/// </summary>
		private void Update()
		{
			if( m_State != ManusManager.communicationHub?.currentState )
			{
				m_State = ManusManager.communicationHub?.currentState;
				Repaint();
			}
		}

		/// <summary>
		/// Draw custom editor window.
		/// </summary>
		private void OnGUI()
		{
			m_AutoConnect = ManusManager.settings.autoConnect;
			m_ConnectGRPC = ManusManager.settings.connectGRPC;
			m_ConnectLocallyOnly = ManusManager.settings.localOnly;

			var t_Style = new GUIStyle();
			if( ManusManager.communicationHub?.currentState == CommunicationHub.State.Connected )
			{
				m_ConnectedHost = $"{ManusManager.communicationHub?.currentState.ToString()} To {ManusManager.communicationHub?.currentHost?.hostName}";
				t_Style.normal.textColor = new Color( 0.4f, 0.7f, 0.4f );
				t_Style.fontStyle = FontStyle.Bold;
			}
			else if( ManusManager.communicationHub?.currentState == CommunicationHub.State.Connecting )
			{
				m_ConnectedHost = $"{ManusManager.communicationHub?.currentState.ToString()} To {ManusManager.communicationHub?.currentHost?.hostName}";
				t_Style.normal.textColor = Color.yellow;
				t_Style.fontStyle = FontStyle.Bold;
			}
			else
			{
				m_ConnectedHost = $"{ManusManager.communicationHub?.currentState.ToString()}";
				t_Style.normal.textColor = new Color( 0.7f, 0.4f, 0.4f );
				t_Style.fontStyle = FontStyle.Bold;
			}
			if( !ManusManager.HasCommunicationHub() ) return;
			EditorGUI.BeginChangeCheck();
			//Status Indicator
			EditorGUILayout.TextArea( "Info", EditorStyles.boldLabel );
			EditorGUILayout.BeginHorizontal( "box" );

			EditorGUILayout.LabelField( "Status: ", m_ConnectedHost, t_Style );
			EditorGUILayout.EndHorizontal();

			//Settings
			EditorGUILayout.TextArea( "Settings", EditorStyles.boldLabel );
			EditorGUILayout.BeginHorizontal( "box" );
			EditorGUILayout.BeginVertical();
			//m_ConnectGRPC = EditorGUILayout.Toggle( "Use GRPC", m_ConnectGRPC );

			EditorGUILayout.EndVertical();
			m_ConnectLocallyOnly = EditorGUILayout.Toggle( "Connect locally only", m_ConnectLocallyOnly );

			m_AutoConnect = EditorGUILayout.Toggle( "Connect Automatically", m_AutoConnect );

			EditorGUILayout.EndHorizontal();


			switch( m_State )
			{
				case CommunicationHub.State.Disconnected:
					{
						EditorGUILayout.BeginHorizontal();

						//Refresh hosts
						if( GUILayout.Button( "Get Available Hosts" ) )
						{
							ManusManager.settings.autoConnect = false;

							m_ShowAvailableHosts = true;
							ManusManager.communicationHub?.SearchAndFetchHosts(); //causes a crash if unity is closed during
						}

						EditorGUILayout.EndHorizontal();

						//Show hosts
						m_Hosts = ManusManager.communicationHub?.GetHosts();
						if( m_Hosts.Count > 0 && m_ShowAvailableHosts )
						{
							m_HostScrollPos = EditorGUILayout.BeginScrollView( m_HostScrollPos );
							{
								EditorGUILayout.BeginVertical( "box" );

								foreach( CoreSDK.ManusHost t_Host in m_Hosts )
								{
									EditorGUILayout.BeginHorizontal( "box" );
									GUILayout.FlexibleSpace();

									EditorGUILayout.LabelField( "Host Name:", GUILayout.Width( 70 ) );
									GUILayout.FlexibleSpace();

									EditorGUILayout.TextArea( $"{t_Host.hostName}", GUILayout.Height( 20 ), GUILayout.Width( 140 ) );
									GUILayout.FlexibleSpace();

									EditorGUILayout.LabelField( "IP:", GUILayout.Width( 20 ) );
									GUILayout.FlexibleSpace();

									EditorGUILayout.TextArea( $"{t_Host.ipAddress}", GUILayout.Height( 20 ), GUILayout.Width( 120 ) );
									GUILayout.FlexibleSpace();

									if( GUILayout.Button( new GUIContent( "Connect" ), GUILayout.Height( 20 ), GUILayout.Width( 80 ) ) )
									{
										ManusManager.communicationHub?.Connect( t_Host );
										m_ShowAvailableHosts = false;
									}
									GUILayout.FlexibleSpace();

									EditorGUILayout.EndHorizontal();
								}
								EditorGUILayout.EndVertical();
							}

							EditorGUILayout.EndScrollView();
							GUILayout.FlexibleSpace();
						}
					}
					break;
				case CommunicationHub.State.Connected:
					{
						if( GUILayout.Button( "Disconnect" ) )
						{

							ManusManager.communicationHub?.Disconnect();
							m_AutoConnect = false;
						}
					}
					break;
			}

			EditorGUILayout.TextArea( "Skeleton Management", EditorStyles.boldLabel );
			EditorGUILayout.BeginHorizontal( "box" );
			if( GUILayout.Button( new GUIContent( "Clear All Temporary Skeletons", "This will invalidate any temporary skeletons you may have sent to Manus Core and/or the Dev Tools." ) ) )
			{
				ManusManager.communicationHub?.ClearAllTemporarySkeletons();
			}
			EditorGUILayout.EndHorizontal();

			//if( m_ConnectGRPC )
			//{
			//	EditorGUILayout.HelpBox( "GRPC port will need to be set up in core", MessageType.Info );
			//}
			if( EditorGUI.EndChangeCheck() )
			{
				ManusManager.communicationHub?.ChangeSettings( m_AutoConnect, m_ConnectGRPC, m_ConnectLocallyOnly );

			}
		}
	}
}
