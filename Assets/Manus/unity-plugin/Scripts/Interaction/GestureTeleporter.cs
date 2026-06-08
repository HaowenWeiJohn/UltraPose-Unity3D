using System.Collections.Generic;

using UnityEngine;

namespace Manus.Interaction
{
	public class GestureTeleporter : MonoBehaviour
	{
		[Header("Gesture")]
		[SerializeField] private string m_AimGestureName = "Gun";
		[SerializeField] private float m_AimPercentageThreshold = 0.9f;
		private uint m_AimGestureID = 0;
		float m_AimProbability = 0.0f;

		[SerializeField] private string m_TeleportGestureName = "Index";
		[SerializeField] private float m_TeleportPercentageThreshold = 0.9f;
		private uint m_TeleportGestureID = 0;
		float m_TeleportProbability = 0.0f;

		[Header("Others")]
		[SerializeField]
		private bool m_TeleportingEnabled;

		private uint m_GloveID = 0;
		private Skeletons.Skeleton m_Skeleton;
		[SerializeField]
		private CoreSDK.Side m_Side = CoreSDK.Side.Invalid;

		[SerializeField]
		private Transform m_PlayerTransform;
		[SerializeField]
		private Transform m_PlayerCameraTransform;
		[SerializeField]
		private GameObject m_TeleportPreview;

		[SerializeField]
		private LayerMask m_TeleportMask;
		[SerializeField]
		private LineRenderer m_LineRenderer;
		[SerializeField]
		private Material m_InvalidTeleportMaterial;
		[SerializeField]
		private Material m_ValidTeleportMaterial;

		[SerializeField]
		private float m_MaxCurveLength = 25.0f;
		[SerializeField]
		private float m_CurveSegmentLength = 0.5f;
		[SerializeField]
		private float m_CurveDropoff = 1f;
		[SerializeField]
		private Vector3 m_CurveDirection = Vector3.forward;

		private Vector3 m_LastAimedPosition = Vector3.zero;

		bool m_ReadyToTeleport = false;
		TurnableObject m_TeleToggler = null;

		[SerializeField]
		private MeshRenderer m_TeleportIcon;
		[SerializeField]
		private Material m_TeleportEnabledMaterial;
		[SerializeField]
		private Material m_TeleportDisabledMaterial;

		private void Start()
		{
			m_Skeleton = GetComponentInParent<Skeletons.Skeleton>();
			m_LastAimedPosition = m_PlayerTransform.position;
			m_TeleportingEnabled = true;
			TurnOffTeleportVisuals();
			m_TeleToggler = GetComponentInChildren<TurnableObject>();
			if( m_TeleToggler )
			{
				m_TeleToggler.onValueChanged += OnTeleWheelTurn;
				OnTeleWheelTurn( m_TeleToggler );
			}
		}

		private void OnEnable()
		{
			ManusManager.communicationHub.onLandscapeEvent.AddListener( OnLandscapeData );
			ManusManager.communicationHub.onGestureData.AddListener( OnGestureData );
		}

		private void OnDisable()
		{
			ManusManager.communicationHub.onLandscapeEvent.RemoveListener( OnLandscapeData );
			ManusManager.communicationHub.onGestureData.RemoveListener( OnGestureData );
		}

		private void Update()
		{
			if( !m_TeleportingEnabled )
			{
				if( m_TeleportIcon && m_TeleportIcon.material != m_TeleportDisabledMaterial )
				{
					m_TeleportIcon.material = m_TeleportDisabledMaterial;
				}
				TurnOffTeleportVisuals();
				return;
			}
			if( m_TeleportIcon && m_TeleportIcon.material != m_TeleportEnabledMaterial )
			{
				m_TeleportIcon.material = m_TeleportEnabledMaterial;
			}

			if( m_TeleportProbability < m_AimProbability )
			{
				if( m_AimProbability > m_AimPercentageThreshold )
				{
					AimTeleport();
					m_ReadyToTeleport = true;
				}
				if( m_ReadyToTeleport )
				{
					AimTeleport();
				}
			}
			else
			{
				if( m_ReadyToTeleport )
				{
					AimTeleport();
					if( m_TeleportProbability > m_TeleportPercentageThreshold )
					{
						Teleport( m_LastAimedPosition );
						m_ReadyToTeleport = false;
						TurnOffTeleportVisuals();
					}
				}
				else
				{
					TurnOffTeleportVisuals();
				}
			}

		}

