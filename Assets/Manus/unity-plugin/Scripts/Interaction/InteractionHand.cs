using System;
using System.Collections;
using System.Collections.Generic;

using Manus.Haptics;
using Manus.Skeletons;

using UnityEngine;

using static Manus.CoreSDK;

namespace Manus.Interaction
{
	/// <summary>
	/// This class is used to detect collisions with the hand.
	/// It is required on a hand in order for CollisionAreas to find a hand.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu( "Manus/Interaction/Hand" )]
	public class InteractionHand : MonoBehaviour
	{
		public class SMRInfo
		{
			public SkinnedMeshRenderer renderer;
			public Material[] originalMats;
			public Material[] shadowMats;
		}

		public class Info
		{
			public InteractionHand handCollision;
			public Collider collider;
		}

		#region Public Properties
		/// <summary>
		/// Returns the hand this module belongs to.
		/// </summary>
		public HandHaptics hand
		{
			get
			{
				return m_Hand;
			}
		}
		#endregion

		//private
		HandHaptics m_Hand;
		Skeletons.Skeleton m_Skeleton;

		GameObject m_VisualHand = null;
		Dictionary<Transform, Transform> m_VisualCloneMap = new Dictionary<Transform, Transform>();
		public float visualHandLerpMultiplier = 50.0f;
		Transform m_VisualHandWrist = null;
		Transform m_ShadowHandWrist = null;


		List<SMRInfo> m_OriginalRenderers = new List<SMRInfo>();

		public HandGrab handGrab { get; private set; }

		public Material shadowHandMaterial = null;
		public float shadowHandMaxOpacity = 0.5f;
		public Vector2 shadowHandOpacityRange = new Vector2(0.05f,0.2f);

		public Transform visualHandRoot
		{
			get
			{
				return m_VisualHandWrist;
			}
		}

		/// <summary>
		/// The start function gets called by Unity and locates the Hand in this component or its parent.
		/// </summary>
		void Start()
		{
			m_Hand = GetComponentInParent<HandHaptics>();
			handGrab = GetComponentInChildren<HandGrab>();
			m_Skeleton = GetComponent<Skeletons.Skeleton>();
		}

		/// <summary>
		/// A function that can be run to find a HandCollision and generate info according to the HandCollision found.
		/// Returns NULL if no HandCollision can be found.
		/// </summary>
		/// <param name="p_Collider"></param>
		/// <returns>Information on the Hand Collision</returns>
		public static Info GetHandColliderInfo( Collider p_Collider )
		{
			var t_Hand = p_Collider.GetComponentInParent<InteractionHand>();
			if( t_Hand == null ) return null;
			return new Info() { handCollision = t_Hand, collider = p_Collider };
		}

		public void Setup()
		{
			m_Hand = GetComponentInParent<HandHaptics>();
			handGrab = GetComponentInChildren<HandGrab>();
			m_Skeleton = GetComponent<Skeletons.Skeleton>();

			if( m_VisualHand != null )
			{
				Destroy( m_VisualHand );
				foreach( var t_Mats in m_OriginalRenderers )
				{
					t_Mats.renderer.materials = t_Mats.originalMats;
					t_Mats.renderer.enabled = true;
				}
			}
			m_OriginalRenderers.Clear();
			m_VisualCloneMap.Clear();
			m_VisualHand = CreateFakeCloneTransforms( ref m_VisualCloneMap, gameObject, gameObject.transform.parent );
			CreateFakeCloneComponents( ref m_VisualCloneMap );
			if( !m_Skeleton ) return;
			for( int i = 0; i < m_Skeleton.skeletonData.chains.Count; i++ )
			{
				if( m_Skeleton.skeletonData.chains[i].type != CoreSDK.ChainType.Hand ) continue;
				if( m_Skeleton.skeletonData.chains[i].nodeIds.Count == 0 ) continue;
				var t_RootID = m_Skeleton.skeletonData.chains[i].nodeIds[0];
				Skeletons.Node t_RootNode = m_Skeleton.skeletonData.nodes.Find((Skeletons.Node p_Node)=> p_Node.id == t_RootID );
				if( t_RootNode == null ) continue;
				m_ShadowHandWrist = t_RootNode.unityTransform;
				m_VisualHandWrist = m_VisualCloneMap[t_RootNode.unityTransform];
			}
		}

