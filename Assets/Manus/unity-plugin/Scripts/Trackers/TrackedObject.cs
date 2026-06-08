using UnityEngine;
using Manus.Trackers;
#if UNITY_EDITOR
using Manus;

using UnityEditor;
#endif

namespace Manus.Trackers
{
	/// <summary>
	/// This component allows an object to be moved according to a tracker position and orientation.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu( "Manus/Trackers/Tracked Object" )]
	public class TrackedObject : MonoBehaviour
	{
		/// <summary>
		/// Sets active to false when tracker does not exist
		/// </summary>
		[Tooltip("Sets active to false when tracker does not exist.")]
		public bool autoToggle = true;

		[Tooltip("The type of the tracker that it is.")]
		public CoreSDK.TrackerType type;
		[Tooltip("The ID of the user the tracker belongs to.")]
		public long userId;
		[Tooltip("The Tracker this object belongs to.")]
		public Tracker tracker;

		/// <summary>
		/// Removes itself from the tracked object list
		/// </summary>
		private void OnDestroy()
		{
			if( isActiveAndEnabled )
			{
				ManusManager.communicationHub?.UnregisterTrackedObject( this );
			}
		}

		/// <summary>
		/// Add the tracked object to the tracker manager
		/// </summary>
		private void OnEnable()
		{
			if( tracker == null )
			{
				ManusManager.communicationHub?.RegisterTrackedObject( this );
				SetTracker( new Tracker( (uint)userId, type ), false );
			}
		}

		/// <summary>
		/// Updates the position and rotation if a tracker is available.
		/// </summary>
		private void Update()
		{
			if( tracker == null ) return;
			transform.localPosition = tracker.position;
			transform.localRotation = tracker.rotation;
		}

		/// <summary>
		/// Sets the tracker to use, NULL ensures the gameobject gets disabled.
		/// </summary>
		/// <param name="p_Tracker"></param>
		/// <param name="p_HasData"></param>
		private void SetTracker( Tracker p_Tracker, bool p_HasData )
		{
			tracker = p_Tracker;

			if( autoToggle )
				gameObject.SetActive( p_HasData );
		}

		public void ApplyTrackerData( CoreSDK.TrackerData p_TrackerData, bool p_HasData )
		{
			SetTracker( new Tracker( p_TrackerData ), p_HasData );
		}
	}
}

#if UNITY_EDITOR
/// <summary>
/// Custom inspector for tracked objects.
/// </summary>
[CustomEditor( typeof( TrackedObject ) )]
public class TrackedObjectEditor : Editor
{
	/// <summary>
	/// Draw inspector view.
	/// </summary>
	public override void OnInspectorGUI()
	{
		TrackedObject t_Object = target as TrackedObject;

		t_Object.autoToggle = EditorGUILayout.Toggle( new GUIContent( "Auto Toggle", "Toggle automatically when there is data" ), t_Object.autoToggle );
		t_Object.type =
			(CoreSDK.TrackerType)EditorGUILayout.EnumPopup( new GUIContent( "Tracker Type", "Type of tracker to pull data from" ), t_Object.type );
		t_Object.userId = EditorGUILayout.LongField( new GUIContent( "User", "ID of user to pull tracker data from" ), t_Object.userId );
	}
}

#endif
