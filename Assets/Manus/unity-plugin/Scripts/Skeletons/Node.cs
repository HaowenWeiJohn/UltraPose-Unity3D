using System;

using Manus.Utility;

using UnityEngine;

namespace Manus.Skeletons
{
    /// <summary>
    /// Skeleton nodes for animation of skeleton.
    /// </summary>
    [Serializable]
	public class Node
	{
		[HideInInspector]
		public string name;
		public string nodeName;
		public uint id;
		public CoreSDK.NodeType type;
		public TransformValues transform;
		public uint parentID;
		[HideInInspector]
		public CoreSDK.NodeSettings settings;

		public Transform unityTransform;


		/// <summary>
		/// Setup transform values so they are not null.
		/// </summary>
		public Node()
		{
			transform = new TransformValues();
		}

		/// <summary>
		/// Update name of the node for Manus Core and inspector.
		/// </summary>
		public void UpdateName()
		{
			name = $"{id}: {nodeName}";
		}

		/// <summary>
		/// Create node setup for Manus Core.
		/// </summary>
		/// <returns>Manus Core node setup</returns>
		public CoreSDK.NodeSetup ToNodeSetup()
		{
			CoreSDK.NodeSettings t_Settings = settings;
			return new CoreSDK.NodeSetup
			{
				id = id,
				name = nodeName,
				type = type,
				transform = transform.ToManus(),
				parentID = parentID,
				settings = t_Settings
			};
		}
	}
}
