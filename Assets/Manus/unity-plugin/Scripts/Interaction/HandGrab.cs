using System.Collections.Generic;
using System.Linq;

using Manus.Skeletons;

using UnityEngine;

namespace Manus.Interaction
{
	public class HandGrab : MonoBehaviour
	{
		[Header("Physics")]
		[SerializeField] private float m_GrabRadius = 0.05f;
		[SerializeField] private LayerMask m_LayerMask = ~0;

		[Header("Gesture")]
		[SerializeField] private string m_GrabGestureName = "Grab";
		[SerializeField] private float m_GrabPercentageThreshold = 0.9f;
		[SerializeField] private float m_GrabPercentage = 0f;

		// Hand Data
		private uint m_GloveID = 0;
		private uint m_GrabGestureID = 0;
		private Skeleton m_Skeleton;
		private CoreSDK.Side m_Side = CoreSDK.Side.Invalid;
		private Vector3 m_LocalPalmPosition;
		private Vector3 m_LocalPalmDirection;

		// Grab Data
		private bool m_TestGrab = false;
		private bool m_Grabbing = false;
		private List<Collider> m_InteractableColliders;
		private GrabbedObject m_GrabbedObject;
		private CoreSDK.GestureProbabilities m_GestureData;

		public GrabbedObject grabbedObject { get { return m_GrabbedObject; } }

		private void OnEnable()
		{
			m_Skeleton = GetComponentInParent<Skeleton>();
			if( TryGetHandChain( m_Skeleton, out Chain t_HandChain ) )
			{
				m_Side = t_HandChain.dataSide;
				m_LocalPalmPosition = CalculatePalmPosition( m_Skeleton, t_HandChain );
				m_LocalPalmDirection = CalculatePalmDirection( m_Skeleton, CalculatePalmPosition(), t_HandChain, m_Side );
			}

			m_InteractableColliders = new List<Collider>();

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
			TestGrabInput();
		}

		private void FixedUpdate()
		{
			UpdateGrabPercentage();
			DetectGrabGesture();
		}

		private void TestGrabInput()
		{
			if( Input.GetKeyDown( KeyCode.DownArrow ) )
				m_TestGrab = true;

			if( Input.GetKeyDown( KeyCode.UpArrow ) )
				m_TestGrab = false;
		}

		#region Grabbing

		private void UpdateGrabPercentage()
		{
			// No gestures
			if( (m_GestureData.gestureData == null || m_GestureData.gestureData.Length == 0) )
			{
				m_GrabPercentage = 0f;
				return;
			}

			// Check grab percentage
			for( int i = 0; i < m_GestureData.gestureData.Length; i++ )
			{
				var t_Gesture = m_GestureData.gestureData[i];
				if( t_Gesture.id != m_GrabGestureID )
					continue;

				m_GrabPercentage = t_Gesture.percent;
			}
		}

		private void DetectGrabGesture()
		{
			bool t_IsGrabbing = m_GrabPercentage > m_GrabPercentageThreshold || m_TestGrab;

			// Nothing changed
			if( m_Grabbing == t_IsGrabbing )
				return;

			m_Grabbing = t_IsGrabbing;
			if( m_Grabbing )
			{
				Grab();
			}
			else
			{
				Release();
			}
		}

