using System.Collections.Generic;
using Manus.Utility;

using UnityEngine;

namespace Manus.Trackers
{
	public class HandTrackerSender : MonoBehaviour
	{
		[SerializeField] private Transform m_LeftHandTracker;
		[SerializeField] private Transform m_RightHandTracker;

		private CoreSDK.TrackerData m_LeftHandTrackerData;
		private CoreSDK.TrackerData m_RightHandTrackerData;

		private void Start()
		{
			// Init left tracker data
			m_LeftHandTrackerData = new CoreSDK.TrackerData();
			m_LeftHandTrackerData.trackerId = new CoreSDK.TrackerId( "UnityTracker_LeftHand" );
			m_LeftHandTrackerData.trackerType = CoreSDK.TrackerType.LeftHand;
			m_LeftHandTrackerData.quality = CoreSDK.TrackingQuality.Untrackable;

			// Init right tracker data
			m_RightHandTrackerData = new CoreSDK.TrackerData();
			m_RightHandTrackerData.trackerId = new CoreSDK.TrackerId( "UnityTracker_RightHand" );
			m_RightHandTrackerData.trackerType = CoreSDK.TrackerType.RightHand;
			m_RightHandTrackerData.quality = CoreSDK.TrackingQuality.Untrackable;
		}

		void Update()
		{
			// Update tracker data
			UpdateTrackerData(m_LeftHandTracker, ref m_LeftHandTrackerData);
			UpdateTrackerData(m_RightHandTracker, ref m_RightHandTrackerData);

			// Set tracking data to core
			List<CoreSDK.TrackerData> t_TrackerData = new List<CoreSDK.TrackerData>
			{
				m_LeftHandTrackerData,
				m_RightHandTrackerData
			};
			CommunicationHub.SendDataForTrackers( t_TrackerData );
		}

		private void UpdateTrackerData(Transform p_Tracker, ref CoreSDK.TrackerData p_TrackerData )
		{
			p_TrackerData.quality = CoreSDK.TrackingQuality.Untrackable;

			if( p_Tracker == null )
				return;

			p_TrackerData.quality = CoreSDK.TrackingQuality.Trackable;
			p_TrackerData.position = p_Tracker.position.ToManus();
			p_TrackerData.rotation = p_Tracker.rotation.ToManus();
		}
	}
}