		private void Update()
		{

			foreach( var t_Trans in m_VisualCloneMap )
			{
				t_Trans.Value.localPosition = Vector3.Lerp( t_Trans.Value.localPosition, t_Trans.Key.localPosition, Time.deltaTime * visualHandLerpMultiplier );
				t_Trans.Value.localRotation = Quaternion.Slerp( t_Trans.Value.localRotation, t_Trans.Key.localRotation, Time.deltaTime * visualHandLerpMultiplier );
			}

			if( visualHandRoot == null ) return;

			Vector3 t_Pos = visualHandRoot.position;

			if( handGrab && handGrab.grabbedObject )
			{
				handGrab.grabbedObject.GrabbedHandPose( this );
			}

			if( m_ShadowHandWrist )
			{
				Vector3 t_Poz = m_ShadowHandWrist.position - m_VisualHandWrist.position;
				float t_Op = t_Poz.magnitude;
				if( t_Op < shadowHandOpacityRange.x )
				{
					foreach( var t_Rends in m_OriginalRenderers )
					{
						t_Rends.renderer.enabled = false;
					}
					return;
				}
				float t_OpRange = (shadowHandOpacityRange.y - shadowHandOpacityRange.x);
				if( t_OpRange > 0.00001 )
				{
					t_Op -= shadowHandOpacityRange.x;
					t_Op /= t_OpRange;
					if( t_Op > 1.0f ) t_Op = 1.0f;
				}
				else
				{
					t_Op = 1.0f;
				}
				t_Op *= shadowHandMaxOpacity;
				foreach( var t_Rends in m_OriginalRenderers )
				{
					t_Rends.renderer.enabled = true;
					foreach( var t_Mats in t_Rends.shadowMats )
					{
						var t_Color = t_Mats.GetColor( "_Color" );
						t_Color.a = t_Op;
						t_Mats.SetColor( "_Color", t_Color );

					}
				}
			}
		}

		GameObject CreateFakeCloneTransforms( ref Dictionary<Transform, Transform> p_CloneMap, GameObject p_Orig, Transform p_Parent )
		{
			var t_Obj = new GameObject( p_Orig.name + "_Fake" );
			var t_ObjTrans = t_Obj.transform;
			p_CloneMap[p_Orig.transform] = t_ObjTrans;
			t_ObjTrans.parent = p_Parent.transform;
			t_ObjTrans.localPosition = p_Orig.transform.localPosition;
			t_ObjTrans.localRotation = p_Orig.transform.localRotation;
			t_ObjTrans.localScale = p_Orig.transform.localScale;

			for( int i = 0; i < p_Orig.transform.childCount; i++ )
			{
				var t_Trans = p_Orig.transform.GetChild( i );
				CreateFakeCloneTransforms( ref p_CloneMap, t_Trans.gameObject, t_ObjTrans );
			}

			return t_Obj;
		}

