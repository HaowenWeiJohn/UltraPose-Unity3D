using Manus;
using Manus.Utility;

using UnityEngine;

public class RawEndpointHand : MonoBehaviour
{
	[SerializeField]
	private uint m_GloveID;

	[SerializeField]
	private CoreSDK.Side m_Side;

	[SerializeField]
	private bool m_IMUEnabled;

	[SerializeField] 
	private Transform m_Case;

	[SerializeField]
	private Transform[] m_Sensors;

	private CoreSDK.RawDeviceData m_RawDeviceData;
	private Quaternion m_InitialCaseRotation;


	private void Awake()
	{
		m_InitialCaseRotation = m_Case.rotation;
	}


	private void OnEnable()
	{
		ManusManager.communicationHub.onRawDeviceData.AddListener( RawDataEvent );
	}
	
	private void OnDisable()
	{
		ManusManager.communicationHub.onRawDeviceData.RemoveListener( RawDataEvent );
	}

	void Update()
    {
	    if (m_GloveID == 0)
		    return;

	    if (m_RawDeviceData.sensorCount == 0)
		    return;

	    for (uint i = 0; i < m_RawDeviceData.sensorCount; i++)
	    {
		    m_Sensors[i].localPosition = m_RawDeviceData.sensorData[i].position.FromManus();
		    m_Sensors[i].localRotation = m_RawDeviceData.sensorData[i].rotation.FromManus() * Quaternion.Euler(-90, 0, 180);
	    }

	    m_Case.rotation = m_IMUEnabled ? m_RawDeviceData.rotation.FromManus() : m_InitialCaseRotation;
    }

	private void RawDataEvent( CoreSDK.RawData p_RawData )
	{
		foreach (var t_RawDeviceData in p_RawData.rawDeviceData)
		{
			if (m_GloveID == t_RawDeviceData.id)
			{
				if (t_RawDeviceData.sensorData[0].position.x == 0 && t_RawDeviceData.sensorData[0].position.z == 0 && t_RawDeviceData.sensorData[0].position.z == 0)
					return;
                
				m_RawDeviceData = t_RawDeviceData;
				return;
			}
		}
		
	}
}
