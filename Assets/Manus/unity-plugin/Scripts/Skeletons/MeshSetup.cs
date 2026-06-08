using System;
using System.Collections.Generic;

using Manus.Utility;

using UnityEngine;

namespace Manus.Skeletons
{
    /// <summary>
    /// Collider setup for animation collisions.
    /// </summary>
    [Serializable]
	public class MeshSetup
	{
		public uint nodeId;
		public List<CoreSDK.Vertex> vertices;
		public List<CoreSDK.Triangle> triangles;

		public MeshSetup()
		{
			nodeId = 0;
			vertices = new List<CoreSDK.Vertex>();
			triangles = new List<CoreSDK.Triangle>();
		}

		public MeshSetup(uint p_NodeId)
		{
			nodeId = p_NodeId;
			vertices = new List<CoreSDK.Vertex>();
			triangles = new List<CoreSDK.Triangle>();
		}

		public void Add(MeshSetup p_OtherSetup)
		{
			// Modify triangles
			int t_VertexCount = vertices.Count;
			for( int i = 0; i < p_OtherSetup.triangles.Count; i++ )
			{
				var t_Triangle = p_OtherSetup.triangles[i];
				t_Triangle.vertexIndex1 += t_VertexCount;
				t_Triangle.vertexIndex2 += t_VertexCount;
				t_Triangle.vertexIndex3 += t_VertexCount;

				p_OtherSetup.triangles[i] = t_Triangle;
			}

			vertices.AddRange(p_OtherSetup.vertices);
			triangles.AddRange(p_OtherSetup.triangles);
		}
	}
}