		public void Grab()
		{
			DetectGrabbableObjects();
			if( m_InteractableColliders == null || m_InteractableColliders.Count == 0 )
				return;

			// Find most probable interatable
			Vector3 t_PalmPosition = CalculatePalmPosition();
			var t_Info = new GrabbedObject.Info(this);
			foreach( Collider t_Collider in m_InteractableColliders )
			{
				Vector3 t_ClosestPoint = t_Collider.ClosestPoint(t_PalmPosition);
				float t_Distance = Vector3.Distance(t_PalmPosition, t_ClosestPoint);
				if( t_Distance < t_Info.distance )
				{
					t_Info.collider = t_Collider;
					t_Info.nearestColliderPoint = t_ClosestPoint;
					t_Info.distance = t_Distance;
				}
			}

			// No colliders were found
			if( t_Info.collider == null )
				return;

			var t_Grabbable = t_Info.collider.GetComponentInParent<IGrabbable>() as MonoBehaviour;
			if( t_Grabbable == null ) // This should not be happening
				return;

			// Release already held object
			if( m_GrabbedObject != null )
			{
				if( !m_GrabbedObject.RemoveInteractingHand( this ) )
				{
					Debug.LogWarning( "The previously Grabbed Object was not tracking this hand!" );
				}
			}

			// Check if the grabbable already has a grabbed object component, otherwise add one
			m_GrabbedObject = t_Grabbable.GetComponent<GrabbedObject>();
			if( m_GrabbedObject == null )
				m_GrabbedObject = t_Grabbable.gameObject.AddComponent<GrabbedObject>();

			//Calculate info
			t_Info.nearestColliderPoint = t_Grabbable.transform.InverseTransformPoint( t_Info.nearestColliderPoint );
			t_Info.handToObject = transform.InverseTransformPoint( t_Grabbable.transform.position );
			t_Info.objectToHand = t_Grabbable.transform.InverseTransformPoint( transform.position );

			t_Info.objectInteractorForward = t_Grabbable.transform.InverseTransformDirection( transform.forward );
			t_Info.handToObjectRotation = Quaternion.Inverse( transform.rotation ) * t_Grabbable.transform.rotation;
			t_Info.objectToHandRotation = Quaternion.Inverse( t_Info.handToObjectRotation );

			// Add hand info
			if( !m_GrabbedObject.AddInteractingHand( t_Info ) )
				Debug.LogWarning( "The Grabbed Object was already tracking this hand!" );
		}

		public void GrabGrabbable( IGrabbable p_GrabbableObject )
		{
			Vector3 t_PalmPosition = CalculatePalmPosition();
			var t_Info = new GrabbedObject.Info(this);
			var t_Grabbable = p_GrabbableObject as MonoBehaviour;
			if( t_Grabbable == null ) // This should not be happening
				return;

			// Release already held object
			if( m_GrabbedObject != null )
			{
				if( !m_GrabbedObject.RemoveInteractingHand( this ) )
				{
					Debug.LogWarning( "The previously Grabbed Object was not tracking this hand!" );
				}
			}

			// Check if the grabbable already has a grabbed object component, otherwise add one
			m_GrabbedObject = t_Grabbable.GetComponent<GrabbedObject>();
			if( m_GrabbedObject == null )
				m_GrabbedObject = t_Grabbable.gameObject.AddComponent<GrabbedObject>();

			// Set basic info
			t_Info.collider = null;
			t_Info.nearestColliderPoint = Vector3.zero;
			t_Info.distance = Vector3.Distance( t_PalmPosition, m_GrabbedObject.transform.position );

			//Calculate info
			t_Info.nearestColliderPoint = t_Grabbable.transform.InverseTransformPoint( t_Info.nearestColliderPoint );
			t_Info.handToObject = transform.InverseTransformPoint( t_Grabbable.transform.position );
			t_Info.objectToHand = t_Grabbable.transform.InverseTransformPoint( transform.position );

			t_Info.objectInteractorForward = t_Grabbable.transform.InverseTransformDirection( transform.forward );
			t_Info.handToObjectRotation = Quaternion.Inverse( transform.rotation ) * t_Grabbable.transform.rotation;
			t_Info.objectToHandRotation = Quaternion.Inverse( t_Info.handToObjectRotation );

			// Add hand info
			if( !m_GrabbedObject.AddInteractingHand( t_Info ) )
				Debug.LogWarning( "The Grabbed Object was already tracking this hand!" );
		}

		public void Release()
		{
			if( m_GrabbedObject == null )
				return;

			if( !m_GrabbedObject.RemoveInteractingHand( this ) )
				Debug.LogWarning( "The previously Grabbed Object was not tracking this hand!" );

			m_GrabbedObject = null;
		}

		private Vector3 CalculatePalmPosition()
		{
			return transform.position + transform.rotation * m_LocalPalmPosition;
		}

