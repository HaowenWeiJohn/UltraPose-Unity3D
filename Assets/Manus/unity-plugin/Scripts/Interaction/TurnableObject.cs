using Manus.Haptics;
using System;
using System.Collections.Generic;

using UnityEngine;

namespace Manus.Interaction
{
	/// <summary>
	/// This is a turnable object implementation, the valves, wheels, dials are grabbable and rotatable.
	/// </summary>
	[AddComponentMenu( "Manus/Interaction/Turnable Object" )]
	public class TurnableObject : MonoBehaviour, IGrabbable, IValue
	{
		#region Fields & Properties
		#region Public Properties
		/// <summary>
		/// The dial's rotation value, this value is between the rotationLimits.
		/// </summary>
		public float value
		{
			get
			{
				if( rotationSteps > 0 )
				{
					return m_SteppedValue;
				}
				return m_Value;
			}
		}

		/// <summary>
		/// The normalized value, if there is no limit then 1.0f is a full 360 degree rotation
		/// </summary>
		public float normalizedValue
		{
			get
			{
				if( limitRotation )
				{
					return (value - rotationLimits.x) / (rotationLimits.y - rotationLimits.x);
				}
				return value / 360.0f;
			}
		}

		#endregion // Public Properties

		#region Public Fields
		public float startValue = 0.0f;

		public bool limitRotation = true;
		/// <summary>
		/// The rotational limits in degrees.
		/// This value CAN be larger than 360 degrees!
		/// </summary>
		public Vector2 rotationLimits = new Vector3(0.0f, 90.0f);

		public float rotationSteps = -1.0f;

		public bool snapToStep = false;

		/// <summary>
		/// This event is triggered when the value changes.
		/// </summary>
		public Action<TurnableObject> onValueChanged;

		public Vector3 turnAxis = Vector3.forward;
		public Vector3 upAxis = Vector3.up;
		#endregion // Public Fields

		#region Protected Types
		float m_Value = 0.0f;
		float m_SteppedValue = 0.0f;
		#endregion // Protected Fields

		#endregion // Fields & Properties

		public class ExtraGrabInfo
		{
			public GrabbedObject.Info info;
			public Vector3 previousPosition;
			public Quaternion previousRotation;
			public Quaternion handToObjectRotation;
		}

		List<ExtraGrabInfo> m_GrabInfo = new List<ExtraGrabInfo>();

		private void Start()
		{
			m_Value = startValue;

			if( limitRotation )
			{
				m_Value = Mathf.Clamp( m_Value, rotationLimits.x, rotationLimits.y );
			}

			transform.localRotation = Quaternion.AngleAxis( m_Value, turnAxis );
			if( rotationSteps > 0.0f )
			{
				float t_SV = m_Value / rotationSteps;
				float t_Rem = Mathf.Abs(t_SV % 1.0f);
				if( t_Rem < 0.4f || t_Rem > 0.6f )
				{
					m_SteppedValue = Mathf.Round( t_SV ) * rotationSteps;
				}

				transform.localRotation = Quaternion.AngleAxis( m_SteppedValue, turnAxis );
				m_Value = m_SteppedValue;
			}
			onValueChanged?.Invoke( this );
		}

		/// <summary>
		/// Called when this starts getting grabbed.
		/// </summary>
		/// <param name="p_Object">Contains information about the grab</param>
		public void OnGrabbedStart( GrabbedObject p_Object )
		{
		}

		/// <summary>
		/// Called when this stops being grabbed.
		/// </summary>
		/// <param name="p_Object">Contains information about the grab</param>
		public void OnGrabbedEnd( GrabbedObject p_Object )
		{
		}

		/// <summary>
		/// Called when a new grabber starts grabbing this.
		/// </summary>
		/// <param name="p_Object">Contains information about the grab</param>
		/// <param name="p_Info">Contains information about the added grabber</param>
		public void OnAddedInteractingInfo( GrabbedObject p_Object, GrabbedObject.Info p_Info )
		{
			ExtraGrabInfo t_ExtraGrabInfo = new ExtraGrabInfo();
			t_ExtraGrabInfo.info = p_Info;
			t_ExtraGrabInfo.previousPosition = p_Info.interactor.transform.position;
			t_ExtraGrabInfo.previousRotation = p_Info.interactor.transform.rotation;
			t_ExtraGrabInfo.handToObjectRotation = Quaternion.Inverse( p_Info.interactor.transform.rotation ) * transform.transform.rotation;
			m_GrabInfo.Add( t_ExtraGrabInfo );
		}

		/// <summary>
		/// Called when a grabber stops grabbing this.
		/// </summary>
		/// <param name="p_Object">Contains information about the grab</param>
		/// <param name="p_Info">Contains information about the removed grabber</param>
		public void OnRemovedInteractingInfo( GrabbedObject p_Object, GrabbedObject.Info p_Info )
		{
			for( int i = 0; i < m_GrabInfo.Count; i++ )
			{
				if( m_GrabInfo[i].info == p_Info )
				{
					// cede haptics control back to physical colliders	
                    HandHaptics t_HandHaptics = m_GrabInfo[i].info.interactor.transform.GetComponent<HandHaptics>();
                    if (t_HandHaptics != null)
                    {
                        int t_NumFingers = t_HandHaptics.GetNumberOfFingers();
                        for (int j = 0; j < t_NumFingers; j++)
                        {
                            t_HandHaptics.SetHapticsStrengthOverride(j, -1);
                        }
                    }
					
					m_GrabInfo.RemoveAt( i );
				}
			}
			if( rotationSteps > 0.0f && m_GrabInfo.Count == 0 )
			{
				transform.localRotation = Quaternion.AngleAxis( m_SteppedValue, turnAxis );
				m_Value = m_SteppedValue;
			}
		}

