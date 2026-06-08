using System.Collections.Generic;

using UnityEngine;

namespace Manus.InteractionScene
{
	/// <summary>
	/// This class is used in the Demo to demonstrate interaction between all the interactables and other objects.
	/// This code is purely demonstrational and probably does not have much use outside this specific scenario.
	/// </summary>
	[AddComponentMenu( "Manus/Interaction Scene/Object Influenced By Values" )]
	public class ObjectInfluencedByValues : MonoBehaviour
	{
		[System.Serializable]
		public class Influencer
		{
			public enum InfluencerType
			{
				Translate,
				Rotate,
				Scale
			}
			public MonoBehaviour objectWithValue = null;
			public InfluencerType type;
			public Vector3 amount;
			public bool useNormalized = true;
		}


		#region Fields & Properties

		#region Public Properties

		public List<Influencer> influencers;

		#endregion // Public Properties

		#region Protected Variables
		protected Vector3 m_InitialLocalPosition = Vector3.zero;
		protected Vector3 m_InitialLocalRotation = Vector3.zero;
		protected Vector3 m_InitialLocalScale = Vector3.zero;

		#endregion // Protected Variables

		#endregion // Fields & Properties

		#region Methods

		#region Unity Messages

		protected virtual void Awake()
		{
			m_InitialLocalPosition = transform.localPosition;
			m_InitialLocalRotation = transform.localRotation.eulerAngles;
			m_InitialLocalScale = transform.localScale;
		}

		protected virtual void FixedUpdate()
		{
			Vector3 t_LPosition = m_InitialLocalPosition;
			Vector3 t_LRotation = m_InitialLocalRotation;
			Vector3 t_LScale = m_InitialLocalScale;

			for( int i = 0; i < influencers.Count; i++ )
			{
				var t_IVal = influencers[i].objectWithValue.GetComponent<Interaction.IValue>();
				if( t_IVal == null ) continue;
				float t_Val = t_IVal.value;
				if( influencers[i].useNormalized )
				{
					t_Val = t_IVal.normalizedValue;
				}
				switch( influencers[i].type )
				{
					case Influencer.InfluencerType.Translate:
						t_LPosition += t_Val * influencers[i].amount;
						break;
					case Influencer.InfluencerType.Rotate:
						t_LRotation += t_Val * influencers[i].amount;
						break;
					case Influencer.InfluencerType.Scale:
						t_LScale += t_Val * influencers[i].amount;
						break;
				}
			}

			transform.localPosition = t_LPosition;
			transform.localRotation = Quaternion.Euler( t_LRotation );
			transform.localScale = t_LScale;
		}

		#endregion // Unity Messages

		#endregion // Methods
	}
}
