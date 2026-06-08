using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Manus.InteractionScene
{
	/// <summary>
	/// This class is used in the Demo to reset interactable objects to their original positions.
	/// This code is purely demonstrational and probably does not have much use outside this specific scenario.
	/// </summary>
	[AddComponentMenu("Manus/Interaction Scene/Resettable Interactable")]
	public class ResettableInteractable : MonoBehaviour
	{
		#region Fields & Properties

		#region Public Fields
		public Interaction.PushButton pushButton = null;
		#endregion // Public Fields

		#region Protected Fields
		protected Vector3 m_InitialPosition = Vector3.zero;
		protected Quaternion m_InitialRotation = Quaternion.identity;

		protected Rigidbody m_RigidBody = null;
		#endregion // Protected Fields

		#endregion // Fields & Properties

		#region Methods

		#region Unity Messages

		/// <summary>
		/// Check for pushbutton script refrence and save initial position.
		/// </summary>
		protected void Awake()
		{
			if (pushButton == null)
			{
				Debug.LogWarning($"No PushButton was given. This script needs one to function.");
				enabled = false;

				return;
			}

			m_RigidBody = GetComponent<Rigidbody>();
			if (m_RigidBody == null)
			{
				Debug.LogWarning($"No RigidBody was found. This script needs one to function.");
				enabled = false;

				return;
			}

			m_InitialPosition = transform.position;
			m_InitialRotation = transform.rotation;
		}

		/// <summary>
		/// Add self to push button on pressed interaction.
		/// </summary>
		protected virtual void OnEnable()
		{
			if (pushButton)
			{
				pushButton.onPressed += ReactToPushButtonEnabled;
			}
		}

		/// <summary>
		/// Remove self from push button on pressed interaction.
		/// </summary>
		protected virtual void OnDisable()
		{
			if (pushButton)
			{
				pushButton.onPressed -= ReactToPushButtonEnabled;
			}
		}

		#endregion // Unity Messages

		#region Protected Methods

		/// <summary>
		/// Reset position, rotation to initial and zero velocity.
		/// </summary>
		/// <param name="p_Button">Button trigger</param>
		protected void ReactToPushButtonEnabled(Interaction.PushButton p_Button)
		{
			transform.position = m_InitialPosition;
			transform.rotation = m_InitialRotation;

#if UNITY_2023_1_OR_NEWER
			m_RigidBody.linearVelocity = Vector3.zero;
#else
			m_RigidBody.velocity = Vector3.zero;
#endif
			m_RigidBody.angularVelocity = Vector3.zero;
		}

		/// <summary>
		/// Called when push button is disabled.
		/// </summary>
		/// <param name="p_Button">Button trigger</param>
		protected void ReactToPushButtonDisabled(Interaction.PushButton p_Button)
		{

		}

#endregion // Protected Methods

#endregion // Methods
	}
}
