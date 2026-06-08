using System.Collections.Generic;
using System.Linq;

using Manus.Skeletons;

using UnityEngine;

namespace Manus.Utility
{
	public static class GizmoShapes
	{
		public static void DrawBox( Vector3 p_Position, Quaternion p_Rotation, Vector3 p_Size )
		{
			Gizmos.matrix = Matrix4x4.TRS( p_Position, p_Rotation, Vector3.one );
			Gizmos.DrawWireCube( Vector3.forward * p_Size.z / 2f, p_Size );
			Gizmos.matrix = Matrix4x4.identity;
		}

		public static void DrawCapsule( Vector3 p_Start, Quaternion p_Rotation, float p_Length, float p_Radius )
		{
			Vector3 t_End = p_Start + p_Rotation * Vector3.forward * p_Length;
			DrawSlice( p_Start, p_Rotation, p_Radius );
			DrawSlice( t_End, p_Rotation, p_Radius );

			DrawCap( p_Start, p_Rotation, p_Radius, true );
			DrawCap( t_End, p_Rotation, p_Radius, false );

			ConnectSlices( p_Start, p_Rotation, t_End, p_Radius );
		}

		public static void DrawSphere( Vector3 p_Position, Quaternion p_Rotation, float p_Radius )
		{
			DrawSlice( p_Position, p_Rotation, p_Radius );
			DrawCap( p_Position, p_Rotation, p_Radius, true );
			DrawCap( p_Position, p_Rotation, p_Radius, false );
		}

		#region Helper

		private static void DrawSlice( Vector3 p_Position, Quaternion p_Rotation, float p_Radius )
		{
			// Draw cross
			Gizmos.DrawRay( p_Position, p_Rotation * Vector3.up * p_Radius );
			Gizmos.DrawRay( p_Position, p_Rotation * Vector3.right * p_Radius );
			Gizmos.DrawRay( p_Position, p_Rotation * -Vector3.up * p_Radius );
			Gizmos.DrawRay( p_Position, p_Rotation * -Vector3.right * p_Radius );

			// Draw circle
			int t_NumberOfPoints = 50;
			float t_DegreesPerPoint = 360f / t_NumberOfPoints;
			List<Vector3> t_Points = new List<Vector3>();

			for( int i = 1; i < t_NumberOfPoints; i++ )
			{
				//Vector2 t_Distance = CollisionMath.GetDistanceToCenter(i * t_DegreesPerPoint, p_Extends);
				t_Points.Add( p_Position + p_Rotation * Quaternion.AngleAxis( t_DegreesPerPoint * i, Vector3.forward ) * Vector3.up * p_Radius );
			}

			for( int i = 0; i < t_Points.Count; i++ )
			{
				Vector3 t_Point1 = t_Points[i];
				Vector3 t_Point2 = t_Points[0];

				if( i != t_Points.Count - 1 )
					t_Point2 = t_Points[ i + 1 ];

				Gizmos.DrawLine( t_Point1, t_Point2 );
			}
		}

		private static void DrawCap( Vector3 p_Position, Quaternion p_Rotation, float p_Radius, bool p_Start )
		{
			// Draw circle
			int t_NumberOfPoints = 25;
			float t_DegreesPerPoint = 180f / (t_NumberOfPoints - 1);
			List<Vector3> t_Points = new List<Vector3>();

			for( int i = 0; i < t_NumberOfPoints; i++ )
			{
				t_Points.Add( p_Position + p_Rotation * Quaternion.AngleAxis( t_DegreesPerPoint * i, Vector3.right ) * Vector3.up * p_Radius * (p_Start ? -1f : 1f) );
			}

			for( int i = 1; i < t_Points.Count; i++ )
			{
				Vector3 t_Point1 = t_Points[i];
				Vector3 t_Point2 = t_Points[i - 1];

				Gizmos.DrawLine( t_Point1, t_Point2 );
			}

			// Draw circle
			t_Points = new List<Vector3>();

			for( int i = 0; i < t_NumberOfPoints; i++ )
			{
				t_Points.Add( p_Position + p_Rotation * Quaternion.AngleAxis( t_DegreesPerPoint * i, Vector3.up ) * -Vector3.right * p_Radius * (p_Start ? -1f : 1f) );
			}

			for( int i = 1; i < t_Points.Count; i++ )
			{
				Vector3 t_Point1 = t_Points[i];
				Vector3 t_Point2 = t_Points[i - 1];

				Gizmos.DrawLine( t_Point1, t_Point2 );
			}
		}

		private static void ConnectSlices( Vector3 p_StartPosition, Quaternion p_StartRotation, Vector3 p_EndPosition, float p_Radius )
		{
			Vector3 t_StartCenter = p_StartPosition;
			Vector3 t_EndCenter = p_EndPosition;

			Gizmos.DrawLine( t_StartCenter + p_StartRotation * Vector3.up * p_Radius, t_EndCenter + p_StartRotation * Vector3.up * p_Radius );
			Gizmos.DrawLine( t_StartCenter + p_StartRotation * Vector3.right * p_Radius, t_EndCenter + p_StartRotation * Vector3.right * p_Radius );
			Gizmos.DrawLine( t_StartCenter + p_StartRotation * -Vector3.up * p_Radius, t_EndCenter + p_StartRotation * -Vector3.up * p_Radius );
			Gizmos.DrawLine( t_StartCenter + p_StartRotation * -Vector3.right * p_Radius, t_EndCenter + p_StartRotation * -Vector3.right * p_Radius );
		}

		#endregion
	}
}
