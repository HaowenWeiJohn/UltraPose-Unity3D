using System.Collections.Generic;

using Manus.Skeletons;
using Manus.Utility;

using UnityEditor;

using UnityEngine;

namespace Manus.Editor
{
	/// <summary>
	/// Custom inspector view for skeleton component.
	/// </summary>
	[CustomEditor( typeof( Skeleton ) ), CanEditMultipleObjects]
	public class SkeletonDataEditor : UnityEditor.Editor
	{

		/* Booleans for Foldout fields*/
		protected static bool s_ShowSkeletonSettings = false;

		protected static bool s_ShowNodes = false;
		protected static List<bool> s_ShowNodeDetails = new List<bool>();
		protected static bool s_ChangedNodeTransform = true;
		protected static bool s_ShowNodesSettings = false;

		protected static bool s_ShowChains = false;
		protected static List<bool> s_ShowChainDetails = new List<bool>();
		protected static bool s_ShowChainSettings = false;
		protected static bool s_ShowNodesInChain = false;
		protected static bool s_ShowFingersInHand = false;
		protected static bool s_ShowToesInFoot = false;

		protected static bool s_ShowColliders = false;
		protected static List<bool> s_ShowColliderDetails = new List<bool>();
		/* Style */
		Vector2 m_NodeScrollPosition = Vector2.zero;
		Vector2 m_ChainScrollPosition = Vector2.zero;
		Vector2 m_CollidersScrollPosition = Vector2.zero;

