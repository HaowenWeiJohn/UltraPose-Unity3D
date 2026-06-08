/*
Copyright 2015 Pim de Witte All Rights Reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

/// Modified version of UnityMainThreadDispacther 
/// Author: Pim de Witte (pimdewitte.com) and contributors, https://github.com/PimDeWitte/UnityMainThreadDispatcher
/// <summary>
/// A thread-safe class which holds a queue with actions to execute on the next Update() method. It can be used to make calls to the main thread for
/// things such as UI Manipulation in Unity. It was developed for use in combination with the Firebase Unity plugin, which uses separate threads for event handling
/// </summary>
namespace Manus.Utility
{
	public class UnityMainThreadDispatcher : MonoBehaviour
	{
		private object m_QueueLock = new object();

		private static Queue<Action> s_ExecutionQueue = new Queue<Action>();

		/// <summary>
		/// Make persistent through scenes.
		/// </summary>
		private void Awake()
		{
			if( s_Instance == null )
			{
				s_Instance = this;
				DontDestroyOnLoad( this.gameObject );
			}
		}

		/// <summary>
		/// Destroy references.
		/// </summary>
		private void OnDestroy()
		{
			s_Instance = null;
		}

		/// <summary>
		/// Go through execution queue.
		/// </summary>
		private void Update()
		{
			Queue<Action> t_Queue;
			lock( m_QueueLock )
			{
				t_Queue = s_ExecutionQueue;
				s_ExecutionQueue = new Queue<Action>();
			}
			if( t_Queue !=null)
			{
				while( t_Queue.Count > 0 )
				{
					t_Queue.Dequeue()?.Invoke();
				}
			}
		}

		/// <summary>
		/// Locks the queue and adds the IEnumerator to the queue.
		/// </summary>
		/// <param name="p_Action">IEnumerator function that will be executed from the main thread.</param>
		public void Enqueue( IEnumerator p_Action )
		{
			lock( s_ExecutionQueue )
			{
				s_ExecutionQueue.Enqueue( () =>
				{
					StartCoroutine( p_Action );
				} );
			}
		}

		/// <summary>
		/// Locks the queue and adds the Action to the queue.
		/// </summary>
		/// <param name="p_Action">function that will be executed from the main thread.</param>
		public void Enqueue( Action p_Action )
		{
			Enqueue( ActionWrapper( p_Action ) );
		}

		/// <summary>
		/// Locks the queue and adds the Action to the queue, returning a Task which is completed when the action completes.
		/// </summary>
		/// <param name="p_Action">function that will be executed from the main thread.</param>
		/// <returns>A Task that can be awaited until the action completes</returns>
		public Task EnqueueAsync( Action p_Action )
		{
			TaskCompletionSource<bool> t_Source = new TaskCompletionSource<bool>();

			void WrappedAction()
			{
				try
				{
					p_Action();
					t_Source.TrySetResult( true );
				}
				catch( Exception p_Exception )
				{
					t_Source.TrySetException( p_Exception );
				}
			}

			Enqueue( ActionWrapper( WrappedAction ) );
			return t_Source.Task;
		}

		private IEnumerator ActionWrapper( Action p_Action )
		{
			p_Action();
			yield return null;
		}

		public static UnityMainThreadDispatcher instance
		{
			get
			{
				return s_Instance;
			}
		}
		private static UnityMainThreadDispatcher s_Instance = null;

		/// <summary>
		/// Find or create UnityMainThreadDispatcher.
		/// </summary>
		public static void Initalize()
		{
			if( s_Instance == null )
			{
				s_Instance = Manus.Utility.ComponentUtil.FindOrInstantiateComponent<UnityMainThreadDispatcher>();
			}
		}
	}
}