		void CreateFakeCloneComponents( ref Dictionary<Transform, Transform> p_CloneMap )
		{
			foreach( var t_Trans in p_CloneMap )
			{
				var t_OSMR = t_Trans.Key.gameObject.GetComponent<SkinnedMeshRenderer>();
				if( t_OSMR != null )
				{
					var t_SMR = t_Trans.Value.gameObject.AddComponent<SkinnedMeshRenderer>();
					t_SMR.enabled = true;
					t_SMR.localBounds = t_OSMR.localBounds;
					t_SMR.quality = t_OSMR.quality;
					t_SMR.updateWhenOffscreen = t_OSMR.updateWhenOffscreen;
					t_SMR.sharedMesh = t_OSMR.sharedMesh;
					t_SMR.rootBone = p_CloneMap[t_OSMR.rootBone];
					List<Transform> t_Bones = new List<Transform>();
					for( int b = 0; b < t_OSMR.bones.Length; b++ )
					{
						t_Bones.Add( p_CloneMap[t_OSMR.bones[b]] );
					}
					t_SMR.bones = t_Bones.ToArray();
					t_SMR.materials = t_OSMR.materials;

					SMRInfo t_SMRInfo = new SMRInfo();
					t_SMRInfo.renderer = t_OSMR;
					t_SMRInfo.originalMats = t_OSMR.sharedMaterials;
					Material[] t_Mats = new Material[t_OSMR.sharedMaterials.Length];
					for( int m = 0; m < t_Mats.Length; m++ )
					{
						t_Mats[m] = shadowHandMaterial;
					}
					t_OSMR.sharedMaterials = t_Mats;
					t_SMRInfo.shadowMats = t_OSMR.materials;
					t_OSMR.enabled = false;
					m_OriginalRenderers.Add( t_SMRInfo );
				}

			}
		}



		#region Glove ID

		static public uint GetGloveID( CommunicationHub.Landscape p_Landscape, uint p_SklID, Side p_Side )
		{
			if( !GetSkeletonLandscapeData( p_SklID, p_Landscape, out CoreSDK.SkeletonLandscapeData t_SkeletonLandscapeData ) )
				return 0;

			uint t_UserID = t_SkeletonLandscapeData.userId;
			if( !GetUserLandscapeData( t_UserID, p_Landscape, out CoreSDK.UserLandscapeData t_UserLandscapeData ) )
				return 0;

			return GetGloveIDFromUserLandscapeData( t_UserLandscapeData, p_Side );
		}

		static bool GetSkeletonLandscapeData( uint p_SkeletonID, CommunicationHub.Landscape p_Landscape, out CoreSDK.SkeletonLandscapeData p_SkeletonLandscapeData )
		{
			p_SkeletonLandscapeData = new CoreSDK.SkeletonLandscapeData();

			// Get skeleton landscape data
			for( int i = 0; i < p_Landscape.skeletons.Count; i++ )
			{
				var t_Skeleton = p_Landscape.skeletons[i];
				if( t_Skeleton.id != p_SkeletonID )
					continue;

				p_SkeletonLandscapeData = t_Skeleton;
				return true;
			}

			return false;
		}

		static bool GetUserLandscapeData( uint p_UserID, CommunicationHub.Landscape p_Landscape, out CoreSDK.UserLandscapeData p_UserLandscapeData )
		{
			p_UserLandscapeData = new CoreSDK.UserLandscapeData();

			// Get user landscape data
			for( int i = 0; i < p_Landscape.users.Count; i++ )
			{
				var t_User = p_Landscape.users[i];
				if( t_User.id != p_UserID )
					continue;

				p_UserLandscapeData = t_User;
				return true;
			}

			return false;
		}

		static uint GetGloveIDFromUserLandscapeData( CoreSDK.UserLandscapeData p_UserLandscapeData, Side p_Side )
		{
			switch( p_Side )
			{
				case CoreSDK.Side.Left:
					return p_UserLandscapeData.leftGloveID;
				case CoreSDK.Side.Right:
					return p_UserLandscapeData.rightGloveID;
				default:
					return 0;
			}
		}

		static public uint GetGestureID( CommunicationHub.Landscape p_Landscape, string p_Name )
		{
			for( int i = 0; i < p_Landscape.gestures.Count; i++ )
			{
				var t_Gesture = p_Landscape.gestures[i];
				if( t_Gesture.name != p_Name )
					continue;

				return t_Gesture.id;
			}
			return 0;
		}

		#endregion
	}
}