		void TurnOffTeleportVisuals()
		{
			m_LineRenderer.enabled = false;
			m_TeleportPreview.SetActive( false );
		}

		private void AimTeleport()
		{
			m_LineRenderer.enabled = true;
			m_TeleportPreview.SetActive( true );

			Vector3[] t_Curve;
			RaycastHit t_RaycastHit;

			if( GetCurveCollision( transform.position, transform.TransformDirection( m_CurveDirection ), out t_Curve, out t_RaycastHit ) )
			{
				var t_TA = t_RaycastHit.collider.gameObject.GetComponent<TeleportationArea>();
				m_LastAimedPosition = t_RaycastHit.point;
				if( t_TA )
				{
					m_LastAimedPosition = t_TA.transform.position;
				}
				m_LineRenderer.material = m_ValidTeleportMaterial;
				m_TeleportPreview.transform.position = m_LastAimedPosition;
			}
			else
			{
				m_LineRenderer.material = m_InvalidTeleportMaterial;
				m_TeleportPreview.SetActive( false );
			}

			m_LineRenderer.positionCount = t_Curve.Length;
			m_LineRenderer.SetPositions( t_Curve );
		}

		private bool GetCurveCollision( Vector3 p_Start, Vector3 p_Direction, out Vector3[] p_CurvePoints, out RaycastHit p_Hit )
		{
			List<Vector3> t_CurvePoints = new List<Vector3>
			{
				p_Start
			};
			bool t_Result = false;
			p_Hit = new RaycastHit();
			float t_Length = 0.0f;
			Vector3 t_PrevPoint = p_Start;
			Vector3 t_Velocity = p_Direction;
			Vector3 t_Grav = Physics.gravity.normalized;
			while( m_MaxCurveLength > t_Length )
			{
				Vector3 t_Point = t_PrevPoint + (t_Velocity * m_CurveSegmentLength);
				t_Velocity += t_Grav * m_CurveDropoff * m_CurveSegmentLength;
				t_Length += m_CurveSegmentLength;
				if( Physics.Linecast( t_PrevPoint, t_Point, out p_Hit, m_TeleportMask ) )
				{
					t_Result = true;
					t_CurvePoints.Add( p_Hit.point );
					break;
				}
				t_CurvePoints.Add( t_Point );
				t_PrevPoint = t_Point;
			}

			p_CurvePoints = t_CurvePoints.ToArray();
			return t_Result;
		}

		private void Teleport( Vector3 p_NewPosition )
		{
			m_PlayerTransform.position = p_NewPosition;
		}

		void OnTeleWheelTurn( TurnableObject p_Wheel )
		{
			m_TeleportingEnabled = false;
			if( p_Wheel.value < -25)
			{
				m_TeleportingEnabled = true;
			}
		}

		#region Callbacks

		private void OnLandscapeData( CommunicationHub.Landscape p_Landscape )
		{
			m_GloveID = InteractionHand.GetGloveID( p_Landscape, m_Skeleton.skeletonData.id, m_Side );
			m_AimGestureID = InteractionHand.GetGestureID( p_Landscape, m_AimGestureName );
			m_TeleportGestureID = InteractionHand.GetGestureID( p_Landscape, m_TeleportGestureName );
		}

		private void OnGestureData( CoreSDK.GestureStream p_GestureStream )
		{
			for( int i = 0; i < p_GestureStream.gestureProbabilities.Count; i++ )
			{
				var t_Probabilities = p_GestureStream.gestureProbabilities[i];
				if( m_GloveID != t_Probabilities.id || t_Probabilities.isUserID )
					continue;

				for( int p = 0; p < t_Probabilities.gestureData.Length; p++ )
				{
					var t_Gesture = t_Probabilities.gestureData[p];
					if( t_Gesture.id == m_AimGestureID )
					{
						m_AimProbability = t_Gesture.percent;
					}
					if( t_Gesture.id == m_TeleportGestureID )
					{
						m_TeleportProbability = t_Gesture.percent;
					}
				}
			}
		}

		#endregion
	}
}
