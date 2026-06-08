using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace Manus
{
	/// <summary>
	/// Custom in scene browser for Manus Core instances.
	/// </summary>
	public class CoreBrowser : MonoBehaviour
	{
		[Tooltip("The generic version of the button that is spawned per instance of core found.")]
		public GameObject genericCoreButton;
		[Tooltip("The panel where available hosts and connection settings are set.")]
		public GameObject browserPanel;
		[Tooltip("The rect used for creating the core buttons in.")]
		public RectTransform coreListRect;
		[Tooltip("Find local hosts only.")]
		public Toggle localHostOnly;
		[Tooltip("Connect to first host found.")]
		public Toggle autoConnect;

		private List<Button> m_LobbyButtons = new List<Button>();
		private List<CoreSDK.ManusHost> m_HostList;
		private CoreSDK.ManusHost m_SelectedHost;
		private Button m_ConnectButton;

		private string m_SelectedHostFristClick;
		private int m_ClickCount;

		/// <summary>
		/// Check if communication hub is disconnected and enable self.
		/// </summary>
		private void Awake()
		{
			m_ConnectButton = GetComponentInChildren<Button>();
			m_HostList = ManusManager.communicationHub?.GetHosts();
			ManusManager.communicationHub?.onHostsFound.AddListener( UpdateBrowser );
			localHostOnly.isOn = ManusManager.settings.localOnly;
			ManusManager.communicationHub?.SearchAndFetchHosts();

			localHostOnly.onValueChanged.AddListener( delegate
			{
				LocalHostToggleValueChanged( localHostOnly );
			} );

			autoConnect.onValueChanged.AddListener( delegate
			{
				AutoConnectToggleValueChanged( autoConnect );
			} );

			m_ConnectButton.onClick.AddListener( delegate
			{
				ActivatePanel();
			} );

			if( ManusManager.communicationHub?.currentState != CommunicationHub.State.Connected )
			{
				m_ConnectButton.gameObject.SetActive( true );
			}
		}

		/// <summary>
		/// The update function checks if the lobby should be updated.
		/// </summary>
		private void Update()
		{
			switch( ManusManager.communicationHub?.currentState )
			{
				case CommunicationHub.State.Disconnected:
					m_ConnectButton.gameObject.SetActive( true );
					m_HostList = ManusManager.communicationHub?.GetHosts();

					break;
				case CommunicationHub.State.Connected:
					m_ConnectButton.gameObject.SetActive( false );
					browserPanel.gameObject.SetActive( false );

					break;
			}

			if( localHostOnly.isOn != ManusManager.settings.localOnly )
			{
				localHostOnly.isOn = ManusManager.settings.localOnly;
				ManusManager.settings.Save();
			}
			if( autoConnect.isOn != ManusManager.settings.autoConnect )
			{
				autoConnect.isOn = ManusManager.settings.autoConnect;
				ManusManager.settings.Save();
			}
		}

		void LocalHostToggleValueChanged( Toggle p_Toggle )
		{
			ManusManager.settings.localOnly = p_Toggle.isOn;
			ManusManager.settings.Save();

			ManusManager.communicationHub?.SearchAndFetchHosts();
		}

		void AutoConnectToggleValueChanged( Toggle p_Toggle )
		{
			ManusManager.settings.autoConnect = p_Toggle.isOn;
			ManusManager.settings.Save();

			ManusManager.communicationHub?.AutoConnect();
		}

		void ActivatePanel()
		{
			browserPanel.gameObject.SetActive( !browserPanel.gameObject.activeSelf );
		}

		/// <summary>
		/// Button with on and off function.
		/// </summary>
		void TaskOnClick( string p_HostName )
		{
			m_ClickCount++;

			if( m_ClickCount == 1 )
			{
				m_SelectedHostFristClick = p_HostName;
			}

			if( m_ClickCount == 2 )
			{
				if( m_SelectedHostFristClick == p_HostName )
				{
					ManusManager.communicationHub?.Connect( m_SelectedHost );
					m_ClickCount = 0;
				}
				else
				{
					m_ClickCount = 0;
				}
			}
		}

		/// <summary>
		/// Update hosts list.
		/// </summary>
		public void Refresh()
		{
			SearchAndFetch();
		}

		private async void SearchAndFetch()
		{
			await ManusManager.communicationHub.SearchAndFetchHosts();
		}

		/// <summary>
		/// Updates the list of lobbies in the interface.
		/// </summary>
		public void UpdateBrowser()
		{
			m_HostList = ManusManager.communicationHub?.GetHosts();

			GameObject t_LGObj;
			for( int i = 0; i < coreListRect.childCount; i++ )
			{
				t_LGObj = coreListRect.GetChild( i ).gameObject;
				if( t_LGObj && t_LGObj.gameObject.activeSelf ) Destroy( t_LGObj );
			}
			m_LobbyButtons.Clear();
			for( int i = 0; i < m_HostList.Count; i++ )
			{
				var t_GObj = Instantiate(genericCoreButton);
				t_GObj.SetActive( true );
				var t_RTrans = t_GObj.GetComponent<RectTransform>();
				t_RTrans.SetParent( coreListRect );
				t_RTrans.localPosition = Vector3.zero;
				t_RTrans.localScale = Vector3.one;

				var t_Button = t_GObj.GetComponent<Button>();
				t_Button.onClick = new Button.ButtonClickedEvent();
				m_LobbyButtons.Add( t_Button );
				var t_Item = m_HostList[i];
				string t_HostKey = m_HostList[i].hostName;
				t_Button.onClick.AddListener( delegate ()
				{
					for( int b = 0; b < m_LobbyButtons.Count; b++ )
					{
						var t_DeselectedButton = m_LobbyButtons[b].colors;
						t_DeselectedButton.normalColor = Color.white;

						m_LobbyButtons[b].colors = t_DeselectedButton;
					}
					var t_Buttons = t_GObj.GetComponent<Button>();

					var t_SelectedButtonColor = t_Buttons.colors;
					t_SelectedButtonColor.normalColor = Color.gray;
					t_SelectedButtonColor.highlightedColor = Color.gray;
					t_Buttons.colors = t_SelectedButtonColor;

					m_SelectedHost = t_Item;
				} );

				t_Button.onClick.AddListener( delegate ()
				{
					TaskOnClick( m_SelectedHost.hostName );
				} );

				var t_Text = t_RTrans.GetComponentInChildren<Text>();
				t_Text.text = $" {m_HostList[i].hostName}  |  {m_HostList[i].ipAddress} | {m_HostList[i].manusCoreVersion.major}.{m_HostList[i].manusCoreVersion.minor}";
			}
		}
	}
}
