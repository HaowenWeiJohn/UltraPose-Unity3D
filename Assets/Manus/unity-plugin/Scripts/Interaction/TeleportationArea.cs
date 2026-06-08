using UnityEngine;
using UnityEngine.UIElements;

namespace Manus.Interaction
{
	public class TeleportationArea : MonoBehaviour
    {
        public BoxCollider m_Area;

        private void OnEnable()
        {
            m_Area = GetComponent<BoxCollider>();
        }

        private void OnDrawGizmos()
        {
            if(m_Area)
            {
                Vector3 t_Scale = m_Area.size;
                t_Scale.Scale(transform.lossyScale);
				var t_Col = Gizmos.color;
				Gizmos.color = Color.red;
				Gizmos.matrix = Matrix4x4.TRS( transform.position + m_Area.center, transform.rotation, Vector3.one );
				Gizmos.DrawWireCube( Vector3.zero, t_Scale );
				Gizmos.matrix = Matrix4x4.identity;
				Gizmos.color = t_Col;
			}
		}
    }
}
