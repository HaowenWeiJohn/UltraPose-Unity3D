
using System;
using System.Collections.Generic;

using UnityEngine;

using Manus.Haptics;

namespace Manus.Interaction
{
	/// <summary>
	/// This is a movable object implementation, the sliders are grabbable and movablee.
	/// </summary>
	[AddComponentMenu( "Manus/Interaction/Movable Object" )]
	public class MovableObject : MonoBehaviour, IGrabbable, IValue
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
				return value / (endPosition - startPosition).magnitude;
			}
		}

		#endregion // Public Properties

		#region Public Fields
		public float startValue = 0.0f;

		/// <summary>
		/// This event is triggered when the value changes.
		/// </summary>
		public Action<MovableObject> onValueChanged;

		public Vector3 startPosition = Vector3.zero;
		public Vector3 endPosition = Vector3.forward;

        public enum HapticsBehavior {
            None,
            Ramp
        };

        public HapticsBehavior m_HapticsBehavior = HapticsBehavior.Ramp;
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
		}

		List<ExtraGrabInfo> m_GrabInfo = new List<ExtraGrabInfo>();

		private void Start()
		{
			m_Value = startValue;
			SetPositionFromValue();
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
                    if(t_HandHaptics != null)
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
		}

		void SetPositionFromValue()
		{
			var t_Line = (endPosition - startPosition);
			t_Line.Normalize();
			transform.localPosition = startPosition + t_Line * m_Value;
		}

        void DoHapticsBehavior()
        {
            for(int i = 0;i < m_GrabInfo.Count;i++)
            {
                HandHaptics t_HandHaptics = m_GrabInfo[i].info.interactor.transform.GetComponent<HandHaptics>();
                if(t_HandHaptics != null)
                {
                    int t_NumFingers = t_HandHaptics.GetNumberOfFingers();
                    for(int j = 0;j < t_NumFingers;j++)
                    {
                        switch(m_HapticsBehavior)
                        {
                            case HapticsBehavior.Ramp:
                                t_HandHaptics.SetHapticsStrengthOverride(j, m_Value);
                                break;

                            case HapticsBehavior.None:
                            default:
                                break;
                        }
                    }
                }
            }
        }

		float NearestValue( Vector3 p_Pos )
		{
			var t_Line = (endPosition - startPosition);
			var t_Length = t_Line.magnitude;
			t_Line.Normalize();

			var t_RelPos = p_Pos - startPosition;
			var t_PosOnLine = Vector3.Dot(t_RelPos, t_Line);
			return Mathf.Clamp( t_PosOnLine, 0.0f, t_Length );
		}

		/// <summary>
		/// Called every FixedUpdate when this is grabbed.
		/// This is where the rotation of the dial is calculated and changed.
		/// </summary>
		/// <param name="p_Object">Contains information about the grab</param>
		public void OnGrabbedFixedUpdate( GrabbedObject p_Object )
		{
			float t_Move = 0.0f;

			for( int i = 0; i < m_GrabInfo.Count; i++ )
			{
				var t_Grab = m_GrabInfo[i];
				GrabbedObject.Info t_Info = t_Grab.info;
				Vector3 t_InteractorPos = t_Info.interactor.transform.TransformPoint(t_Info.handToObject);
				if( transform.parent )
				{
					t_InteractorPos = transform.parent.InverseTransformPoint( t_InteractorPos );
				}
				t_Move += NearestValue( t_InteractorPos );
			}
			m_Value = t_Move / p_Object.hands.Count;

			SetPositionFromValue();

			onValueChanged?.Invoke( this );

            DoHapticsBehavior();
		}

		public void OnGrabbedHandPose( InteractionHand p_Object, GrabbedObject.Info p_Info )
		{
			p_Object.visualHandRoot.position = transform.TransformPoint( p_Info.objectToHand );
			p_Object.visualHandRoot.rotation = transform.rotation * p_Info.objectToHandRotation;
		}

		private void OnDrawGizmosSelected()
		{
			Color t_Col = Gizmos.color;
			Gizmos.color = Color.yellow;

			Vector3 t_Start = startPosition;
			Vector3 t_End = endPosition;
			if( transform.parent )
			{
				t_Start = transform.parent.TransformPoint( startPosition );
				t_End = transform.parent.TransformPoint( endPosition );
			}

			Gizmos.DrawLine( t_Start, t_End );

			Gizmos.color = t_Col;
		}
	}
}
