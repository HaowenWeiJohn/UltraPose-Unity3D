using System.Collections.Generic;

using Manus.Haptics;
using Manus.Utility;

using UnityEngine;

namespace Manus.Skeletons
{
	/// <summary>
	/// Skeleton handles all animation based on Polygon data from Manus Core.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu( "Manus/Skeletons/Skeleton" )]
	public class Skeleton : MonoBehaviour
	{
		public SkeletonData skeletonData;
		public bool isLocalPlayer = true;
		public bool enableHaptics = false;
		public delegate void OnDataApplied();
		public OnDataApplied onDataApplied;
		public bool debugAnimatedSkeleton;
		public bool debugColliders;
		public uint? sklSetupIdx;
		public uint? sessionId;

		private bool m_Paused = false;
		private CoreSDK.Skeleton m_LastSkeletonData;

		private Dictionary<uint, uint> m_ScaledParentNodeID = new Dictionary<uint, uint>();
		private List<HandHaptics> m_HandsList = new List<HandHaptics>();

		/// <summary>
		/// Ensure hands are ready for use and send them to Manus Core so it can be animated.
		/// </summary>
		private void OnEnable()
		{
			SetupHands();
			SendSkeleton();
		}

		/// <summary>
		/// Unload the skeleton once it is disabled.
		/// </summary>
		private void OnDisable()
		{
			ManusManager.communicationHub?.UnloadSkeleton( this );
		}

		/// <summary>
		/// Send skeleton to Manus Core and setup skeleton to allow for scaling.
		/// </summary>
		public void SendSkeleton()
		{
			if( !Application.isPlaying )
			{
				Debug.LogWarning( $"MANUS-WARN: Tried to send skeleton when not in playmode." );
				return;
			}

			ManusManager.communicationHub?.SetupSkeleton( this );
			GenerateScaleBones();
			var t_IH = GetComponent<Interaction.InteractionHand>();
			if(t_IH != null )
			{
				t_IH.Setup();
			}
		}

		/// <summary>
		/// Update the boolean associated to the haptics enabled based on the input from the user in the GUI.
		/// </summary>
		public void UpdateEnableHaptics()
		{
			// for all hand of the skeleton update bool with enableHaptics
			foreach( var t_Hand in m_HandsList )
			{
				t_Hand.UpdateHapticsEnabled( enableHaptics );
			}
		}

		/// <summary>
		/// Send skeleton to Manus Development Dashboard.
		/// </summary>
		public void SendSkeletonToDevTools()
		{
			// update the skeleton name based on UI input
			skeletonData.name = this.gameObject.name;
			ManusManager.communicationHub?.SaveTemporarySkeleton( this, SetupMeshes(), false );
		}

