using UnityEngine;

using Manus.Utility;

namespace Manus.Trackers
{
	[AddComponentMenu( "Manus/Trackers/Custom Tracker" )]
	public class CustomTracker : MonoBehaviour
	{
		[Tooltip("ID of the tracker, this must be unique.")]
		public string trackerID = "UnityCustomTracker";

		[HideInInspector]
		public CoreSDK.TrackerType type = CoreSDK.TrackerType.Unknown;
		[HideInInspector]
		public uint userId = 0;
		[HideInInspector]
		public CoreSDK.ManusVec3 position;
		[HideInInspector]
		public CoreSDK.ManusQuaternion rotation;

		private void OnEnable()
		{
			ManusManager.communicationHub.RegisterCustomTracker( this );
		}

		private void OnDisable()
		{
			ManusManager.communicationHub.UnregisterCustomTracker( this );
		}

		private void Update()
		{
			position = transform.localPosition.ToManus();
			rotation = transform.localRotation.ToManus();
		}
	}
}