		/// <summary>
		/// Draw inspector view.
		/// </summary>
		public override void OnInspectorGUI()
		{
			GUIStyle t_BoldLabel = new GUIStyle(EditorStyles.boldLabel);
			t_BoldLabel.stretchWidth = true;
			GUIStyle t_FoldoutStyle = new GUIStyle(EditorStyles.foldout);

			Skeleton t_Skeleton = (Skeleton)target;
			bool t_Connected = ManusManager.communicationHub.currentState == CommunicationHub.State.Connected;
			EditorGUILayout.Space();

			EditorGUILayout.LabelField( t_Skeleton.name + " Data", EditorStyles.boldLabel );

			// Buttons
			EditorGUILayout.Space();

			if( !t_Connected && !Application.isPlaying ) EditorGUILayout.HelpBox( "Some functionality is disabled due to Manus Core not being connected.", MessageType.Info );
			EditorGUILayout.BeginHorizontal();
			{

				GUI.enabled = !Application.isPlaying;
				if( GUILayout.Button( new GUIContent( "Setup Nodes", "Finds all the transforms under this object, makes them into nodes and sets up all of the nodes' settings." ), GUILayout.Height( 20 ) ) )
				{
					t_Skeleton.SetupNodes();
					s_ChangedNodeTransform = false;
				}
				GUI.enabled = true;

				GUI.enabled = t_Skeleton.skeletonData.nodes.Count > 0 && !Application.isPlaying && t_Connected;
				if( GUILayout.Button( new GUIContent( "Send skeleton to tool", "Sends the skeleton nodes to the Dev Tools application. This application helps speed up the chain creation process." ), GUILayout.Height( 20 ) ) )
					t_Skeleton.SendSkeletonToDevTools();
				GUI.enabled = true;


			}
			EditorGUILayout.EndHorizontal();

			GUI.enabled = t_Skeleton.sklSetupIdx.HasValue && ManusManager.communicationHub.HasLoadableSkeleton( t_Skeleton.sklSetupIdx.Value ) && !Application.isPlaying;
			if( GUILayout.Button( new GUIContent( "Load skeleton from tool", "Load the modified skeleton sent from the Dev Tools." ), GUILayout.Height( 20 ) ) )
			{
				t_Skeleton.LoadSkeleton();
				ManusManager.communicationHub.RemoveLoadableSkeleton( t_Skeleton.sklSetupIdx.Value );
			}
			GUI.enabled = true;

			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();
			{
				GUI.enabled = ManusManager.communicationHub.currentState == CommunicationHub.State.Connected && !Application.isPlaying;
				if( GUILayout.Button( new GUIContent( "Import skeleton from file", "Load a MSKL (Manus Skeleton) file into this component." ), GUILayout.Height( 20 ) ) )
				{
					string t_Path = EditorUtility.OpenFilePanel( "Import Manus Skeleton file", "", "mskl" );
					if( t_Path != "" ) ManusManager.communicationHub.LoadTemporarySkeletonFromFile( t_Skeleton, t_Path );
				}
				if( GUILayout.Button( new GUIContent( "Export skeleton to file", "Save current skeleton to a MSKL (Manus Skeleton) file." ), GUILayout.Height( 20 ) ) )
				{
					string t_Path = EditorUtility.SaveFilePanel( "Export Manus Skeleton file", "", t_Skeleton.name, "mskl" );
					if( t_Path != "" ) ManusManager.communicationHub.SaveTemporarySkeletonToFile( t_Skeleton, t_Path );
				}
				GUI.enabled = true;
			}
			EditorGUILayout.EndHorizontal();

			// Skeleton
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.LabelField( "", GUI.skin.horizontalSlider );

			EditorGUILayout.BeginVertical( "box" );
			{
				EditorGUILayout.LabelField( "Settings", EditorStyles.boldLabel );

				EditorGUILayout.BeginVertical( "box" );
				{
					{
						t_Skeleton.debugAnimatedSkeleton = EditorGUILayout.Toggle( new GUIContent( "Draw Skeleton", "Enable a debug view of this skeleton in the viewport." ), t_Skeleton.debugAnimatedSkeleton );
						t_Skeleton.debugColliders = EditorGUILayout.Toggle( new GUIContent( "Draw Colliders", "Draw the colliders in this skeleton in viewport." ), t_Skeleton.debugColliders );
					}
					{
						t_Skeleton.isLocalPlayer = EditorGUILayout.Toggle( new GUIContent( "Local Player", "Does this skeleton belong to this instance of unity?" ), t_Skeleton.isLocalPlayer );
						t_Skeleton.enableHaptics = EditorGUILayout.Toggle( new GUIContent( "Enable Haptics", "Enables haptic feedback on hands." ), t_Skeleton.enableHaptics );
						EditorGUI.BeginChangeCheck();
					}
				}
				t_Skeleton.skeletonData.settings.scaleToTarget = EditorGUILayout.Toggle( new GUIContent( "Scale To Target", "Scales the skeleton to the target set in Manus Core." ), t_Skeleton.skeletonData.settings.scaleToTarget );

				t_Skeleton.skeletonData.settings.useEndPointApproximations = EditorGUILayout.Toggle( new GUIContent( "Use Endpoint Approximations", "Allows Manus Core to use endpoint approximation to improve positioning." ),
				t_Skeleton.skeletonData.settings.useEndPointApproximations );
				EditorGUILayout.EndVertical();
				EditorGUILayout.Space();

				CoreSDK.SkeletonType t_SkeletonType = (CoreSDK.SkeletonType)EditorGUILayout.EnumPopup( new GUIContent( "Skeleton Type", "This is the type of skeleton that this skeleton is." ), t_Skeleton.skeletonData.type );
				t_Skeleton.skeletonData.type = t_SkeletonType;

				EditorGUILayout.Space();

				for( int i = 0; i < t_Skeleton.skeletonData.chains.Count; i++ )
				{
					if( t_Skeleton.skeletonData.chains[i].type == CoreSDK.ChainType.Hand && t_Skeleton.skeletonData.type != CoreSDK.SkeletonType.Invalid )
					{
						t_Skeleton.skeletonData.chains[i].settings.hand.handMotion = (CoreSDK.HandMotion)EditorGUILayout.EnumPopup( new GUIContent(
						t_Skeleton.skeletonData.chains[i].dataSide + " Hand Motion Type",
						"Tracking source of hand, will follow skeleton if set to none." ), t_Skeleton.skeletonData.chains[i].settings.hand.handMotion );
					}
				}
				EditorGUILayout.Space();

				CoreSDK.SkeletonTargetType t_SkeletonTargetType =
				(CoreSDK.SkeletonTargetType)EditorGUILayout.EnumPopup( new GUIContent( "Targeted Data Type", "The data target used in Manus Core for animating the skeleton." ),
				t_Skeleton.skeletonData.settings.targetType );

				t_Skeleton.skeletonData.settings.targetType = t_SkeletonTargetType;

				switch( t_SkeletonTargetType )
				{
					case CoreSDK.SkeletonTargetType.Invalid:
						break;
					case CoreSDK.SkeletonTargetType.UserData:
						t_Skeleton.skeletonData.settings.skeletonTargetUserData.userID = (uint)EditorGUILayout.DelayedIntField( new GUIContent( "User ID", "The ID of the user that this skeleton should use to animate." ),
							(int)t_Skeleton.skeletonData.settings.skeletonTargetUserData.userID );
						break;
					case CoreSDK.SkeletonTargetType.UserIndexData:
						t_Skeleton.skeletonData.settings.skeletonTargetUserIndexData.userIndex = (uint)EditorGUILayout.DelayedIntField(
							new GUIContent( "User Index", "The index of the User that this skeleton should use to animate." ),
							(int)t_Skeleton.skeletonData.settings.skeletonTargetUserIndexData.userIndex );
						break;
					case CoreSDK.SkeletonTargetType.AnimationData:
						EditorGUILayout.HelpBox( "This feature is currently unavailable", MessageType.Warning );
						break;
					case CoreSDK.SkeletonTargetType.GloveData:
						t_Skeleton.skeletonData.settings.skeletonGloveData.gloveID = (uint)EditorGUILayout.DelayedIntField( new GUIContent( "Glove ID", "The ID of the glove that this skeleton should use to animate." ),
							(int)t_Skeleton.skeletonData.settings.skeletonGloveData.gloveID );
						break;
				}
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.LabelField( "", GUI.skin.horizontalSlider );

			// Nodes

			for( int i = 0; i < t_Skeleton.skeletonData.nodes.Count; i++ )
			{
				if( !t_Skeleton.skeletonData.nodes[i].unityTransform.hasChanged ) continue;
				s_ChangedNodeTransform = true;
				t_Skeleton.skeletonData.nodes[i].unityTransform.hasChanged = false;
			}
			s_ShowNodes = EditorGUILayout.Foldout( s_ShowNodes, "Nodes", GUI.enabled, t_FoldoutStyle );
			if( s_ShowNodes )
			{
				int t_NodeCount = t_Skeleton.skeletonData.nodes.Count;

				EditorGUILayout.BeginVertical( "box", GUILayout.MinHeight( t_NodeCount > 10 ? 250 : t_NodeCount * 25 ) );//a box around the nodes list
				{
					EditorGUILayout.BeginHorizontal( "box", GUILayout.ExpandWidth( true ) );//a box around the title and the node count
					{
						EditorGUILayout.LabelField( "Node Count", t_BoldLabel, GUILayout.MinWidth( 40 ), GUILayout.ExpandWidth( true ) );
						EditorGUILayout.LabelField( t_NodeCount.ToString(), t_BoldLabel, GUILayout.MinWidth( 40 ), GUILayout.ExpandWidth( true ) );
					}
					EditorGUILayout.EndHorizontal();

					m_NodeScrollPosition = EditorGUILayout.BeginScrollView( m_NodeScrollPosition );
					{
						while( t_NodeCount < s_ShowNodeDetails.Count )
						{
							s_ShowNodeDetails.RemoveRange( t_NodeCount, (s_ShowNodeDetails.Count - t_NodeCount) );
						}
						while( t_NodeCount > s_ShowNodeDetails.Count )
						{
							s_ShowNodeDetails.Add( false );
						}

						for( int i = 0; i < t_NodeCount; i++ )
						{
							var t_Node = t_Skeleton.skeletonData.nodes[i];
							EditorGUI.indentLevel++;

							s_ShowNodeDetails[i] = EditorGUILayout.Foldout( s_ShowNodeDetails[i], t_Node.name, GUI.enabled, t_FoldoutStyle );

							if( s_ShowNodeDetails[i] )
							{
								EditorGUI.indentLevel++;
								EditorGUILayout.BeginVertical( "box" );//a box around the selected node's details
								{
									t_Node.type =
										(CoreSDK.NodeType)EditorGUILayout.EnumPopup( new GUIContent( "Node Type", "The type of node that this node is." ), t_Node.type );

									t_Node.unityTransform = (Transform)EditorGUILayout.ObjectField( "Game Object",
										t_Node.unityTransform, typeof( Transform ), false );

									s_ShowNodesSettings = EditorGUILayout.Foldout( s_ShowNodesSettings, "Node Settings", GUI.enabled, t_FoldoutStyle );

									if( s_ShowNodesSettings )
									{
										EditorGUILayout.BeginVertical( "box" );//a box around the selected node's settings
										{
											t_Node.settings.usedSettings =
												(CoreSDK.NodeSettingsFlag)EditorGUILayout.EnumFlagsField( "Settings",
													t_Node.settings.usedSettings );
											EditorGUI.indentLevel++;

											if( t_Node.settings.usedSettings.HasFlag( CoreSDK.NodeSettingsFlag.IK ) )
											{
												t_Node.settings.ik.ikAim =
													EditorGUILayout.Slider( new GUIContent( "IK Aim", "This is the direction of IK aim." ), t_Node.settings.ik.ikAim, -1.0f, 1.0f );
											}
											if( t_Node.settings.usedSettings.HasFlag( CoreSDK.NodeSettingsFlag.Foot ) )
											{
												t_Node.settings.foot.heightFromGround = EditorGUILayout.FloatField( new GUIContent( "Foot Height from ground", "The height of foot from ground in meters." ),
													t_Node.settings.foot.heightFromGround );
											}
											if( t_Node.settings.usedSettings.HasFlag( CoreSDK.NodeSettingsFlag.RotationOffset ) )
											{
												Vector3 t_Rotation = EditorGUILayout.Vector3Field( new GUIContent( "Rotation offset", "The rotational offset of this node." ),
												t_Node.settings.rotationOffset.value.FromManus().eulerAngles);
												t_Node.settings.rotationOffset.value = Quaternion.Euler( t_Rotation ).ToManus();
											}
											if( t_Node.settings.usedSettings.HasFlag( CoreSDK.NodeSettingsFlag.Leaf ) )
											{
												Vector3 t_Direction = EditorGUILayout.Vector3Field( new GUIContent( "Leaf direction", "The direction which the leaf settings point toward." ),
												t_Node.settings.leaf.direction.FromManus());
												t_Node.settings.leaf.direction = t_Direction.ToManus();
												t_Node.settings.leaf.length = EditorGUILayout.FloatField( new GUIContent( "Leaf length", "The length of the leaf bone." ),
													t_Node.settings.leaf.length );
											}
											EditorGUI.indentLevel--;
										}
										EditorGUILayout.EndVertical();
									}
								}
								EditorGUILayout.EndVertical();
								EditorGUI.indentLevel--;

							}
							EditorGUI.indentLevel--;
						}
					}
					EditorGUILayout.EndScrollView();
				}
				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.LabelField( "", GUI.skin.horizontalSlider );

			// Chains 

			s_ShowChains = EditorGUILayout.Foldout( s_ShowChains, "Chains", GUI.enabled );
			if( s_ShowChains )
			{
				EditorGUILayout.BeginVertical( "box", GUILayout.MinHeight( t_Skeleton.skeletonData.chains.Count > 10 ? 250 : t_Skeleton.skeletonData.chains.Count * 25 ) );//a box around the whole chains editor
				{
					EditorGUILayout.BeginHorizontal( "box" );//a box around the chains title, count and the add a chain button
					{
						uint t_ChainCount = (uint)t_Skeleton.skeletonData.chains.Count;

						EditorGUILayout.LabelField( "Chain Count", EditorStyles.boldLabel, GUILayout.MinWidth( 40 ) );

						GUILayout.FlexibleSpace();

						EditorGUILayout.LabelField( t_ChainCount.ToString(), EditorStyles.boldLabel, GUILayout.MinWidth( 40 ) );

						GUILayout.FlexibleSpace();

						var t_BackgroundColor = GUI.backgroundColor;
						GUI.backgroundColor = new Color( .21f, 2.8f, .5f );
						if( GUILayout.Button( "Add a chain", GUILayout.MinWidth( 100 ) ) ) t_Skeleton.skeletonData.chains.Add( new Chain() );
						GUI.backgroundColor = t_BackgroundColor;
					}
					EditorGUILayout.EndHorizontal();

					EditorGUI.indentLevel++;

					for( int s = 0; s < t_Skeleton.skeletonData.chains.Count; s++ )
					{
						string t_ChainName = $"{t_Skeleton.skeletonData.chains[s].id}: Empty Chain";

						if( t_Skeleton.skeletonData.chains[s].type != CoreSDK.ChainType.Invalid )
						{
							t_ChainName = $"{t_Skeleton.skeletonData.chains[s].id}:{t_Skeleton.skeletonData.chains[s].dataSide}" +
										$"-{t_Skeleton.skeletonData.chains[s].type} ";
						}

						if( t_Skeleton.skeletonData.chains[s].id == 0 )
						{
							// Find unique chain ID
							t_Skeleton.skeletonData.chains[s].id = t_Skeleton.skeletonData.GetNewChainID();
						}

						t_Skeleton.skeletonData.chains[s].name = t_ChainName;
					}

					if( t_Skeleton.skeletonData.chains.Count > s_ShowChainDetails.Count )
					{
						for( int i = s_ShowChainDetails.Count; i < t_Skeleton.skeletonData.chains.Count; i++ )
						{
							s_ShowChainDetails.Add( false );
						}
					}
					else if( t_Skeleton.skeletonData.chains.Count < s_ShowChainDetails.Count )
					{
						s_ShowChainDetails.RemoveRange( t_Skeleton.skeletonData.chains.Count,
							(s_ShowChainDetails.Count - t_Skeleton.skeletonData.chains.Count) );
					}

					m_ChainScrollPosition = EditorGUILayout.BeginScrollView( m_ChainScrollPosition );
					{
						for( int i = 0; i < t_Skeleton.skeletonData.chains.Count; i++ )
						{
							EditorGUI.indentLevel++;
							DisplayChain( t_Skeleton, i );
							EditorGUI.indentLevel--;
						}
					}
					EditorGUILayout.EndScrollView();

					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.LabelField( "", GUI.skin.horizontalSlider );

			/*
			// Colliders
			s_ShowColliders = EditorGUILayout.Foldout( s_ShowColliders, "Colliders", GUI.enabled );
			if( s_ShowColliders )
			{

				EditorGUILayout.BeginVertical( "box", GUILayout.MinHeight( t_Skeleton.skeletonData.colliders.Count > 10 ? 250 : t_Skeleton.skeletonData.colliders.Count * 25 ) );
				{

					t_Skeleton.skeletonData.settings.collisionType =
						(CoreSDK.CollisionType)EditorGUILayout.EnumPopup( "Collision Detection", t_Skeleton.skeletonData.settings.collisionType );
					EditorGUILayout.BeginHorizontal( "box" );//a box around the collider title, count and the add a collider button
					{
						uint t_ColliderCount = (uint)t_Skeleton.skeletonData.colliders.Count;

						EditorGUILayout.LabelField( "Collider Count", EditorStyles.boldLabel, GUILayout.MinWidth( 150 ) );

						GUILayout.FlexibleSpace();

						EditorGUILayout.LabelField( t_ColliderCount.ToString(), EditorStyles.boldLabel, GUILayout.MinWidth( 50 ) );

						GUILayout.FlexibleSpace();

						var t_BackgroundColor = GUI.backgroundColor;
						GUI.backgroundColor = new Color( .21f, 2.8f, .5f );

						if( GUILayout.Button( "Add a collider" ) ) t_Skeleton.skeletonData.colliders.Add( new ColliderSetup() );

						GUI.backgroundColor = t_BackgroundColor;
					}
					EditorGUILayout.EndHorizontal();

					m_CollidersScrollPosition = EditorGUILayout.BeginScrollView( m_CollidersScrollPosition );

					if( t_Skeleton.skeletonData.colliders.Count > s_ShowColliderDetails.Count )
					{
						for( int i = s_ShowColliderDetails.Count; i < t_Skeleton.skeletonData.colliders.Count; i++ )
						{
							s_ShowColliderDetails.Add( false );
						}
					}
					else if( t_Skeleton.skeletonData.colliders.Count < s_ShowColliderDetails.Count )
					{
						s_ShowColliderDetails.RemoveRange( t_Skeleton.skeletonData.colliders.Count,
							(s_ShowColliderDetails.Count - t_Skeleton.skeletonData.colliders.Count) );
					}

					for( int i = 0; i < t_Skeleton.skeletonData.colliders.Count; i++ )
					{
						EditorGUI.indentLevel++;
						EditorGUILayout.BeginHorizontal();

						Node t_Node = t_Skeleton.skeletonData.GetNodeWithId(t_Skeleton.skeletonData.colliders[i].nodeId);
						s_ShowColliderDetails[i] = EditorGUILayout.Foldout( s_ShowColliderDetails[i],
							$"{i}: {t_Node?.nodeName} - {t_Skeleton.skeletonData.colliders[i].type}",
							GUI.enabled );

						var t_ColliderButton = GUI.backgroundColor;
						GUI.backgroundColor = new Color( 3f, .25f, .2f );
						var t_ColliderText = new GUIStyle( GUI.skin.button );
						t_ColliderText.normal.textColor = Color.white;

						if( GUILayout.Button( new GUIContent( " - ", "Delete Collider" ), t_ColliderText, GUILayout.Width( 25 ) ) )
							t_Skeleton.skeletonData.colliders.RemoveAt( i );

						GUI.backgroundColor = t_ColliderButton;

						EditorGUILayout.EndHorizontal();

						if( s_ShowColliderDetails[i] )
						{
							EditorGUI.indentLevel++;

							var t_Collider = t_Skeleton.skeletonData.colliders[i];

							EditorGUILayout.BeginVertical( "box" );//a box around the collider 
							t_Collider.nodeId = (uint)EditorGUILayout.DelayedIntField( "Node ID", (int)t_Collider.nodeId );

							t_Skeleton.skeletonData.nodes[(int)t_Node?.id].unityTransform = (Transform)EditorGUILayout.ObjectField( new GUIContent( "Game Object", "Transform of the node." ),
												t_Skeleton.skeletonData.nodes[(int)t_Node?.id].unityTransform, typeof( Transform ), false );

							t_Collider.localPosition = EditorGUILayout.Vector3Field( new GUIContent( "Local position", "Local potion of the collider." ), t_Collider.localPosition );
							t_Collider.localRotation = EditorGUILayout.Vector3Field( new GUIContent( "Local rotation", "Local rotation of the collider." ), t_Collider.localRotation );

							CoreSDK.ColliderType t_ColliderType =
										(CoreSDK.ColliderType)EditorGUILayout.EnumPopup( new GUIContent( "Collider Type", "Type of collider being used." ), t_Skeleton.skeletonData.colliders[i].type );
							t_Collider.type = t_ColliderType;


							switch( t_Collider.type )
							{
								case CoreSDK.ColliderType.Invalid:
									break;
								case CoreSDK.ColliderType.Sphere:
									EditorGUI.indentLevel++;
									EditorGUILayout.BeginVertical( "box" );//a box around the collider type's settings

									t_Collider.sphere.radius = EditorGUILayout.Slider( new GUIContent( "Radius", "Radius of the sphere collider." ), t_Collider.sphere.radius, 0, 1 );
									EditorGUILayout.EndVertical();
									break;
								case CoreSDK.ColliderType.Capsule:
									EditorGUI.indentLevel++;
									EditorGUILayout.BeginVertical( "box" );//a box around the collider type's settings

									t_Collider.capsule.radius = EditorGUILayout.Slider( new GUIContent( "Radius", "Radius of the capsule collider." ), t_Collider.capsule.radius, 0, 1 );
									t_Collider.capsule.length = EditorGUILayout.Slider( new GUIContent( "Length", "Length of capsule collider." ), t_Collider.capsule.length, 0, 5 );
									EditorGUILayout.EndVertical();

									break;
								case CoreSDK.ColliderType.Box:
									EditorGUI.indentLevel++;
									EditorGUILayout.BeginVertical( "box" );//a box around the collider type's settings

									t_Collider.box.size = EditorGUILayout.Vector3Field( new GUIContent( "Size", "Size of box collider" ), t_Collider.box.size.FromManus() ).ToManus();
									EditorGUILayout.EndVertical();

									break;
								default:
									break;
							}
							t_Skeleton.skeletonData.colliders[i] = t_Collider;

							EditorGUI.indentLevel--;
							EditorGUI.indentLevel--;
							EditorGUILayout.EndVertical();
						}
						EditorGUI.indentLevel--;

					}

					EditorGUILayout.EndScrollView();
				}
				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.LabelField( "", GUI.skin.horizontalSlider );
			*/

			if( EditorGUI.EndChangeCheck() && Application.isPlaying )
			{
				t_Skeleton.SendSkeleton();

			}

			if( EditorGUI.EndChangeCheck() )
			{
				if( !Application.isPlaying )
				{
					EditorUtility.SetDirty( t_Skeleton );
					UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty( t_Skeleton.gameObject.scene );
				}
				t_Skeleton.UpdateEnableHaptics();
			}
		}

		/// <summary>
		/// Display chains details.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton to grab data from</param>
		/// <param name="p_Index">Index of the chain</param>
		void DisplayChain( Skeleton p_Skeleton, int p_Index )
		{
			EditorGUILayout.BeginHorizontal();
			{
				s_ShowChainDetails[p_Index] = EditorGUILayout.Foldout( s_ShowChainDetails[p_Index], p_Skeleton.skeletonData.chains[p_Index].name, GUI.enabled );

				var t_BackgroundColor = GUI.backgroundColor;
				GUI.backgroundColor = new Color( 3f, .25f, .2f );
				var t_TextStyle = new GUIStyle( GUI.skin.button );
				t_TextStyle.normal.textColor = Color.white;
				if( GUILayout.Button( new GUIContent( " - ", "Delete this chain." ), t_TextStyle, GUILayout.Width( 25 ) ) )
					p_Skeleton.skeletonData.chains.RemoveAt( p_Index );
				GUI.backgroundColor = t_BackgroundColor;
			}
			EditorGUILayout.EndHorizontal();

			if( s_ShowChainDetails[p_Index] )
			{
				EditorGUILayout.BeginVertical( "box" );//a box around the selected chain details

				EditorGUILayout.Space();
				EditorGUI.indentLevel++;

				CoreSDK.ChainType t_ChainType =
		(CoreSDK.ChainType)EditorGUILayout.EnumPopup( new GUIContent( "Chain Type", "The type of this chain." ), p_Skeleton.skeletonData.chains[ p_Index ].type );

				p_Skeleton.skeletonData.chains[p_Index].type = t_ChainType;

				p_Skeleton.skeletonData.chains[p_Index].dataSide =
					(CoreSDK.Side)EditorGUILayout.EnumPopup( new GUIContent( "Chain Side", "The side that this chain belongs to." ), p_Skeleton.skeletonData.chains[p_Index].dataSide );

				p_Skeleton.skeletonData.chains[p_Index].appliedDataType =
					(CoreSDK.ChainType)EditorGUILayout.EnumPopup( new GUIContent( "Applied Data Type", "The chain type this chain maps to in Manus Core." ), p_Skeleton.skeletonData.chains[p_Index].appliedDataType );

				EditorGUILayout.Space();

				s_ShowNodesInChain = EditorGUILayout.Foldout( s_ShowNodesInChain, "Node Ids", GUI.enabled );

				if( s_ShowNodesInChain )
				{
					EditorGUI.indentLevel++;
					EditorGUILayout.BeginVertical( "box" );//a box around the selected chain nodes

					uint t_NodeIdCount = (uint)EditorGUILayout.DelayedIntField( "Node Count", p_Skeleton.skeletonData.chains[ p_Index ].nodeIds.Count );

					GUILayout.FlexibleSpace();
					EditorGUI.indentLevel++;

					while( p_Skeleton.skeletonData.chains[p_Index].nodeIds.Count < t_NodeIdCount )
					{
						p_Skeleton.skeletonData.chains[p_Index].nodeIds.Add( 0 );
					}
					while( p_Skeleton.skeletonData.chains[p_Index].nodeIds.Count > t_NodeIdCount )
					{
						p_Skeleton.skeletonData.chains[p_Index].nodeIds.RemoveAt( p_Skeleton.skeletonData.chains[p_Index].nodeIds.Count - 1 );
					}

					for( int s = 0; s < p_Skeleton.skeletonData.chains[p_Index].nodeIds.Count; s++ )
					{
						string t_NodeName = "Empty Node";
						foreach( Node t_Node in p_Skeleton.skeletonData.nodes )
						{
							if( t_Node.id == p_Skeleton.skeletonData.chains[p_Index].nodeIds[s] && t_Node.id != 0 )
							{
								t_NodeName = t_Node.name;
							}
						}
						p_Skeleton.skeletonData.chains[p_Index].nodeIds[s] =
							(uint)EditorGUILayout.DelayedIntField( t_NodeName, (int)p_Skeleton.skeletonData.chains[p_Index].nodeIds[s] );
					}
					EditorGUI.indentLevel--;
					GUILayout.FlexibleSpace();
					EditorGUI.indentLevel--;
					EditorGUILayout.EndVertical();
				}

				p_Skeleton.skeletonData.chains[p_Index].settings.usedSettings = t_ChainType;

				s_ShowChainSettings = EditorGUILayout.Foldout( s_ShowChainSettings, "Chain Settings", GUI.enabled );
				if( s_ShowChainSettings )
				{
					EditorGUI.indentLevel++;
					DisplayChainSettings( p_Skeleton, p_Skeleton.skeletonData.chains[p_Index] );
					EditorGUI.indentLevel--;
					EditorGUILayout.Space();

				}
				EditorGUILayout.EndVertical();
				EditorGUI.indentLevel--;
			}
		}

		/// <summary>
		/// Display settings for each chain.
		/// </summary>
		/// <param name="p_Skeleton">Skeleton associated</param>
		/// <param name="p_Chain">Chain to display settings from</param>
		void DisplayChainSettings( Skeleton p_Skeleton, Chain p_Chain )
		{
			var t_ChainType = p_Chain.type;
			switch( t_ChainType )
			{
				case CoreSDK.ChainType.Invalid:
					break;
				case CoreSDK.ChainType.Arm:
					p_Chain.settings.arm.armLengthMultiplier =
						EditorGUILayout.FloatField( new GUIContent( "Arm Length Multiplier", "The length of the arm is multiplied by this value." ),
							p_Chain.settings.arm.armLengthMultiplier );
					p_Chain.settings.arm.elbowRotationOffset =
						EditorGUILayout.Slider( new GUIContent( "Elbow Rotation Offset", "The elbow has a rotational offset of this value." ),
							p_Chain.settings.arm.elbowRotationOffset, -1.0f, 1.0f );
					Vector3 t_DirAromRotOffset = EditorGUILayout.Vector3Field( new GUIContent( "Arm Rotation Offset", "The arm has a rotational offest of this value." ),
					p_Chain.settings.arm.armRotationOffset.FromManus() );
					p_Chain.settings.arm.armRotationOffset = t_DirAromRotOffset.ToManus();
					Vector3 t_ArmPositionMultiplier = EditorGUILayout.Vector3Field( new GUIContent( "Arm Position Multiplier", "The position of this arm is multiplied by this value." ),
					p_Chain.settings.arm.positionMultiplier.FromManus() );
					p_Chain.settings.arm.positionMultiplier = t_ArmPositionMultiplier.ToManus();
					Vector3 t_ArmPositionOffset = EditorGUILayout.Vector3Field( new GUIContent( "Arm Position Offset", "The position of this arm is offset by this amount." ),
					p_Chain.settings.arm.positionOffset.FromManus() );
					p_Chain.settings.arm.positionOffset = t_ArmPositionOffset.ToManus();
					break;
				case CoreSDK.ChainType.Leg:
					p_Chain.settings.leg.reverseKneeDirection =
						EditorGUILayout.Toggle( new GUIContent( "Reverse Knee Direction", "Inverse the direction in which the knee bends." ),
							p_Chain.settings.leg.reverseKneeDirection );
					p_Chain.settings.leg.kneeRotationOffset =
						EditorGUILayout.FloatField( new GUIContent( "Knee Rotation Offset", "The offset in rotation this knee has." ),
							p_Chain.settings.leg.kneeRotationOffset );
					p_Chain.settings.leg.footForwardOffset = EditorGUILayout.FloatField( new GUIContent( "Foot Forward Offset", "The offset of this foot's forward." ),
						p_Chain.settings.leg.footForwardOffset );
					p_Chain.settings.leg.footSideOffset = EditorGUILayout.FloatField( new GUIContent( "Foot Side Offset", "The offset of this foot's side." ),
						p_Chain.settings.leg.footSideOffset );
					break;
				case CoreSDK.ChainType.Neck:
					p_Chain.settings.neck.neckBendOffset = EditorGUILayout.FloatField( new GUIContent( "Neck Bend Offset", "This is the offset of this neck's bend." ),
						p_Chain.settings.neck.neckBendOffset );
					break;
				case CoreSDK.ChainType.Spine:
					p_Chain.settings.spine.spineBendOffset = EditorGUILayout.FloatField( new GUIContent( "Spine Bend Offset", "This is the offset of this spine's bend." ),
						p_Chain.settings.spine.spineBendOffset );
					break;
				case CoreSDK.ChainType.FingerIndex:
				case CoreSDK.ChainType.FingerMiddle:
				case CoreSDK.ChainType.FingerRing:
				case CoreSDK.ChainType.FingerPinky:
					p_Chain.settings.finger.useLeafAtEnd = EditorGUILayout.Toggle( new GUIContent( "Use Leaf At End", "When enabled this chain uses leaf bone adjustments." ),
						p_Chain.settings.finger.useLeafAtEnd );

					bool t_MetacarpalUsed = p_Chain.settings.finger.metacarpalBoneId != -1;
					t_MetacarpalUsed = EditorGUILayout.Toggle( new GUIContent( "Use Metacarpal Bone", "Uses finger metacarpal bone." ), t_MetacarpalUsed );
					p_Chain.settings.finger.metacarpalBoneId = t_MetacarpalUsed ? (int)p_Chain.nodeIds[0] : -1;

					p_Chain.settings.finger.handChainId = EditorGUILayout.DelayedIntField( new GUIContent( "Hand Chain ID", "The ID of the hand chain." ),
						p_Chain.settings.finger.handChainId );
					break;
				case CoreSDK.ChainType.FingerThumb:
					p_Chain.settings.finger.useLeafAtEnd = EditorGUILayout.Toggle( new GUIContent( "Use Leaf At End", "When enabled this chain uses leaf bone adjustments." ),
						p_Chain.settings.finger.useLeafAtEnd );
					p_Chain.settings.finger.handChainId = EditorGUILayout.DelayedIntField( new GUIContent( "Hand Chain ID", "The ID of the hand chain." ),
						p_Chain.settings.finger.handChainId );
					break;
				case CoreSDK.ChainType.Pelvis:
					p_Chain.settings.pelvis.hipHeight = EditorGUILayout.FloatField( new GUIContent( "Hip Height", "The height of the hip." ),
						p_Chain.settings.pelvis.hipHeight );
					p_Chain.settings.pelvis.hipBendOffset = EditorGUILayout.FloatField( new GUIContent( "Hip Bend Offset", "The offset of the hip's bend." ),
						p_Chain.settings.pelvis.hipBendOffset );
					p_Chain.settings.pelvis.thicknessMultiplier =
						EditorGUILayout.FloatField( new GUIContent( "Thickness Multiplier", "The thickness multiplier of the pelvis." ),
							p_Chain.settings.pelvis.thicknessMultiplier );
					break;
				case CoreSDK.ChainType.Head:
					p_Chain.settings.head.headPitchOffset = EditorGUILayout.FloatField( new GUIContent( "Head Pitch Offset", "The pitch offset of the head." ),
						p_Chain.settings.head.headPitchOffset );
					p_Chain.settings.head.headYawOffset = EditorGUILayout.FloatField( new GUIContent( "Head Yaw Offset", "The yaw offset of the head." ),
						p_Chain.settings.head.headYawOffset );
					p_Chain.settings.head.headTiltOffset = EditorGUILayout.FloatField( new GUIContent( "Head Tilt Offset", "The tilt offset of the head." ),
						p_Chain.settings.head.headTiltOffset );
					p_Chain.settings.head.useLeafAtEnd = EditorGUILayout.Toggle( new GUIContent( "Use Leaf At End", "When enabled this chain uses leaf bone adjustments." ),
						p_Chain.settings.head.useLeafAtEnd );
					break;
				case CoreSDK.ChainType.Shoulder:
					p_Chain.settings.shoulder.forwardOffset =
						EditorGUILayout.FloatField( new GUIContent( "Shoulder Forward Offset", "The forward offset of the shoulder." ),
							p_Chain.settings.shoulder.forwardOffset );
					p_Chain.settings.shoulder.shrugOffset = EditorGUILayout.FloatField( new GUIContent( "Shoulder Shrug Offset", "The offset of the shrug in the shoulder." ),
						p_Chain.settings.shoulder.shrugOffset );
					p_Chain.settings.shoulder.forwardMultiplier = EditorGUILayout.FloatField(
						new GUIContent( "Shoulder Forward Multiplier", "The multiplier for the shoulder's forward." ),
						p_Chain.settings.shoulder.forwardMultiplier );
					p_Chain.settings.shoulder.shrugMultiplier = EditorGUILayout.FloatField(
						new GUIContent( "Shoulder Shrug Multiplier", "The multiplier for the shoulder's shrug." ),
						p_Chain.settings.shoulder.shrugMultiplier );
					break;
				case CoreSDK.ChainType.Hand:

					EditorGUILayout.LabelField( "Hand Motion Type: " + p_Chain.settings.hand.handMotion.ToString(), EditorStyles.label );

					List<int> t_FingerIds = new List<int>( p_Chain.settings.hand.fingerChainIds );

					s_ShowFingersInHand = EditorGUILayout.Foldout( s_ShowFingersInHand, "Fingers In Hand", GUI.enabled );
					if( s_ShowFingersInHand )
					{
						EditorGUI.indentLevel++;

						uint t_FingerIdCount = (uint)EditorGUILayout.DelayedIntField( new GUIContent( "Finger Count", "The amount of finger chains in this hand." ), t_FingerIds.Count );
						EditorGUI.indentLevel++;

						while( t_FingerIds.Count < t_FingerIdCount )
						{
							t_FingerIds.Add( 0 );
						}
						while( t_FingerIds.Count > t_FingerIdCount )
						{
							t_FingerIds.RemoveAt( t_FingerIds.Count - 1 );
						}
						p_Chain.settings.hand.fingerChainIds = t_FingerIds.ToArray();

						EditorGUILayout.Space();
						for( int s = 0; s < p_Chain.settings.hand.fingerChainIds.Length; s++ )
						{
							string t_ChainName = "Finger Chain ID";
							foreach( Chain t_Chain in p_Skeleton.skeletonData.chains )
							{
								if( t_Chain.id == p_Chain.settings.hand.fingerChainIds[s] )
								{
									t_ChainName = t_Chain.name;
								}
							}
							p_Chain.settings.hand.fingerChainIds[s] =
								(int)EditorGUILayout.DelayedIntField( new GUIContent( t_ChainName, "The ID of a finger in this hand." ),
									(int)p_Chain.settings.hand.fingerChainIds[s] );
						}
						EditorGUI.indentLevel--;
						EditorGUI.indentLevel--;
					}
					break;
				case CoreSDK.ChainType.Foot:
					List<int> t_ToeIds = new List<int>( p_Chain.settings.foot.toeChainIds );

					s_ShowToesInFoot = EditorGUILayout.Foldout( s_ShowToesInFoot, "Toes In Foot", GUI.enabled );
					if( s_ShowToesInFoot )
					{
						EditorGUI.indentLevel++;

						uint t_ToeIdCount = (uint)EditorGUILayout.DelayedIntField( new GUIContent( "Toe Count", "The amount of toes in this foot." ), t_ToeIds.Count );
						EditorGUI.indentLevel++;

						while( t_ToeIds.Count < t_ToeIdCount )
						{
							t_ToeIds.Add( 0 );
						}
						while( t_ToeIds.Count > t_ToeIdCount )
						{
							t_ToeIds.RemoveAt( t_ToeIds.Count - 1 );
						}
						p_Chain.settings.foot.toeChainIds = t_ToeIds.ToArray();

						EditorGUILayout.Space();
						for( int s = 0; s < p_Chain.settings.foot.toeChainIds.Length; s++ )
						{
							string t_ChainName = "Toe Chain ID";
							foreach( Chain t_Chain in p_Skeleton.skeletonData.chains )
							{
								if( t_Chain.id == p_Chain.settings.foot.toeChainIds[s] )
								{
									t_ChainName = t_Chain.name;
								}
							}
							p_Chain.settings.foot.toeChainIds[s] =
								(int)EditorGUILayout.DelayedIntField( new GUIContent( t_ChainName, "The ID of a toe chain in this foot." ),
									(int)p_Chain.settings.foot.toeChainIds[s] );
						}
						EditorGUI.indentLevel--;
						EditorGUI.indentLevel--;
					}
					EditorGUILayout.Space();
					break;
				case CoreSDK.ChainType.Toe:
					p_Chain.settings.toe.footChainId = EditorGUILayout.DelayedIntField( new GUIContent( "Foot Chain ID", "The chain ID of the foot chain used by this toe." ),
						p_Chain.settings.toe.footChainId );
					p_Chain.settings.toe.useLeafAtEnd = EditorGUILayout.Toggle( new GUIContent( "Use Leaf At End", "When enabled this chain uses leaf bone adjustments." ),
						p_Chain.settings.toe.useLeafAtEnd );
					break;
			}
		}
	}
}
