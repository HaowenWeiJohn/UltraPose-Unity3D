using System;

using UnityEngine;

namespace Manus.Haptics
{
	/// <summary>
	/// This is the class which needs to be on every hand in order for all the other hand related components to function correctly.
	/// In order for the haptics to function each of the fingers on the hand will need a FingerHaptics class with the correct finger type set.
	/// These are generated via the skeleton if not present as long as haptics are enabled on the skeleton.
	/// The FingerHaptics will generate haptic values for this class to give to the hand.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu( "Manus/Haptics/Hand Haptics" )]
	public class HandHaptics : MonoBehaviour
	{
		private FingerHaptics[] m_Fingers = new FingerHaptics[(int)FingerType.Pinky]; // the number of haptics fingers has to be five for the haptic commands to be sent correctly
		private float[] m_HapticsStrength;
		private bool m_HapticsEnabled = false;
		private Skeletons.Skeleton m_Skeleton;
		private float[] m_HapticsStrengthOverride;

		/// <summary>
		/// The hand type used by several components, most importantly the CommunicationHub uses this to
		/// identify what hand data needs to be applied to this hand.
		/// In order to get data from Hermes correctly this should either be Utility.HandType.Left or Utility.HandType.Right.
		/// </summary>
		private CoreSDK.Side type = CoreSDK.Side.Invalid;

		/// <summary>
		/// Fetch skeleton component from parent.
		/// </summary>
		private void Awake()
		{
			if( m_Skeleton != null )
				return;
			m_Skeleton = gameObject.GetComponentInParent<Skeletons.Skeleton>();
		}

		/// <summary>
		/// Function for setting up hands used by skeleton.
		/// </summary>
		/// <param name="p_HandSide">Side of the hand</param>
		/// <param name="p_HapticsEnabled">Whether haptics is enabled</param>
		public void SetupHand( CoreSDK.Side p_HandSide, bool p_HapticsEnabled )
		{
			type = p_HandSide;
			m_HapticsEnabled = p_HapticsEnabled;
		}

		/// <summary>
		/// Function for setting up the finger haptics
		/// </summary>
		public void SetupFingerHaptics()
		{
			FingerHaptics[] t_Fingers = GetComponentsInChildren<FingerHaptics>();

			// order the fingers so that thumb is 0 and pinky is 4, this will be used to send the haptics data to the correct fingers
			for( int i = 0; i < t_Fingers.Length; i++ )
			{
				switch( t_Fingers[i].GetFingerType() )
				{
					case FingerType.Thumb:
						m_Fingers[0] = t_Fingers[i];
						break;
					case FingerType.Index:
						m_Fingers[1] = t_Fingers[i];
						break;
					case FingerType.Middle:
						m_Fingers[2] = t_Fingers[i];
						break;
					case FingerType.Ring:
						m_Fingers[3] = t_Fingers[i];
						break;
					case FingerType.Pinky:
						m_Fingers[4] = t_Fingers[i];
						break;
				}
			}
			m_HapticsStrength = new float[m_Fingers.Length];

			m_HapticsStrengthOverride = new float[m_Fingers.Length];
			for( int i = 0; i < m_HapticsStrengthOverride.Length; i++ )
			{
				m_HapticsStrengthOverride[i] = -1f;
			}
		}

		/// <summary>
		/// Returns the number of fingers for which we have haptics information set up
		/// </summary>
		public int GetNumberOfFingers()
		{
			return m_Fingers.Length;
		}

		/// <summary>
		/// Sets the override haptics strength for a particular finger
		/// Override values will be used in place of physically determined collider values unless the override value is -1
		/// </summary>
		/// <param name="p_FingerIndex">index of the finger to set haptics strength for</param>
		/// <param name="p_Strength">Haptic strength to set, [0,1] or -1 for no override</param>
		public void SetHapticsStrengthOverride( int p_FingerIndex, float p_Strength )
		{
			m_HapticsStrengthOverride[p_FingerIndex] = p_Strength;
		}

		/// <summary>
		/// Update the boolean associated to the haptics enabled based on the input from the user in the GUI.
		/// </summary>
		/// <param name="p_HapticsEnabled">New haptics state</param>
		public void UpdateHapticsEnabled( bool p_HapticsEnabled )
		{
			m_HapticsEnabled = p_HapticsEnabled;
		}

		/// <summary>
		/// Called by Unity.
		/// Update and trigger haptics if needed.
		/// Sets finger haptic value for a finger at a given index, Thumb is 0, Pinky is 4.
		/// The Haptic range is from 0.0 to 1.0.
		/// </summary>
		private void FixedUpdate()
		{
			if( !m_HapticsEnabled ) return;
			if( ManusManager.communicationHub == null ) return;
			if( ManusManager.communicationHub.currentState != CommunicationHub.State.Connected ) return;
			if( !m_Skeleton.enabled ) return;
			if( m_Skeleton.skeletonData.id == 0 ) return;

			if( !ManusManager.communicationHub.DoesSkeletonGloveSupportHaptics( m_Skeleton.skeletonData.id, type ) ) return;

			for( int i = 0; i < m_Fingers.Length; i++ )
			{
				if( m_Fingers[i] == null ) continue;
				float t_FingerHapticsValue = m_Fingers[ i ].GetHapticValue();
				m_HapticsStrength[i] = m_HapticsStrengthOverride[i] == -1f ? Mathf.Clamp01( t_FingerHapticsValue ) : Mathf.Clamp01( m_HapticsStrengthOverride[i] );
			}

			ManusManager.communicationHub?.SendHapticDataForSkeleton( m_Skeleton.skeletonData.id, type, m_HapticsStrength );
		}
	}
}