		private Vector3 CalculateGrabPosition()
		{
			Vector3 t_PalmPosition = CalculatePalmPosition();
			Vector3 t_PalmDirection = transform.rotation * m_LocalPalmDirection;
			t_PalmPosition = t_PalmPosition + t_PalmDirection * m_GrabRadius * .55f;

			return t_PalmPosition;
		}

		private void DetectGrabbableObjects()
		{
			Vector3 t_GrabPosition = CalculateGrabPosition();
			var t_Colliders = Physics.OverlapSphere( t_GrabPosition, m_GrabRadius, m_LayerMask );

			m_InteractableColliders.Clear();
			for( int i = 0; i < t_Colliders.Length; i++ )
			{
				var t_Collider = t_Colliders[i];
				var t_Grabbable = t_Collider.GetComponentInParent<IGrabbable>();
				if( t_Grabbable == null )
					continue;
				m_InteractableColliders.Add( t_Collider );
			}
		}

		#endregion

		#region Callbacks

		private void OnLandscapeData( CommunicationHub.Landscape p_Landscape )
		{
			m_GloveID = InteractionHand.GetGloveID( p_Landscape, m_Skeleton.skeletonData.id, m_Side );
			m_GrabGestureID = InteractionHand.GetGestureID( p_Landscape, m_GrabGestureName );
		}

		private void OnGestureData( CoreSDK.GestureStream p_GestureStream )
		{
			for( int i = 0; i < p_GestureStream.gestureProbabilities.Count; i++ )
			{
				var t_Probabilities = p_GestureStream.gestureProbabilities[i];
				if( m_GloveID != t_Probabilities.id || t_Probabilities.isUserID )
					continue;

				m_GestureData = t_Probabilities;
			}
		}

		#endregion

		#region Initialization

		private bool TryGetHandChain( Skeleton p_Skeleton, out Chain p_Chain )
		{
			p_Chain = null;

			// Find hand node
			Node t_HandNode = null;
			for( int i = 0; i < p_Skeleton.skeletonData.nodes.Count; i++ )
			{
				var t_Node = p_Skeleton.skeletonData.nodes[i];
				if( t_Node.unityTransform != transform )
					continue;

				t_HandNode = t_Node;
				break;
			}

			// No hand node found
			if( t_HandNode == null )
				return false;

			// Find hand chain
			for( int i = 0; i < p_Skeleton.skeletonData.chains.Count; i++ )
			{
				var t_Chain = p_Skeleton.skeletonData.chains[i];

				if( t_Chain.type != CoreSDK.ChainType.Hand )
					continue;

				for( int j = 0; j < t_Chain.nodeIds.Count; j++ )
				{
					var t_NodeID = t_Chain.nodeIds[j];
					if( t_NodeID != t_HandNode.id )
						continue;

					p_Chain = t_Chain;
					return true;
				}
			}

			// No chain found
			return false;
		}

		private Vector3 CalculatePalmPosition( Skeleton p_Skeleton, Chain p_HandChain )
		{
			var t_FingerChainIDs = p_HandChain.settings.hand.fingerChainIds;
			int t_FingerPositionsCount = 0;
			Vector3 t_AverageFingerPosition = Vector3.zero;

			for( int i = 0; i < t_FingerChainIDs.Length; i++ )
			{
				var t_FingerChainID = t_FingerChainIDs[i];

				// Not for thumb
				var t_Chain = p_Skeleton.skeletonData.GetChainWithId((uint)t_FingerChainID);
				if( t_Chain == null || t_Chain.type == CoreSDK.ChainType.FingerThumb )
					continue;

				if( !GetFingerPosition( p_Skeleton, t_FingerChainID, out Vector3 t_FingerPosition ) )
					continue;

				t_AverageFingerPosition += t_FingerPosition;
				t_FingerPositionsCount++;
			}

			t_AverageFingerPosition /= t_FingerPositionsCount;

			float t_FingerPref = 0.75f;
			Vector3 t_PalmPosition = t_AverageFingerPosition * t_FingerPref + transform.position * (1f - t_FingerPref);
			// Convert to local
			t_PalmPosition = Quaternion.Inverse( transform.rotation ) * (t_PalmPosition - transform.position);

			return t_PalmPosition;
		}

