using Manus.Utility;

using UnityEngine;

namespace Manus.Trackers
{
	/// <summary>
	/// Tracker information
	/// </summary>
	public class Tracker
	{
		/// <summary>
		/// The type that this tracker is.
		/// </summary>
		public CoreSDK.TrackerType type;

		/// <summary>
		/// The unique ID of the tracker
		/// Its recommended to get this ID from the hardware, so it's unique and doesn't change  
		/// </summary>
		public string deviceID;

		/// <summary>
		/// Is true when its a VR headset
		/// </summary>
		public bool isHMD;

		/// <summary>
		/// What index does this tracker belong to, usually it's 0.
		/// When multiple trackers of the same type are connected this number increases depending on when it connected.
		/// </summary>
		public int typeIndex;

		/// <summary>
		/// What user does this tracker belong to.
		/// </summary>
		public uint userId;

		/// <summary>
		/// The tracking system device index, this isn't always used.
		/// </summary>
		public uint deviceIndex;

		/// <summary>
		/// The tracker's position.
		/// </summary>
		public Vector3 position;

		/// <summary>
		/// The tracker's rotation.
		/// </summary>
		public Quaternion rotation;

		/// <summary>
		/// Instantiates the tracker class with 'invalid' values.
		/// </summary>
		public Tracker()
		{
			type = CoreSDK.TrackerType.Unknown;
			typeIndex = -1;
			userId = 0;
			deviceIndex = 6969;
			position = Vector3.zero;
			rotation = Quaternion.identity;
		}

		/// <summary>
		/// Instantiates the tracker class with data coming from SDK.
		/// </summary>
		/// <param name="p_TrackerData"></param>
		public Tracker( CoreSDK.TrackerData p_TrackerData )
		{
			type = p_TrackerData.trackerType;
			userId = p_TrackerData.userId;
			deviceID = p_TrackerData.trackerId.id;
			position = p_TrackerData.position.FromManus();
			rotation = p_TrackerData.rotation.FromManus();
		}

		/// <summary>
		/// Instantiates the tracker class with a user index and type.
		/// </summary>
		/// <param name="p_User"></param>
		/// <param name="p_Type"></param>
		public Tracker( uint p_User, CoreSDK.TrackerType p_Type )
		{
			userId = p_User;
			type = p_Type;
		}
	}
}