		Vector2 Project( Vector3 p_LocalPoint )
		{
			Vector3 t_PlaneXDir = Vector3.Cross(upAxis,turnAxis);
			Vector3 t_PlaneYDir = upAxis;

			return new Vector2( Vector3.Dot( t_PlaneXDir, p_LocalPoint ), Vector3.Dot( t_PlaneYDir, p_LocalPoint ) );
		}

		/// <summary>
		/// Called every FixedUpdate when this is grabbed.
		/// This is where the rotation of the dial is calculated and changed.
		/// </summary>
		/// <param name="p_Object">Contains information about the grab</param>
		public void OnGrabbedFixedUpdate( GrabbedObject p_Object )
		{
			transform.localRotation = Quaternion.AngleAxis( m_Value, turnAxis );

			float t_Ang = 0.0f;

			for( int i = 0; i < m_GrabInfo.Count; i++ )
			{
				var t_Grab = m_GrabInfo[i];
				GrabbedObject.Info t_Info = t_Grab.info;
				Vector3 t_PrevDir = transform.InverseTransformPoint( t_Grab.previousPosition );
				Vector3 t_CurrentDir = transform.InverseTransformPoint( t_Info.interactor.transform.position );
				Vector2 t_PrevProjDir = Project( t_PrevDir );
				Vector2 t_CurrentProjDir = Project( t_CurrentDir );

				float t_PosDeltaAngle = Vector2.SignedAngle( t_PrevProjDir, t_CurrentProjDir );

				float t_Blend = Mathf.Clamp01((t_PrevProjDir.sqrMagnitude * t_CurrentProjDir.sqrMagnitude) / 0.0001f);

				Quaternion t_Rot = t_Info.interactor.transform.rotation * t_Grab.handToObjectRotation;

				Vector3 t_Old = transform.rotation * upAxis;
				Vector3 t_New = t_Rot * upAxis;

				Vector2 t_OldProjDir = Project( transform.InverseTransformDirection(t_Old) );
				Vector2 t_NewProjDir = Project( transform.InverseTransformDirection(t_New) );
				float t_RotDeltaAngle = Vector2.SignedAngle( t_OldProjDir, t_NewProjDir );

				t_Ang += Mathf.Lerp( t_RotDeltaAngle, t_PosDeltaAngle, t_Blend );
			}
			m_Value += t_Ang / p_Object.hands.Count;
			if( limitRotation )
			{
				m_Value = Mathf.Clamp( m_Value, rotationLimits.x, rotationLimits.y );
			}

			transform.localRotation = Quaternion.AngleAxis( m_Value, turnAxis );

			for( int i = 0; i < m_GrabInfo.Count; i++ )
			{
				m_GrabInfo[i].previousPosition = m_GrabInfo[i].info.interactor.transform.position;
				m_GrabInfo[i].previousRotation = m_GrabInfo[i].info.interactor.transform.rotation;
				m_GrabInfo[i].handToObjectRotation = Quaternion.Inverse( m_GrabInfo[i].previousRotation ) * transform.transform.rotation;
			}

			if( rotationSteps > 0.0f )
			{
				float t_SV = m_Value / rotationSteps;
				float t_Rem = Mathf.Abs(t_SV % 1.0f);
				float t_LastSteppedValue = m_SteppedValue;
				if( t_Rem < 0.4f || t_Rem > 0.6f )
				{
					m_SteppedValue = Mathf.Round( t_SV ) * rotationSteps;
				}

				if( snapToStep )
				{
					transform.localRotation = Quaternion.AngleAxis( m_SteppedValue, turnAxis );
					for(int i = 0;i < m_GrabInfo.Count;i++)
					{
                        HandHaptics t_HandHaptics = m_GrabInfo[i].info.interactor.transform.GetComponent<HandHaptics>();
                        if (t_HandHaptics != null)
                        {
                            int t_NumFingers = t_HandHaptics.GetNumberOfFingers();	
                            if (t_LastSteppedValue != m_SteppedValue)
                            {
                                // BRRRRAP
                                for (int j = 0; j < t_NumFingers; j++)
                                {
                                    t_HandHaptics.SetHapticsStrengthOverride(j, 1);
                                }
                            }
                            else
                            {
                                for (int j = 0; j < t_NumFingers; j++)
                                {
                                    t_HandHaptics.SetHapticsStrengthOverride(j, 0);
                                }
                            }
                        }	
					}
				}
			}

			onValueChanged?.Invoke( this );
		}

		public void OnGrabbedHandPose( InteractionHand p_Object, GrabbedObject.Info p_Info )
		{
			p_Object.visualHandRoot.position = transform.TransformPoint( p_Info.objectToHand );
			p_Object.visualHandRoot.rotation = transform.rotation * p_Info.objectToHandRotation;
		}

		private void OnDrawGizmosSelected()
		{
			Color t_Col = Gizmos.color;
			Gizmos.color = Color.blue;
			Gizmos.DrawLine( transform.position, transform.position + transform.TransformDirection( turnAxis ) );
			Gizmos.color = Color.green;
			Gizmos.DrawLine( transform.position, transform.position + transform.TransformDirection( upAxis ) );
			Gizmos.color = t_Col;
		}
	}
}
