using System.Collections;
using System.Collections.Generic;

using Manus.Skeletons;

using UnityEngine;

namespace Manus.Interaction
{
	public class ColliderGenerator : MonoBehaviour
	{
		[HideInInspector,SerializeField] private List<GeneratedCollider> m_GeneratedColliders = new List<GeneratedCollider>();

		private Skeleton m_Skeleton;

		[Header("Colliders")]
		[SerializeField] private Vector3 m_HandCenter = new Vector3(-0.01f,0.0f,0.0f);
		[SerializeField] private Vector3 m_HandSize = new Vector3(0.07f,0.05f,0.08f);

		[SerializeField, Range(0.001f, 0.5f)] private float m_FingerThicknessStart = 0.02f;
		[SerializeField, Range(0.001f, 0.5f)] private float m_FingerThicknessEnd = 0.01f;
		[SerializeField] private ColliderDirection m_ColliderDirection;
		[SerializeField] private bool m_InverseDirection;
		
		private void OnValidate()
		{
			if( m_Skeleton == null )
			{
				m_Skeleton = GetComponentInParent<Skeleton>();
			
				if( m_Skeleton == null )
					return;
			}

			m_GeneratedColliders.RemoveAll( ( GeneratedCollider p_Col ) =>
			{
				if( p_Col == null ) return true;
				if( p_Col.collider == null ) return true;
				return false;
			} );

			GenerateColliders();
		}

		//private void ResetColliders()
		//{
		//	for( int i = 0; i < m_GeneratedColliders.Count; i++ )
		//	{
		//		var t_GeneratedCollider = m_GeneratedColliders[i];
		//		DestroyImmediate( t_GeneratedCollider.collider );
		//	}

		//	m_GeneratedColliders.Clear();
		//}

		private void GenerateColliders()
		{
			//ResetColliders();

			for( int i = 0; i < m_Skeleton.skeletonData.chains.Count; i++ )
			{
				var t_Chain = m_Skeleton.skeletonData.chains[i];
				if( t_Chain.type != CoreSDK.ChainType.Hand )
					continue;

				GenerateHandColliders( t_Chain );
			}
		}

		private void GenerateHandColliders(Chain p_HandChain)
		{
			var t_FingerChainIDs = p_HandChain.settings.hand.fingerChainIds;
			for( int i = 0; i < t_FingerChainIDs.Length; i++ )
			{
				var t_FingerChainID = t_FingerChainIDs[i];
				var t_FingerChain = m_Skeleton.skeletonData.GetChainWithId((uint)t_FingerChainID);
				if (t_FingerChain == null)
					continue;

				GenerateFingerColliders( t_FingerChain );
			}

			var t_Node1 = m_Skeleton.skeletonData.GetNodeWithId(p_HandChain.nodeIds[0]);
			GeneratedCollider t_GeneratedCollider;
			if( !TryGetColliderForNode( t_Node1.id, out t_GeneratedCollider ) )
			{
				var t_NewCollider = t_Node1.unityTransform.gameObject.AddComponent<BoxCollider>();
				t_GeneratedCollider = new GeneratedCollider
				{
					nodeID = t_Node1.id,
					collider = t_NewCollider
				};
				m_GeneratedColliders.Add( t_GeneratedCollider );
			}

			var t_BoxCollider = (BoxCollider)t_GeneratedCollider.collider;
			t_BoxCollider.center = m_HandCenter;
			t_BoxCollider.size = m_HandSize;
			t_BoxCollider.isTrigger = true;
		}

		private void GenerateFingerColliders(Chain p_FingerChain)
		{
			float t_CurrentThickness = m_FingerThicknessStart;
			for( int i = 0; i < p_FingerChain.nodeIds.Count - 1; i++ )
			{
				t_CurrentThickness = m_FingerThicknessStart + i * ((m_FingerThicknessEnd - m_FingerThicknessStart) / (p_FingerChain.nodeIds.Count - 1));

				var t_Node1 = m_Skeleton.skeletonData.GetNodeWithId(p_FingerChain.nodeIds[i]);
				var t_Node2 = m_Skeleton.skeletonData.GetNodeWithId(p_FingerChain.nodeIds[i + 1]);

				GeneratedCollider t_GeneratedCollider;
				if(!TryGetColliderForNode( t_Node1.id, out t_GeneratedCollider ) )
				{
					var t_NewCollider = t_Node1.unityTransform.gameObject.AddComponent<CapsuleCollider>();
					t_GeneratedCollider = new GeneratedCollider
					{
						nodeID = t_Node1.id,
						collider = t_NewCollider
					};
					m_GeneratedColliders.Add( t_GeneratedCollider );
				}

				var t_CapsuleCollider = (CapsuleCollider)t_GeneratedCollider.collider;
				t_CapsuleCollider.height = Vector3.Distance( t_Node1.unityTransform.position, t_Node2.unityTransform.position );

				float t_HeightAdjustment = t_CurrentThickness;
				float t_OffsetAdjustment = 0.0f;
				if( p_FingerChain.nodeIds.Count - 2 == i )
				{
					t_HeightAdjustment = t_CurrentThickness * 0.5f;
					t_OffsetAdjustment = -t_CurrentThickness * 0.5f;
				}

				Vector3 t_Center = Vector3.zero;
				float t_Offset = (t_CapsuleCollider.height + t_OffsetAdjustment) / 2f;

				switch( m_ColliderDirection )
				{
					case ColliderDirection.X:
						t_Center.x = t_Offset;
						break;
					case ColliderDirection.Y:
						t_Center.y = t_Offset;
						break;
					case ColliderDirection.Z:
						t_Center.z = t_Offset;
						break;
				}
				if( m_InverseDirection )
					t_Center *= -1;

				
				t_CapsuleCollider.height = t_CapsuleCollider.height + t_HeightAdjustment;
				t_CapsuleCollider.center = t_Center;
				t_CapsuleCollider.direction = (int)m_ColliderDirection;
				t_CapsuleCollider.radius = t_CurrentThickness / 2f;
				t_CapsuleCollider.isTrigger = true;
			}
		}

		private bool TryGetColliderForNode(uint p_NodeID, out GeneratedCollider p_Collider)
		{
			p_Collider = null;
			for( int i = 0; i < m_GeneratedColliders.Count; i++ )
			{
				var t_Collider = m_GeneratedColliders[i];
				if( t_Collider == null ) continue;
				if( t_Collider.collider == null ) continue;
				if( t_Collider.nodeID == p_NodeID )
				{
					p_Collider = t_Collider;
					return true;
				}
			}

			return false;
		}

		public enum ColliderDirection
		{
			X = 0,
			Y = 1,
			Z = 2,
		}

		[System.Serializable]
		public class GeneratedCollider
		{
			public uint nodeID;
			public Collider collider;
		}
	}
}
