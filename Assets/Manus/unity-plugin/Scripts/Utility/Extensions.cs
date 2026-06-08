using System;
using System.Collections.Generic;
using System.Linq;

using Manus.Skeletons;

using UnityEngine;

namespace Manus.Utility
{
	public static partial class Extensions
	{
		public static CoreSDK.ManusTransform ToManus( this TransformValues p_Transform )
		{
			return new CoreSDK.ManusTransform
			{
				position = p_Transform.position.ToManus(),
				rotation = p_Transform.rotation.ToManus(),
				scale = p_Transform.scale.ToManus()
			};
		}

		public static TransformValues FromManus( this CoreSDK.ManusTransform p_Transform )
		{
			return new TransformValues()
			{
				position = p_Transform.position.FromManus(),
				rotation = p_Transform.rotation.FromManus(),
				scale = p_Transform.scale.FromManus()
			};
		}

		public static CoreSDK.ManusVec3 ToManus( this Vector3 p_Vec3 )
		{
			return new CoreSDK.ManusVec3
			{
				x = p_Vec3.x,
				y = p_Vec3.y,
				z = p_Vec3.z
			};
		}

		public static CoreSDK.ManusQuaternion ToManus( this Quaternion p_Quaternion )
		{
			return new CoreSDK.ManusQuaternion
			{
				x = p_Quaternion.x,
				y = p_Quaternion.y,
				z = p_Quaternion.z,
				w = p_Quaternion.w
			};
		}

		public static Vector3 FromManus( this CoreSDK.ManusVec3 p_Vector3 )
		{
			return new Vector3
			{
				x = p_Vector3.x,
				y = p_Vector3.y,
				z = p_Vector3.z
			};
		}

		public static Quaternion FromManus( this CoreSDK.ManusQuaternion p_Quaternion )
		{
			return new Quaternion
			{
				x = p_Quaternion.x,
				y = p_Quaternion.y,
				z = p_Quaternion.z,
				w = p_Quaternion.w
			};
		}

		/// <summary>
		/// Create chain from Manus Core chain setup.
		/// </summary>
		/// <returns>Skeleton chain</returns>
		public static Chain FromChainSetup( this CoreSDK.ChainSetup p_ChainSetup )
		{
			return new Chain()
			{
				id = p_ChainSetup.id,
				type = p_ChainSetup.type,
				appliedDataType = p_ChainSetup.dataType,
				dataIndex = p_ChainSetup.dataIndex,
				dataSide = p_ChainSetup.side,
				nodeIds = p_ChainSetup.nodeIds.ToList().GetRange( 0, (int)p_ChainSetup.nodeIdCount ),
				settings = p_ChainSetup.settings.FromChainSettings()
			};
		}

		/// <summary>
		/// Create chain setup for Manus Core.
		/// </summary>
		/// <returns>Manus Core chain setup</returns>
		public static CoreSDK.ChainSetup ToChainSetup(this Chain p_Chain)
		{
			var t_ChainSetup = new CoreSDK.ChainSetup
			{
				id = p_Chain.id,
				type = p_Chain.type,
				dataType = p_Chain.appliedDataType,
				dataIndex = p_Chain.dataIndex,
				side = p_Chain.dataSide,
				nodeIdCount = (uint)p_Chain.nodeIds.Count,
				nodeIds = p_Chain.nodeIds.ToArray(),
				settings = p_Chain.settings.ToChainSettings()
			};
			
			// Resize array to marshaling size
			Array.Resize( ref t_ChainSetup.nodeIds, CoreSDK.CHAINSETUP_NODEIDS_ARRAY_SIZE );
			
			return t_ChainSetup;
		}

		private static CoreSDK.ChainSettings FromChainSettings( this CoreSDK.ChainSettings p_ChainSettings )
		{
			switch( p_ChainSettings.usedSettings )
			{
				case CoreSDK.ChainType.Hand:
					p_ChainSettings.hand = new CoreSDK.ChainSettingsHand( p_ChainSettings.hand.fingerChainIds,
						p_ChainSettings.hand.fingerChainIdsUsed,
						p_ChainSettings.hand.handMotion );
					// Resize array to amount of used chains (clean unity inspector) 
					Array.Resize( ref p_ChainSettings.hand.fingerChainIds, p_ChainSettings.hand.fingerChainIdsUsed );
					break;
				case CoreSDK.ChainType.Foot:
					p_ChainSettings.foot = new CoreSDK.ChainSettingsFoot( p_ChainSettings.foot.toeChainIds, p_ChainSettings.foot.toeChainIdsUsed );
					// Resize array to amount of used chains (clean unity inspector)
					Array.Resize( ref p_ChainSettings.foot.toeChainIds, p_ChainSettings.foot.toeChainIdsUsed );
					break;
			}

			return p_ChainSettings;
		}