		/// <summary>
		/// Setup and create chains on skeleton and update their names.
		/// </summary>
		public bool AllocateChains()
		{
			if( ManusManager.communicationHub.AllocateChains( this ) )
			{
				UpdateNames();
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Generate rotation offsets for skeleton
		/// </summary>
		[ContextMenu("Generate rotation offsets")]
		public bool PrepareSkeleton()
		{
			return ManusManager.communicationHub.PrepareSkeleton( this );
		}

		/// <summary>
		/// Load skeleton from Manus Development Dashboard.
		/// </summary>
		public void LoadSkeleton()
		{
			if( sklSetupIdx.HasValue )
			{
				ManusManager.communicationHub?.LoadSkeletonFromTool( this, sklSetupIdx.Value );
			}
		}

		/// <summary>
		/// Update names of skeleton, chains and nodes.
		/// </summary>
		protected virtual void UpdateNames()
		{
			if( skeletonData == null ) return;

			skeletonData.name = this.gameObject.name;

			UpdateNodesAndChainsNames();
		}

		/// <summary>
		/// Update all names of chains and nodes.
		/// </summary>
		protected void UpdateNodesAndChainsNames()
		{
			foreach( var t_Node in skeletonData.nodes )
			{
				t_Node.UpdateName();
			}

			foreach( var t_Chain in skeletonData.chains )
			{
				t_Chain.UpdateName();
			}
		}

		/// <summary>
		/// Setup hands by adding missing hand and finger haptics components.
		/// </summary>
		public void SetupHands()
		{
			m_HandsList = new List<HandHaptics>();
			foreach( var t_Chain in skeletonData.chains )
			{
				if( t_Chain.type == CoreSDK.ChainType.Hand )
				{
					//Save data side
					CoreSDK.Side t_HandSide = CoreSDK.Side.Invalid;
					switch( t_Chain.dataSide )
					{
						case CoreSDK.Side.Left:
							t_HandSide = CoreSDK.Side.Left;
							break;
						case CoreSDK.Side.Right:
							t_HandSide = CoreSDK.Side.Right;
							break;
					}

					//Get first node and find hand
					var t_HandNode = skeletonData.GetNodeWithId( t_Chain.nodeIds[0] );

					HandHaptics t_Hand = t_HandNode.unityTransform.GetComponent<HandHaptics>();

					//Add hand script if it does not exist
					if( t_Hand == null )
					{
						t_Hand = t_HandNode.unityTransform.gameObject.AddComponent<HandHaptics>();
					}

					// add it to the HandList, this will be used for setting up the FingerHaptics
					m_HandsList.Add( t_Hand );
					//Adjust hand script settings
					t_Hand.SetupHand( t_HandSide, enableHaptics );
				}
			}

			// Dont add haptics if there is no hand chain
			if( m_HandsList.Count == 0 )
				return;

			foreach( var t_Chain in skeletonData.chains )
			{
				//Create finger haptics 
				if( (t_Chain.type != CoreSDK.ChainType.FingerThumb &&
					 t_Chain.type != CoreSDK.ChainType.FingerIndex &&
					 t_Chain.type != CoreSDK.ChainType.FingerMiddle &&
					 t_Chain.type != CoreSDK.ChainType.FingerRing &&
					 t_Chain.type != CoreSDK.ChainType.FingerPinky) ) continue;

				//Chain type to finger type
				FingerType t_FingerType = FingerType.Invalid;
				switch( t_Chain.type )
				{
					case CoreSDK.ChainType.FingerThumb:
						t_FingerType = FingerType.Thumb;
						break;
					case CoreSDK.ChainType.FingerIndex:
						t_FingerType = FingerType.Index;
						break;
					case CoreSDK.ChainType.FingerMiddle:
						t_FingerType = FingerType.Middle;
						break;
					case CoreSDK.ChainType.FingerRing:
						t_FingerType = FingerType.Ring;
						break;
					case CoreSDK.ChainType.FingerPinky:
						t_FingerType = FingerType.Pinky;
						break;
				}

				//Get final node in chain
				var t_FingerTipNode = skeletonData.GetNodeWithId( t_Chain.nodeIds[t_Chain.nodeIds.Count - 1] );
				if( t_FingerTipNode == null ) continue;

				Haptics.FingerHaptics t_FingerHaptics = t_FingerTipNode.unityTransform.GetComponent<Haptics.FingerHaptics>();
				if( t_FingerHaptics == null )
				{
					t_FingerHaptics = t_FingerTipNode.unityTransform.gameObject.AddComponent<Haptics.FingerHaptics>();
				}

				t_FingerHaptics.SetupFinger( t_FingerType );
			}

			// use hand list to setup finger haptics
			foreach( var t_Hand in m_HandsList )
			{
				t_Hand.SetupFingerHaptics();
			}
		}

		/// <summary>
		/// Create nodes from mesh transforms and add them to the skeleton. 
		/// </summary>
		public void SetupNodes()
		{
			OnNodeSetup();
			var t_NewSkeleton = new SkeletonData();
			t_NewSkeleton.id = skeletonData.id;
			t_NewSkeleton.type = skeletonData.type;
			t_NewSkeleton.settings = skeletonData.settings;
			t_NewSkeleton.chains = skeletonData.chains;

			var t_NodeSettings = new Dictionary<Transform, CoreSDK.NodeSettings>();
			foreach( var t_Node in skeletonData.nodes )
			{
				if( t_Node.settings.usedSettings != CoreSDK.NodeSettingsFlag.None )
				{
					t_NodeSettings.Add( t_Node.unityTransform, t_Node.settings );
				}
			}

			// traverse the whole hierarchy and add them as nodes
			uint t_NodeIndex = 0;
			AddNode( transform, t_NewSkeleton, ref t_NodeIndex );

			foreach( var t_NodeTransformWithSettings in t_NodeSettings.Keys )
			{
				var t_OldNode = t_NewSkeleton.GetNodeWithUnityTransform( t_NodeTransformWithSettings );
				if( t_OldNode != null )
					t_OldNode.settings = t_NodeSettings[t_NodeTransformWithSettings];
			}

			skeletonData = t_NewSkeleton;
			UpdateNames();
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty( gameObject );
#endif
		}

		/// <summary>
		/// Find and setup all meshes in the skeleton
		/// </summary>
		public List<MeshSetup> SetupMeshes()
		{
			var t_MeshSetups = new List<MeshSetup>();

			var t_SkinnedMeshes = GetComponentsInChildren<SkinnedMeshRenderer>();
			for( int i = 0; i < t_SkinnedMeshes.Length; i++ )
			{
				var t_SkinnedMesh = t_SkinnedMeshes[i];
				if( !t_SkinnedMesh.sharedMesh.isReadable )
				{
					Debug.LogWarning( "Mesh not sent because the mesh is not marked as readable" );
					continue;
				}

				Mesh t_BakedMesh = new Mesh();
				t_SkinnedMesh.BakeMesh( t_BakedMesh );

				var t_BoneWeights = t_SkinnedMesh.sharedMesh.boneWeights;
				var t_MeshSetup = SetupMesh( skeletonData, t_BakedMesh, t_SkinnedMesh.transform, t_SkinnedMesh.bones, t_BoneWeights );
				if( t_MeshSetup != null )
					t_MeshSetups.Add( t_MeshSetup );
			}

			var t_StaticMeshes = GetComponentsInChildren<MeshFilter>();
			for( int i = 0; i < t_StaticMeshes.Length; i++ )
			{
				var t_Mesh = t_StaticMeshes[i];
				if( !t_Mesh.sharedMesh.isReadable )
				{
					Debug.LogWarning( "Mesh not sent because the mesh is not marked as readable" );
					continue;
				}

				var t_MeshSetup = SetupMesh( skeletonData, t_Mesh.sharedMesh, t_Mesh.transform, null, null );
				if( t_MeshSetup != null )
					t_MeshSetups.Add( t_MeshSetup );
			}

			return t_MeshSetups;
		}

		/// <summary>
		/// Convert a single mesh to a mesh setup
		/// </summary>
		/// <param name="p_Mesh">Mesh to convert</param>
		/// <param name="p_Transform">Parent object of the mesh</param>
		/// <param name="p_Bones">Bones of the skeleton (only for skinned meshes)</param>
		/// <returns>Mesh setup</returns>
		private MeshSetup SetupMesh( SkeletonData p_Skeleton, Mesh p_Mesh, Transform p_Transform, Transform[] p_Bones, BoneWeight[] p_BoneWeights )
		{
			var t_Node = p_Skeleton.GetNodeWithName( p_Transform.name );
			if( t_Node == null )
				return null;

			MeshSetup t_New = new MeshSetup( t_Node.id );

			int t_VertexCount = p_Mesh.vertexCount;
			var t_Vertices = p_Mesh.vertices;
			//
			t_New.vertices.Capacity = t_VertexCount;
			for( int i = 0; i < t_VertexCount; i++ )
			{
				var t_Vertex = new CoreSDK.Vertex();
				t_Vertex.position = t_Vertices[i].ToManus();

				if( p_Bones != null && p_BoneWeights != null )
				{
					var t_Weights = SetupVertexWeights( p_Skeleton, p_BoneWeights[i], p_Bones );
					t_Vertex.weightsCount = (uint)t_Weights.Length;
					t_Vertex.weights = t_Weights;
				}

				t_New.vertices.Add( t_Vertex );
			}

			int t_TriangleCount = p_Mesh.triangles.Length / 3;
			var t_Triangles = p_Mesh.triangles;
			t_New.triangles.Capacity = t_TriangleCount;
			for( int i = 0; i < t_TriangleCount; i++ )
			{
				var t_Triangle = new CoreSDK.Triangle();
				t_Triangle.vertexIndex1 = t_Triangles[i * 3];
				t_Triangle.vertexIndex2 = t_Triangles[i * 3 + 1];
				t_Triangle.vertexIndex3 = t_Triangles[i * 3 + 2];

				t_New.triangles.Add( t_Triangle );
			}

			return t_New;
		}

		/// <summary>
		/// Get the vertex weights of the mesh
		/// </summary>
		/// <param name="p_Weight">Boneweight of the mesh</param>
		/// <param name="p_Bones">Skeleton of the mesh</param>
		/// <returns>Bone weights</returns>
		private CoreSDK.Weight[] SetupVertexWeights( SkeletonData p_Skeleton, BoneWeight p_Weight, Transform[] p_Bones )
		{
			var t_WeightSetups = new List<CoreSDK.Weight>();

			// Weight 0
			if( p_Weight.weight0 != 0 )
			{
				var t_WeightNode = p_Skeleton.GetNodeWithName( p_Bones[p_Weight.boneIndex0].name );
				if( t_WeightNode == null )
					return t_WeightSetups.ToArray();

				t_WeightSetups.Add( new CoreSDK.Weight( t_WeightNode.id, p_Weight.weight0 ) );
			}

			// Weight 1
			if( p_Weight.weight1 != 0 )
			{
				var t_WeightNode = p_Skeleton.GetNodeWithName( p_Bones[p_Weight.boneIndex1].name );
				if( t_WeightNode == null )
					return t_WeightSetups.ToArray();

				t_WeightSetups.Add( new CoreSDK.Weight( t_WeightNode.id, p_Weight.weight1 ) );
			}

			// Weight 2
			if( p_Weight.weight2 != 0 )
			{
				var t_WeightNode = p_Skeleton.GetNodeWithName( p_Bones[p_Weight.boneIndex2].name );
				if( t_WeightNode == null )
					return t_WeightSetups.ToArray();

				t_WeightSetups.Add( new CoreSDK.Weight( t_WeightNode.id, p_Weight.weight2 ) );
			}

			// Weight 3
			if( p_Weight.weight3 != 0 )
			{
				var t_WeightNode = p_Skeleton.GetNodeWithName( p_Bones[p_Weight.boneIndex3].name );
				if( t_WeightNode == null )
					return t_WeightSetups.ToArray();

				t_WeightSetups.Add( new CoreSDK.Weight( t_WeightNode.id, p_Weight.weight3 ) );
			}

			return t_WeightSetups.ToArray();
		}

		/// <summary>
		/// Redo node transform values.
		/// </summary>
		[ContextMenu( "Re-setup nodes transform values" )]
		public void ReSetupNodeTransforms()
		{
			OnNodeSetup();

			foreach( var t_Node in skeletonData.nodes )
			{
				t_Node.transform = new TransformValues
				{
					position = t_Node.unityTransform.localPosition,
					rotation = t_Node.unityTransform.localRotation,
					scale = t_Node.unityTransform.localScale
				}; //local
			}

#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty( gameObject );
#endif
		}

		/// <summary>
		/// Add node to skeleton data and add its child nodes.
		/// </summary>
		/// <param name="p_Transform">Transform of the node</param>
		/// <param name="p_Skeleton">Skeleton to add node to</param>
		/// <param name="t_Index">New node index</param>
		/// <returns>Node ID</returns>
		private uint AddNode( Transform p_Transform, SkeletonData p_Skeleton, ref uint t_Index )
		{
			var t_Node = new Node();

			// Copy all transform values over to the node
			t_Node.id = t_Index;
			t_Node.nodeName = p_Transform.name;
			t_Node.transform = new TransformValues
			{
				position = p_Transform.localPosition,
				rotation = p_Transform.localRotation,
				scale = p_Transform.localScale
			};
			t_Node.type = p_Transform.GetComponent<Renderer>() != null ? CoreSDK.NodeType.Mesh : CoreSDK.NodeType.Joint;
			t_Node.settings = new CoreSDK.NodeSettings();
			t_Node.parentID = 0;
			t_Node.unityTransform = p_Transform;

			foreach( var t_SklNode in p_Skeleton.nodes )
			{
				if( p_Transform.parent == t_SklNode.unityTransform )
				{
					t_Node.parentID = t_SklNode.id;
					break;
				}
			}

			p_Skeleton.nodes.Add( t_Node );
			//Attach an empty gameObject to every node
			OnNodeCreated( t_Node );

			t_Index++;

			// Add all child nodes
			for( int i = 0; i < p_Transform.childCount; i++ )
			{
				AddNode( p_Transform.GetChild( i ), p_Skeleton, ref t_Index );
			}

			return t_Node.id;
		}

		/// <summary>
		/// Apply Manus Core skeleton data to plugin skeleton.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton data to apply</param>
		public void ApplyData( CoreSDK.Skeleton p_Skeleton, CoreSDK.ManusTimestamp? p_Time = null )
		{
			if( p_Skeleton.nodes == null )
				return;

			m_LastSkeletonData = p_Skeleton;

			if(p_Time.HasValue)
				skeletonData.lastTimestamp = p_Time.Value;

			if( m_Paused )
				return;

			foreach( var t_TargetNode in p_Skeleton.nodes )
			{
				var t_Node = skeletonData.GetNodeWithId( t_TargetNode.id );
				Vector3 t_ParentScale = Vector3.one;

				if( m_ScaledParentNodeID.ContainsKey( t_Node.id ) )
				{
					t_ParentScale = skeletonData.GetNodeWithId( m_ScaledParentNodeID[t_Node.id] ).unityTransform.localScale;
					t_Node.unityTransform.parent.localScale = new Vector3( 1f / t_ParentScale.x, 1f / t_ParentScale.y, 1f / t_ParentScale.z );
					t_Node.unityTransform.parent.localPosition = t_TargetNode.transform.position.FromManus();
				}
				else
				{
					t_Node.unityTransform.localPosition = t_TargetNode.transform.position.FromManus();
				}

				t_Node.unityTransform.localRotation = t_TargetNode.transform.rotation.FromManus();

				Vector3 t_Scale = t_TargetNode.transform.scale.FromManus();
				Vector3 t_NewScale = new Vector3( t_Scale.x * t_ParentScale.x, t_Scale.y * t_ParentScale.y, t_Scale.z * t_ParentScale.z );
				t_Node.unityTransform.localScale = t_NewScale;
			}

			onDataApplied?.Invoke();
		}

		/// <summary>
		/// Reset to original starting pose.
		/// </summary>
		public void ResetToBindPose( bool p_AlsoResetScale = false )
		{
			m_Paused = true;
			foreach( var t_TargetNode in skeletonData.nodes )
			{
				var t_Node = skeletonData.GetNodeWithId( t_TargetNode.id );
				var t_ParentNode = skeletonData.GetNodeWithId( t_Node.parentID );

				if( !IsNodeAnimated( t_Node.id ) )
					continue;

				Vector3 t_ParentScale = Vector3.one;

				if( t_Node.id != t_ParentNode.id && IsNodeAnimated( t_ParentNode.id ) )
				{
					if( p_AlsoResetScale )
					{
						t_ParentScale = t_ParentNode.unityTransform.localScale;
						t_Node.unityTransform.parent.localScale = new Vector3( 1f / t_ParentScale.x, 1f / t_ParentScale.y, 1f / t_ParentScale.z );
					}

					t_Node.unityTransform.parent.localPosition = t_TargetNode.transform.position;
				}
				else
				{
					t_Node.unityTransform.localPosition = t_TargetNode.transform.position;
				}

				t_Node.unityTransform.localRotation = t_TargetNode.transform.rotation;

				if( p_AlsoResetScale )
				{
					Vector3 t_Scale = t_TargetNode.transform.scale;
					Vector3 t_NewScale = new Vector3( t_Scale.x * t_ParentScale.x, t_Scale.y * t_ParentScale.y, t_Scale.z * t_ParentScale.z );
					t_Node.unityTransform.localScale = t_NewScale;
				}
			}
		}

		/// <summary>
		/// Continue animating after resetting it to the bindpose
		/// </summary>
		public void ContinueAnimating()
		{
			m_Paused = false;

			if( m_LastSkeletonData.id != skeletonData.id || m_LastSkeletonData.nodes == null )
				return;

			ApplyData( m_LastSkeletonData );
		}

		/// <summary>
		/// Generate transforms to allow for scaling skeletons.
		/// </summary>
		private void GenerateScaleBones()
		{
			if( m_ScaledParentNodeID?.Count != 0 )
				return;

			bool t_FoundInBetweenNodes = false;
			m_ScaledParentNodeID = new Dictionary<uint, uint>();
			// For each node that is in a chain, add an antiscale parent node to revert the parents 
			for( int i = 0; i < skeletonData.nodes.Count; i++ )
			{
				var t_Node = skeletonData.nodes[i];
				bool t_FoundInBetweenNodesInChain = false;

				if( IsNodeAnimated( t_Node.id ) &&
					t_Node.id != t_Node.parentID &&
					!t_Node.unityTransform.parent.name.Contains( "_antiScale" ) )
				{
					// Find parent
					Node t_ParentNode = skeletonData.GetNodeWithId( t_Node.parentID );
					while( true )
					{
						if( IsNodeAnimated( t_ParentNode.id ) )
							break;

						if( t_ParentNode.id == t_ParentNode.parentID )
						{
							t_ParentNode = null;
							break;
						}

						t_FoundInBetweenNodesInChain = true;
						t_ParentNode = skeletonData.GetNodeWithId( t_ParentNode.parentID );
					}

					if( t_ParentNode == null )
						continue;

					if( t_FoundInBetweenNodesInChain )
					{
						t_FoundInBetweenNodes = true;
						continue;
					}

					GameObject t_AntiScaleNode = new GameObject( $"{t_Node.nodeName}_antiScale" );

					t_AntiScaleNode.transform.SetParent( skeletonData.GetNodeWithId( t_Node.parentID ).unityTransform );
					t_AntiScaleNode.transform.position = t_Node.unityTransform.position;
					t_AntiScaleNode.transform.rotation = t_ParentNode.unityTransform.rotation;
					t_AntiScaleNode.transform.localScale = Vector3.one;

					t_Node.unityTransform.SetParent( t_AntiScaleNode.transform );
					m_ScaledParentNodeID.Add( t_Node.id, t_ParentNode.id );
				}
			}

			if( t_FoundInBetweenNodes )
				Debug.LogWarning( "Found chains that have inbetween nodes, this is not supported for scaling" );
		}

		/// <summary>
		/// Check whether node is getting animated.
		/// </summary>
		/// <param name="p_NodeId">Node ID</param>
		/// <returns>Whether node is animated</returns>
		private bool IsNodeAnimated( uint p_NodeId )
		{
			foreach( var t_Chain in skeletonData.chains )
			{
				// Node in chain
				foreach( var t_ChainNodeId in t_Chain.nodeIds )
				{
					if( p_NodeId == t_ChainNodeId )
						return true;
				}

				// Node as metacarpal setting
				if( (t_Chain.appliedDataType == CoreSDK.ChainType.FingerIndex ||
					 t_Chain.appliedDataType == CoreSDK.ChainType.FingerMiddle ||
					 t_Chain.appliedDataType == CoreSDK.ChainType.FingerRing ||
					 t_Chain.appliedDataType == CoreSDK.ChainType.FingerPinky) &&
					t_Chain.settings.finger.metacarpalBoneId == p_NodeId )
					return true;
			}

			return false;
		}

#if UNITY_EDITOR
		/// <summary>
		/// Draw debug gizmos.
		/// </summary>
		private void OnDrawGizmos()
		{
			if( debugAnimatedSkeleton )
			{
				Gizmos.color = Color.white;
				UnityEditor.Handles.color = Color.white;

				if( skeletonData.nodes.Count > 0 )
				{
					foreach( var t_Node in skeletonData.nodes )
					{
						DrawHierarchy( t_Node );
					}
				}
			}

			if( skeletonData == null || skeletonData.colliders == null ) return;

			if( skeletonData.colliders.Count > 0 && debugColliders )
			{
				foreach( var t_Collider in skeletonData.colliders )
				{
					Gizmos.color = new Color( 0, 1, .4f );
					DrawCollider( t_Collider );
				}
			}
		}

		/// <summary>
		/// Draw nodes in skeleton for debugging.
		/// </summary>
		/// <param name="p_Node"></param>
		protected void DrawHierarchy( Node p_Node )
		{
			if( p_Node.id != p_Node.parentID )
			{
				Gizmos.color = Color.white;
				UnityEditor.Handles.color = Color.white;
				Node t_ParentNode = skeletonData.GetNodeWithId( p_Node.parentID );

				bool t_InChain = false;
				// Check if node and child are both in a chain
				foreach( var t_Chains in skeletonData.chains )
				{
					bool t_Parent = false;
					bool t_Child = false;

					foreach( var t_ChainNode in t_Chains.nodeIds )
					{
						if( t_ChainNode == p_Node.id )
							t_Parent = true;

						if( t_ChainNode == t_ParentNode.id )
							t_Child = true;
					}

					if( t_Parent && t_Child )
					{
						t_InChain = true;
						break;
					}
				}

				if( t_InChain )
				{
					Gizmos.color = Color.cyan;
					UnityEditor.Handles.color = Color.cyan;
				}

				DrawBone( t_ParentNode, p_Node );
			}

			//Draw settings
			Gizmos.color = Color.yellow;
			UnityEditor.Handles.color = Color.yellow;
			Vector3 t_NodePosition = debugAnimatedSkeleton ? p_Node.unityTransform.transform.position : p_Node.transform.position;
			Quaternion t_NodeRotation = debugAnimatedSkeleton ? p_Node.unityTransform.transform.rotation : p_Node.transform.rotation;

			if( p_Node.settings.usedSettings.HasFlag( CoreSDK.NodeSettingsFlag.Leaf ) )
			{
				Vector3 t_LeafPositon = t_NodePosition +
										(t_NodeRotation * p_Node.settings.leaf.direction.FromManus().normalized * p_Node.settings.leaf.length);
				UnityEditor.Handles.DrawLine( t_NodePosition, t_LeafPositon );
				UnityEditor.Handles.DrawWireDisc( t_LeafPositon, t_NodePosition - t_LeafPositon, p_Node.settings.leaf.length / 5f );
			}
		}

		/// <summary>
		/// Draw skeleton collider setups.
		/// </summary>
		/// <param name="p_Collider">Collider to draw</param>
		protected void DrawCollider( ColliderSetup p_Collider )
		{
			var t_Node = skeletonData.GetNodeWithId( p_Collider.nodeId );
			if( t_Node == null )
				return;

			Vector3 t_Pos = t_Node.unityTransform.position + t_Node.unityTransform.rotation * p_Collider.localPosition;
			Quaternion t_Rot = t_Node.unityTransform.rotation * Quaternion.Euler( p_Collider.localRotation );

			switch( p_Collider.type )
			{
				case CoreSDK.ColliderType.Sphere:
					GizmoShapes.DrawSphere( t_Pos, t_Rot, p_Collider.sphere.radius );
					break;
				case CoreSDK.ColliderType.Capsule:
					GizmoShapes.DrawCapsule( t_Pos, t_Rot, p_Collider.capsule.length, p_Collider.capsule.radius );
					break;
				case CoreSDK.ColliderType.Box:
					GizmoShapes.DrawBox( t_Pos, t_Rot, p_Collider.box.size.FromManus() );
					break;
				case CoreSDK.ColliderType.Invalid:
				default:
					break;
			}
		}

		/// <summary>
		/// Draw specific bone.
		/// </summary>
		/// <param name="p_Node">Start node</param>
		/// <param name="p_Target">Target node</param>
		/// <param name="p_BoneSize">Size of bone</param>
		private void DrawBone( Node p_Node, Node p_Target, float p_BoneSize = .05f )
		{
			// Make metaCarpal bones a slightly different color
			foreach( var t_Chain in skeletonData.chains )
			{
				if( p_Node.id == t_Chain.settings.finger.metacarpalBoneId &&
					(t_Chain.type == CoreSDK.ChainType.FingerIndex ||
					 t_Chain.type == CoreSDK.ChainType.FingerMiddle ||
					 t_Chain.type == CoreSDK.ChainType.FingerRing ||
					 t_Chain.type == CoreSDK.ChainType.FingerPinky ||
					 t_Chain.type == CoreSDK.ChainType.FingerThumb) )
				{
					Gizmos.color = new Color( .768f, .98f, 1 );
					UnityEditor.Handles.color = new Color( .8f, 1f, 1f );
				}
			}

			Vector3 t_Node = debugAnimatedSkeleton ? p_Node.unityTransform.transform.position : p_Node.transform.position;
			Quaternion t_NodeRotation = debugAnimatedSkeleton ? p_Node.unityTransform.transform.rotation : p_Node.transform.rotation;
			Vector3 t_Target = debugAnimatedSkeleton ? p_Target.unityTransform.transform.position : p_Target.transform.position;

			float t_BoneSize = p_BoneSize;
			t_BoneSize *= Vector3.Distance( t_Node, t_Target ) / .5f;

			Gizmos.DrawWireSphere( t_Node, t_BoneSize / 2f );
			if( t_Node == t_Target )
				return;

			Quaternion t_Rotation = Quaternion.LookRotation( t_Target - t_Node );
			Vector3[] t_Corners = new Vector3[4];
			Vector3 t_BoneStart = t_Node + t_Rotation * Vector3.forward * t_BoneSize / 2f;
			t_Corners[0] = t_BoneStart + t_Rotation * Vector3.up * t_BoneSize * .5f + t_Rotation * Vector3.right * t_BoneSize * .5f;
			t_Corners[1] = t_BoneStart + t_Rotation * Vector3.up * t_BoneSize * .5f + t_Rotation * -Vector3.right * t_BoneSize * .5f;
			t_Corners[2] = t_BoneStart + t_Rotation * -Vector3.up * t_BoneSize * .5f + t_Rotation * -Vector3.right * t_BoneSize * .5f;
			t_Corners[3] = t_BoneStart + t_Rotation * -Vector3.up * t_BoneSize * .5f + t_Rotation * Vector3.right * t_BoneSize * .5f;

			UnityEditor.Handles.DrawLine( t_Corners[0], t_Corners[1] );
			UnityEditor.Handles.DrawLine( t_Corners[1], t_Corners[2] );
			UnityEditor.Handles.DrawLine( t_Corners[2], t_Corners[3] );
			UnityEditor.Handles.DrawLine( t_Corners[3], t_Corners[0] );

			UnityEditor.Handles.DrawLine( t_Corners[0], t_Target );
			UnityEditor.Handles.DrawLine( t_Corners[1], t_Target );
			UnityEditor.Handles.DrawLine( t_Corners[2], t_Target );
			UnityEditor.Handles.DrawLine( t_Corners[3], t_Target );
		}
#endif

		/// <summary>
		/// Called upon node creation.
		/// </summary>
		/// <param name="p_Node"></param>
		protected virtual void OnNodeCreated( Node p_Node )
		{
			//Implemented on child class
		}

		/// <summary>
		/// Called on node setup.
		/// </summary>
		protected virtual void OnNodeSetup()
		{
			//Implemented on child class
		}

		/// <summary>
		/// Resets SkeletonData and clears anti scale nodes.
		/// </summary>
		public void ResetSkeletonData()
		{
			skeletonData = new SkeletonData();
			m_ScaledParentNodeID.Clear(); //clear any added antiScale nodes...
		}
	}
}
