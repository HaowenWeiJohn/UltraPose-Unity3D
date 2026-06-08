using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Manus.Skeletons
{
	/// <summary>
	/// Skeleton data holds all data from the skeleton.
	/// </summary>
	[Serializable]
	public class SkeletonData
	{
		[HideInInspector]
		public uint id;
		public string name;

		public CoreSDK.ManusTimestamp lastTimestamp;

		public CoreSDK.SkeletonType type;
		public List<Node> nodes;
		public List<Chain> chains;
		public List<ColliderSetup> colliders;
		public List<MeshSetup> meshes;
		public CoreSDK.SkeletonSettings settings;

		public SkeletonData()
		{
			nodes = new List<Node>();
			chains = new List<Chain>();
			colliders = new List<ColliderSetup>();
			meshes = new List<MeshSetup>();
			settings = new CoreSDK.SkeletonSettings();
		}

		/// <summary>
		/// Fetch specific node with an ID.
		/// </summary>
		/// <param name="p_Id">ID of the node</param>
		/// <returns>Node with ID or null if not found</returns>
		public Node GetNodeWithId( uint p_Id )
		{
			for( int i = 0; i < nodes.Count; i++ )
			{
				if( nodes[i].id == p_Id )
				{
					return nodes[i];
				}
			}
			return null;
		}

		/// <summary>
		/// Fetch a specific with a name.
		/// </summary>
		/// <param name="p_Name">Name of the node</param>
		/// <returns>Node with name or null if not found</returns>
		public Node GetNodeWithName( string p_Name )
		{
			for( int i = 0; i < nodes.Count; i++ )
			{
				if( nodes[i].nodeName == p_Name )
				{
					return nodes[i];
				}
			}
			return null;
		}

		/// <summary>
		/// Fetch a specific with a specific Unity Transform.
		/// </summary>
		/// <param name="p_Transform">Unity Transform</param>
		/// <returns>Node with name or null if not found</returns>
		public Node GetNodeWithUnityTransform( Transform p_Transform )
		{
			for( int i = 0; i < nodes.Count; i++ )
			{
				if( nodes[i].unityTransform == p_Transform )
				{
					return nodes[i];
				}
			}
			return null;
		}

		/// <summary>
		/// Fetch children of node ID.
		/// </summary>
		/// <param name="p_Id">ID of node</param>
		/// <returns>List of child nodes</returns>
		public IList<Node> GetChildNodesOfNodeWithId( uint p_Id )
		{
			List<Node> t_ChildNodes = new List<Node>();

			for( int i = 0; i < nodes.Count; i++ )
			{
				var t_Node = nodes[i];
				if( t_Node.parentID == p_Id && t_Node.parentID != t_Node.id )
				{
					t_ChildNodes.Add( nodes[i] );
				}
			}

			return t_ChildNodes.AsReadOnly();
		}

		/// <summary>
		/// Go through and count depth of the node.
		/// </summary>
		/// <param name="p_Id">ID of node</param>
		/// <returns>Depth of the node</returns>
		public int GetNodeDepth( uint p_Id )
		{
			Node t_Node = GetNodeWithId( p_Id );
			int t_Depth = 0;

			while( t_Node.id != t_Node.parentID )
			{
				t_Node = GetNodeWithId( t_Node.parentID );
				t_Depth++;
			}

			return t_Depth;
		}

		/// <summary>
		/// Fetch specific chain with an ID.
		/// </summary>
		/// <param name="p_Id">ID of the chain</param>
		/// <returns>Chain with ID or null if not found</returns>
		public Chain GetChainWithId( uint p_Id )
		{
			for( int i = 0; i < chains.Count; i++ )
			{
				if( chains[i].id == p_Id )
				{
					return chains[i];
				}
			}
			return null;
		}

		/// <summary>
		/// Create skeleton setup for Manus Core.
		/// </summary>
		/// <returns>Manus core skeleton setup</returns>
		public CoreSDK.SkeletonSetupInfo ToSkeletonSetup()
		{
			return new CoreSDK.SkeletonSetupInfo
			{
				id = id,
				name = name,
				type = type,
				settings = settings
			};
		}

		/// <summary>
		/// Fetch nodes from a node ID list.
		/// </summary>
		/// <param name="p_NodeIds">Node IDs</param>
		/// <returns>List of nodes</returns>
		public List<Node> ToNodeList( List<uint> p_NodeIds )
		{
			List<Node> t_List = new List<Node>();
			p_NodeIds.ForEach( p_NodeId => t_List.Add( GetNodeWithId( p_NodeId ) ) );

			return t_List;
		}

		/// <summary>
		/// Get new chain ID.
		/// </summary>
		/// <returns>New chain ID</returns>
		public uint GetNewChainID()
		{
			return chains.Count > 0 ? chains.Max( p_Chain => p_Chain.id + 1 ) : 1;
		}
	}
}