		private static CoreSDK.ChainSettings ToChainSettings( this CoreSDK.ChainSettings p_ChainSettings )
		{
			//Resize arrays to marshaling size
			Array.Resize( ref p_ChainSettings.hand.fingerChainIds, CoreSDK.CHAINSETTINGSHAND_FINGERCHAINIDS_ARRAY_SIZE);
			Array.Resize( ref p_ChainSettings.foot.toeChainIds, CoreSDK.CHAINSETTINGSFOOT_TOECHAINIDS_ARRAY_SIZE );
			
			return p_ChainSettings;
		}

		public static ColliderSetup FromColliderSetup( this CoreSDK.ColliderSetup p_ColliderSetup )
		{
			return new ColliderSetup()
			{
				nodeId = p_ColliderSetup.nodeID,
				localPosition = p_ColliderSetup.localPosition.FromManus(),
				localRotation = p_ColliderSetup.localRotation.FromManus(),
				type = p_ColliderSetup.type,
				sphere = p_ColliderSetup.sphere,
				capsule = p_ColliderSetup.capsule,
				box = p_ColliderSetup.box,
			};
		}

		public static Node FromNodeSetup( this CoreSDK.NodeSetup p_NodeSetup, List<Node> p_Nodes, SkeletonData p_TargetSkeleton )
		{
			Node t_Node = new Node();
			t_Node.name = null;
			t_Node.nodeName = p_NodeSetup.name;
			t_Node.id = p_NodeSetup.id;
			t_Node.type = p_NodeSetup.type;
			t_Node.transform = p_NodeSetup.transform.FromManus();
			t_Node.parentID = p_NodeSetup.parentID;
			t_Node.settings = p_NodeSetup.settings;
			t_Node.UpdateName();

			//Collect corresponding transforms
			Transform t_UnityNode = p_TargetSkeleton.nodes[0].unityTransform.GetComponentsInChildren<Transform>()
				.Where( p_Transform => p_Transform.name == t_Node.nodeName ).First();
			if( t_UnityNode == null ) return null;
			if( t_Node.id != t_Node.parentID )
			{
				Node t_ParentNode = null;
				foreach( var t_OtherNode in p_Nodes )
				{
					if( t_OtherNode.id == t_Node.parentID )
					{
						t_ParentNode = t_OtherNode;
						break;
					}
				}

				if( t_ParentNode != null && t_ParentNode.unityTransform != null )
				{
					t_UnityNode.SetParent( t_ParentNode.unityTransform );
				}
			}

			t_UnityNode.localPosition = p_NodeSetup.transform.position.FromManus();
			t_UnityNode.localRotation = p_NodeSetup.transform.rotation.FromManus();
			t_UnityNode.localScale = p_NodeSetup.transform.scale.FromManus();
			t_Node.unityTransform = t_UnityNode;

			return t_Node;
		}

		public static bool IsUpdatingFirmware( this CoreSDK.UpdateStatusEnum p_UpdateStatus )
		{
			return p_UpdateStatus == CoreSDK.UpdateStatusEnum.UpdatingInitializing
				|| p_UpdateStatus == CoreSDK.UpdateStatusEnum.UpdatingBootloader
				|| p_UpdateStatus == CoreSDK.UpdateStatusEnum.UpdatingFirmware
				|| p_UpdateStatus == CoreSDK.UpdateStatusEnum.UpdatingFinalizing
				|| p_UpdateStatus == CoreSDK.UpdateStatusEnum.UpdatingFailed;
		}

		#region ChainSettings Default values

		public static CoreSDK.ChainSettingsPelvis Default(this CoreSDK.ChainSettingsPelvis p_ChainSettingsPelvis)
		{
			p_ChainSettingsPelvis.hipHeight = 1.0f;
			p_ChainSettingsPelvis.hipBendOffset = 0.0f;
			p_ChainSettingsPelvis.thicknessMultiplier = 1.0f;
			
			return p_ChainSettingsPelvis;
		}

		public static CoreSDK.ChainSettingsLeg Default( this CoreSDK.ChainSettingsLeg p_ChainSettingsLeg )
		{
			p_ChainSettingsLeg.reverseKneeDirection = false;
			p_ChainSettingsLeg.kneeRotationOffset = 0.0f;
			p_ChainSettingsLeg.footForwardOffset = 0.0f;
			p_ChainSettingsLeg.footSideOffset = 0.0f;
			
			return p_ChainSettingsLeg;
		}

		public static CoreSDK.ChainSettingsSpine Default(this CoreSDK.ChainSettingsSpine p_ChainSettingsSpine)
		{
			p_ChainSettingsSpine.spineBendOffset = 0.0f;
			
			return p_ChainSettingsSpine;
		}