		private Vector3 CalculatePalmDirection( Skeleton p_Skeleton, Vector3 p_PalmPosition, Chain p_HandChain, CoreSDK.Side p_Side )
		{
			var t_InOrder = new CoreSDK.ChainType[] { CoreSDK.ChainType.FingerIndex, CoreSDK.ChainType.FingerMiddle, CoreSDK.ChainType.FingerRing, CoreSDK.ChainType.FingerPinky };
			var t_InFingerID = GetBestFingerChainID(p_Skeleton, p_HandChain.settings.hand.fingerChainIds, t_InOrder);
			if( !GetFingerPosition( p_Skeleton, t_InFingerID, out Vector3 t_InPosition ) )
				return Vector3.zero;

			var t_OutOrder = t_InOrder.Reverse().ToArray();
			var t_OutFingerID = GetBestFingerChainID(p_Skeleton, p_HandChain.settings.hand.fingerChainIds, t_OutOrder);
			if( !GetFingerPosition( p_Skeleton, t_OutFingerID, out Vector3 t_OutPosition ) )
				return Vector3.zero;

			Vector3 t_ForwardDirection = p_PalmPosition - transform.position;
			Vector3 t_SideDirection = t_InPosition - t_OutPosition;
			Vector3 t_UpVector = Vector3.Cross(t_ForwardDirection, t_SideDirection).normalized;
			t_UpVector *= p_Side == CoreSDK.Side.Left ? -1f : 1f;

			// Convert to local
			t_UpVector = Quaternion.Inverse( transform.rotation ) * t_UpVector;

			return t_UpVector;
		}

		private int GetBestFingerChainID( Skeleton p_Skeleton, int[] p_FingerChains, CoreSDK.ChainType[] p_PreferenceOrder )
		{
			for( int i = 0; i < p_PreferenceOrder.Length; i++ )
			{
				var t_ChainType = p_PreferenceOrder[i];
				for( int j = 0; j < p_FingerChains.Length; j++ )
				{
					int t_FingerChainID = p_FingerChains[j];
					var t_FingerChain = p_Skeleton.skeletonData.GetChainWithId((uint)t_FingerChainID);
					if( t_FingerChain == null || t_FingerChain.type != t_ChainType )
						continue;

					return t_FingerChainID;
				}
			}

			return -1;
		}

		private bool GetFingerPosition( Skeleton p_Skeleton, int p_FingerChainID, out Vector3 p_Position )
		{
			p_Position = Vector3.zero;

			var t_FingerChain = p_Skeleton.skeletonData.GetChainWithId((uint)p_FingerChainID);
			if( t_FingerChain == null )
				return false;

			bool t_HasMetacarpal = t_FingerChain.settings.finger.metacarpalBoneId > 0;
			bool t_IsThumb = t_FingerChain.type == CoreSDK.ChainType.FingerThumb;

			int t_FirstNodeIndex = !t_HasMetacarpal || t_IsThumb  ? 0 : 1;
			var t_FirstNodeID = t_FingerChain.nodeIds[t_FirstNodeIndex];
			var t_FirstNode = p_Skeleton.skeletonData.GetNodeWithId(t_FirstNodeID);
			if( t_FirstNode == null )
				return false;

			p_Position = t_FirstNode.unityTransform.position;
			return true;
		}

		#endregion

		#region Editor

		private void OnDrawGizmos()
		{
			Vector3 t_PalmPosition = CalculatePalmPosition();
			Vector3 t_GrabPosition = CalculateGrabPosition();
			Vector3 t_PalmDirection = transform.rotation * m_LocalPalmDirection;

			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere( t_GrabPosition, m_GrabRadius );

			Gizmos.color = Color.red;
			Gizmos.DrawRay( t_PalmPosition, t_PalmDirection * m_GrabRadius * 1.5f );
		}

		#endregion
	}
}
