using System;
using Manus.Utility;

using UnityEngine;

namespace Manus.Skeletons
{
    /// <summary>
    /// Collider setup for animation collisions.
    /// </summary>
    [Serializable]
	public class ColliderSetup
	{
		public uint nodeId;
		public Vector3 localPosition;
		public Vector3 localRotation;
		public CoreSDK.ColliderType type;

		public CoreSDK.SphereColliderSetup sphere = new CoreSDK.SphereColliderSetup();
		public CoreSDK.CapsuleColliderSetup capsule = new CoreSDK.CapsuleColliderSetup();
		public CoreSDK.BoxColliderSetup box = new CoreSDK.BoxColliderSetup();

		/// <summary>
		/// Create collider setup for Manus Core.
		/// </summary>
		/// <returns>Manus Core collider setup</returns>
		public CoreSDK.ColliderSetup ToColliderSetup()
		{
			return new CoreSDK.ColliderSetup
			{
				nodeID = nodeId,
				localPosition = localPosition.ToManus(),
				localRotation = localRotation.ToManus(),
				type = type,
				sphere = sphere,
				capsule = capsule,
				box = box
			};
		}
	}
}
