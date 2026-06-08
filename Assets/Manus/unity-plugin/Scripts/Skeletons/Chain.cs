using System;
using System.Collections.Generic;

using Manus.Utility;

using UnityEngine;

namespace Manus.Skeletons
{
    /// <summary>
    /// Skeleton chains for animation.
    /// </summary>
    [Serializable]
	public class Chain
	{
		[HideInInspector]
		public string name;

		public uint id;
		public CoreSDK.ChainType type;
		public CoreSDK.ChainType appliedDataType;

		[HideInInspector]
		public uint dataIndex = 0; //This is not needed now, but might be used in the future to add multiple heads or so... 

		public CoreSDK.Side dataSide;
		public List<uint> nodeIds = new List<uint>();
		public CoreSDK.ChainSettings settings = new CoreSDK.ChainSettings().Default();

		public Chain() { }

		/// <summary>
		/// Basic constructor for chains.
		/// Applied data type set to chain type can be set after construction.
		/// </summary>
		/// <param name="p_Type">Type of chain</param>
		/// <param name="p_DataSide">Side of the data</param>
		/// <param name="p_NodeIds">IDs of the nodes in the chain</param>
		/// <param name="p_Settings">Settings of the chain</param>
		/// <param name="p_DataIndex">Data index of the chain</param>
		/// <param name="p_ID">ID of the chain</param>
		public Chain( CoreSDK.ChainType p_Type, CoreSDK.Side p_DataSide, List<uint> p_NodeIds, CoreSDK.ChainSettings p_Settings, uint p_DataIndex = 0, uint p_ID = 0 )
		{
			id = p_ID;
			type = p_Type;
			appliedDataType = p_Type;
			dataIndex = p_DataIndex;
			dataSide = p_DataSide;
			nodeIds = p_NodeIds;
			settings = p_Settings;
			UpdateName();
		}

		/// <summary>
		/// Update name of the chain for Manus Core and inspector.
		/// </summary>
		public void UpdateName()
		{
			name = $"{id}: {dataSide}-{type} ";
		}
	}
}