		public static CoreSDK.ChainSettingsNeck Default(this CoreSDK.ChainSettingsNeck p_ChainSettingsNeck)
		{
			p_ChainSettingsNeck.neckBendOffset = 0.0f;
			
			return p_ChainSettingsNeck;
		}

		public static CoreSDK.ChainSettingsHead Default(this CoreSDK.ChainSettingsHead p_ChainSettingsHead)
		{
			p_ChainSettingsHead.headPitchOffset = 0.0f;
			p_ChainSettingsHead.headYawOffset = 0.0f;
			p_ChainSettingsHead.headTiltOffset = 0.0f;
			p_ChainSettingsHead.useLeafAtEnd = false;
			
			return p_ChainSettingsHead;
		}

		public static CoreSDK.ChainSettingsArm Default(this CoreSDK.ChainSettingsArm p_ChainSettingsArm)
		{
			p_ChainSettingsArm.armLengthMultiplier = 1.0f;
			p_ChainSettingsArm.elbowRotationOffset = 0.0f;
			p_ChainSettingsArm.armRotationOffset = new CoreSDK.ManusVec3();
			p_ChainSettingsArm.positionMultiplier = new CoreSDK.ManusVec3( 1.0f, 1.0f, 1.0f );
			p_ChainSettingsArm.positionOffset = new CoreSDK.ManusVec3();
			
			return p_ChainSettingsArm;
		}

		public static CoreSDK.ChainSettingsShoulder Default(this CoreSDK.ChainSettingsShoulder p_ChainSettingsShoulder)
		{
			p_ChainSettingsShoulder.forwardOffset = 0.0f;
			p_ChainSettingsShoulder.shrugOffset = 0.0f;
			p_ChainSettingsShoulder.forwardMultiplier = 1.0f;
			p_ChainSettingsShoulder.shrugMultiplier = 1.0f;
			
			return p_ChainSettingsShoulder;
		}

		public static CoreSDK.ChainSettingsFinger Default(this CoreSDK.ChainSettingsFinger p_ChainSettingsFinger)
		{
			p_ChainSettingsFinger.useLeafAtEnd = false;
			p_ChainSettingsFinger.metacarpalBoneId = -1;
			p_ChainSettingsFinger.handChainId = -1;
			p_ChainSettingsFinger.fingerWidth = 0;
			
			return p_ChainSettingsFinger;
		}

		public static CoreSDK.ChainSettingsHand Default(this CoreSDK.ChainSettingsHand p_ChainSettingsHand)
		{
			p_ChainSettingsHand.fingerChainIds = new int[CoreSDK.CHAINSETTINGSHAND_FINGERCHAINIDS_ARRAY_SIZE];
			p_ChainSettingsHand.fingerChainIdsUsed = 0;
			p_ChainSettingsHand.handMotion = CoreSDK.HandMotion.Auto;

			return p_ChainSettingsHand;
		}

		public static CoreSDK.ChainSettingsFoot Default(this CoreSDK.ChainSettingsFoot p_ChainSettingsFoot)
		{
			p_ChainSettingsFoot.toeChainIdsUsed = 0;
			p_ChainSettingsFoot.toeChainIds = new int[CoreSDK.CHAINSETTINGSFOOT_TOECHAINIDS_ARRAY_SIZE];

			return p_ChainSettingsFoot;
		}

		public static CoreSDK.ChainSettingsToe Default(this CoreSDK.ChainSettingsToe p_ChainSettingsToe)
		{
			p_ChainSettingsToe.footChainId = -1;
			p_ChainSettingsToe.useLeafAtEnd = false;
			
			return p_ChainSettingsToe;
		}

		public static CoreSDK.ChainSettings Default(this CoreSDK.ChainSettings p_ChainSettings)
		{
			p_ChainSettings.pelvis = p_ChainSettings.pelvis.Default();
			p_ChainSettings.leg = p_ChainSettings.leg.Default();
			p_ChainSettings.spine = p_ChainSettings.spine.Default();
			p_ChainSettings.neck = p_ChainSettings.neck.Default();
			p_ChainSettings.head = p_ChainSettings.head.Default();
			p_ChainSettings.arm = p_ChainSettings.arm.Default();
			p_ChainSettings.shoulder = p_ChainSettings.shoulder.Default();
			p_ChainSettings.finger = p_ChainSettings.finger.Default();
			p_ChainSettings.hand = p_ChainSettings.hand.Default();
			p_ChainSettings.foot = p_ChainSettings.foot.Default();
			p_ChainSettings.toe = p_ChainSettings.toe.Default();
			
			return p_ChainSettings;
		}

		#endregion
	}
}
