using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using AOT;

namespace Manus
{
	public partial class CoreSDK
	{
		private const CallingConvention s_ImportCallingConvention = CallingConvention.Cdecl;
		private const CharSet s_ImportCharSet = CharSet.Ansi;

		private const string s_DLLName = "ManusSDK";
		private const int s_NumFingersOnHand = 5;
		private const int s_NumFlexSegmentsPerFinger = 2;
		private const int s_MaxNumImusOnGlove = s_NumFingersOnHand + 1;
		private const int s_MaxUsers = 16;
		private const int s_MaxNumCharsInUsername = 64;

		private const int s_MaxNumberOfCharsInMeasurement = 64;
		private const int s_MaxNumberOfCharsInHostName = 256;
		private const int s_MaxNumberOfCharsinIpAddress = 40;
		private const int s_MaxNumberOfCharsInTrackerId = 32;
		private const int s_MaxNumberOfCharsInTrackerManufacturer = 32;
		private const int s_MaxNumberOfCharsInTrackerProductName = 32;
		private const int s_MaxNumberOfCharsInTargetId = 32;
		private const int s_MaxNumberOfCharsInVersion = 16;

		private const uint s_UnitialisedId = 0;
		private const int s_MaxNumberOfHosts = 100;
		private const int s_MaxNumberOfDongles = 16;
		private const int s_MaxNumCharsInLicenseType = 64;
		private const int s_MaxNumberOfGloves = s_MaxNumberOfDongles * 2;
		private const int s_MaxNumberOfHapticDongles = s_MaxNumberOfDongles;
		private const int s_MaxNumberOfSkeletons = s_MaxNumberOfDongles;
		private const int s_MaxNumberPolygonUsers = s_MaxNumberOfSkeletons;
		private const int s_NumberOfTrackersPerPolygonSkeleton = 8;
		private const int s_MaxNumberOfTrackers = s_MaxNumberOfSkeletons * s_NumberOfTrackersPerPolygonSkeleton;

		private const int s_MaxBoneWeightsPerVertex = 4;

		private const int s_MaxNumCharsInNodeName = 256; // this is for a UTF8 string , NOT an ASCII CHAR array (same base type though)
		private const int s_MaxChainLength = 32;
		private const int s_MaxFingerIDS = 10;
		private const int s_MaxToeIDS = 10;

		private const int s_MaxNumCharsInSystemErrorMessage = 256;
		private const int s_MaxNumCharDebuggingID = 64;
		private const int s_MaxNumberOfErgonomicsData = s_MaxNumberOfGloves;

		private const int s_MaxChainsNodes = 999; //remove?

		private const int s_MaxNumberOfSessions = 8; // this is not the real limit for Core but just for the SDKClient to handle
		private const int s_MaxNumberOfSkeletonsPerSession = 16;
		private const int s_MaxNumCharsInSkeletonName = 256; // we already encountered 34 char names in unreal, but its utf8 so enbiggen even more!

		private const int s_MaxNumCharsInTimecodeInterfaceStrings = 64;
		private const int s_MaxNumberOfTimecodeInterfaces = 32;

		private const int s_MaxNumCharsInSessionName = 256;

		private const int s_MaxNumCharsInCommandResponse = 256;

		public static List<int> listOfFunctions;

		private static uint s_UninitalisedId = 0;

		public delegate void OnConnectedToCore( ManusHost p_Host );
		public delegate void OnConnectedToCorePtr( IntPtr p_HostPtr );
		public delegate void OnDisconnectFromCore( ManusHost p_Host );
		public delegate void OnDisconnectFromCorePtr( IntPtr p_HostPtr );

		public delegate void SkeletonStreamCallback( SkeletonStream p_SkeletonStream );
		public delegate void SkeletonStreamCallbackPtr( IntPtr p_SkeletonStreamPtr );
		protected delegate void InternalSkeletonStreamCallback( SkeletonStreamInfo p_SkeletonStream );
		protected delegate void InternalSkeletonStreamCallbackPtr( IntPtr p_SkeletonStreamPtr );

		public delegate void TrackerStreamCallback( TrackerStream p_TrackerStream );
		public delegate void TrackerStreamCallbackPtr( IntPtr p_TrackerStreamPtr );
		protected delegate void InternalTrackerStreamCallback( InternalTrackerStreamInfo p_TrackerStream );
		protected delegate void InternalTrackerStreamCallbackPtr( IntPtr p_TrackerStreamPtr );

		public delegate void RawSkeletonStreamCallback( RawSkeletonStream p_SkeletonStream );
		public delegate void RawSkeletonStreamCallbackPtr( IntPtr p_SkeletonStreamPtr );
		protected delegate void InternalRawSkeletonStreamCallback( SkeletonStreamInfo p_SkeletonStream );
		protected delegate void InternalRawSkeletonStreamCallbackPtr( IntPtr p_SkeletonStreamPtr );

		public delegate void LandscapeStreamCallback( Landscape p_LandscapeData );
		public delegate void LandscapeStreamCallbackPtr( IntPtr p_LandscapeDataPtr );

		public delegate void ErgonomicsStreamCallback( ErgonomicsStream p_ErgonomicsData );
		public delegate void ErgonomicsStreamCallbackPtr( IntPtr p_ErgonomicsDataPtr );

		public delegate void SystemStreamCallback( SystemMessage p_SystemData );
		public delegate void SystemStreamCallbackPtr( IntPtr p_SystemDataPtr );

		public delegate void GestureStreamCallback( GestureStream p_GestureStream );
		public delegate void GestureStreamCallbackPtr( IntPtr p_GestureStreamPtr );
		protected delegate void InternalGestureStreamCallback( GestureStreamInfo p_GestureStream );
		protected delegate void InternalGestureStreamCallbackPtr( IntPtr p_GestureStreamPtr );
		
		public delegate void OnRawDeviceData( RawData p_RawDeviceData );
		public delegate void OnInternalRawDeviceData( RawDeviceDataInfo p_RawDeviceDataInfo );


		static OnConnectedToCore m_OnConnectedToCore = null;
		static OnDisconnectFromCore m_OnDisconnectFromCore = null;
		static SkeletonStreamCallback m_OnSkeletonData = null;
		static TrackerStreamCallback m_OnTrackerData = null;
		static RawSkeletonStreamCallback m_OnRawSkeletonData = null;
		static LandscapeStreamCallback m_OnLandscape = null;
		static ErgonomicsStreamCallback m_OnErgonomics = null;
		static SystemStreamCallback m_OnSystem = null;
		static GestureStreamCallback m_OnGestureData = null;
		static OnRawDeviceData m_OnRawDeviceData = null;

		#region Wrapper startup and shutdown.

		public static SDKReturnCode Initialize( SessionType p_SessionType )
		{
			var t_Res = ManusDLLImport.CoreSdk_InitializeCore();
			if( t_Res != SDKReturnCode.Success ) return t_Res;
	
			t_Res = ManusDLLImport.CoreSdk_SetSessionType( p_SessionType );
			if( t_Res != SDKReturnCode.Success ) return t_Res;

			CoordinateSystemVUH t_VUH = new CoordinateSystemVUH()
			{
				handedness = Side.Left,
				up = AxisPolarity.PositiveY,
				view = AxisView.ZFromViewer,
				unitScale = 1.0f
			};
			return ManusDLLImport.CoreSdk_InitializeCoordinateSystemWithVUH( t_VUH, false );
		}

		public static SDKReturnCode ShutDown()
		{
			return ManusDLLImport.CoreSdk_ShutDown();
		}

		#endregion

		#region Utility functions

		public static SDKReturnCode WasDllBuiltInDebugConfiguration( out bool p_BuiltInDebug )
		{
			p_BuiltInDebug = false;
			return ManusDLLImport.CoreSdk_WasDllBuiltInDebugConfiguration( out p_BuiltInDebug );
		}

		public static SDKReturnCode GetTimestampInfo( ManusTimestamp p_Timestamp, out ManusTimestampInfo p_Info )
		{
			return ManusDLLImport.CoreSdk_GetTimestampInfo( p_Timestamp, out p_Info );
		}

		public static SDKReturnCode SetTimestampInfo( out ManusTimestamp p_Timestamp, ManusTimestampInfo p_Info )
		{
			return ManusDLLImport.CoreSdk_SetTimestampInfo( out p_Timestamp, p_Info );
		}

		#endregion

		#region Connection handling

		public static SDKReturnCode LookForHosts( uint p_WaitSeconds = 1, bool p_LoopbackOnly = false )
		{
			return ManusDLLImport.CoreSdk_LookForHosts( p_WaitSeconds, p_LoopbackOnly );
		}

		public static SDKReturnCode GetNumberOfAvailableHostsFound( out uint p_NumberOfAvailableHostsFound )
		{
			p_NumberOfAvailableHostsFound = 0;
			return ManusDLLImport.CoreSdk_GetNumberOfAvailableHostsFound( out p_NumberOfAvailableHostsFound );
		}

		public static SDKReturnCode GetAvailableHostsFound( out ManusHost[] p_Hosts, uint p_NumberOfHostsThatFitInArray )
		{
			p_Hosts = new ManusHost[p_NumberOfHostsThatFitInArray];
			return ManusDLLImport.CoreSdk_GetAvailableHostsFound( p_Hosts, p_NumberOfHostsThatFitInArray );
		}

		public static SDKReturnCode GetIsConnectedToCore( out bool p_ConnectedToCore )
		{
			p_ConnectedToCore = false;
			return ManusDLLImport.CoreSdk_GetIsConnectedToCore( out p_ConnectedToCore );
		}

		public static SDKReturnCode ConnectGRPC()
		{
			return ManusDLLImport.CoreSdk_ConnectGRPC();
		}

		public static SDKReturnCode ConnectToHost( ManusHost p_Host )
		{
			return ManusDLLImport.CoreSdk_ConnectToHost( p_Host );
		}

		public static SDKReturnCode GetVersionsAndCheckCompatibility( out ManusVersion p_SdkVersion, out ManusVersion p_CoreVersion,
			out bool p_AreVersionsCompatible )
		{
			p_SdkVersion.versionInfo = "";
			p_CoreVersion.versionInfo = "";
			p_AreVersionsCompatible = false;

			return ManusDLLImport.CoreSdk_GetVersionsAndCheckCompatibility( out p_SdkVersion, out p_CoreVersion, out p_AreVersionsCompatible );
		}

		public static SDKReturnCode RegisterCallbackForOnConnectedToCore( OnConnectedToCore p_OnConnectedToCoreCallback )
		{
			//if( m_OnConnectedToCore == null )
			{
				var t_Res = ManusDLLImport.CoreSdk_RegisterCallbackForOnConnect( ProcessInternalCallbackForOnConnectedToCore );
				if( t_Res != SDKReturnCode.Success ) return t_Res;
			}
			m_OnConnectedToCore = p_OnConnectedToCoreCallback;
			return SDKReturnCode.Success;
		}

		[MonoPInvokeCallback( typeof( OnConnectedToCorePtr ) )]
		private static void ProcessInternalCallbackForOnConnectedToCore( IntPtr p_HostPtr )
		{
			m_OnConnectedToCore( Marshal.PtrToStructure<ManusHost>( p_HostPtr ) );
		}

		public static SDKReturnCode RegisterCallbackForOnDisconnectedFromCore( OnDisconnectFromCore p_OnDisconnectedFromCoreCallback )
		{
			//if( m_OnDisconnectFromCore == null )
			{
				var t_Res = ManusDLLImport.CoreSdk_RegisterCallbackForOnDisconnect( ProcessInternalCallbackForOnDisconnectedFromCore );
				if( t_Res != SDKReturnCode.Success ) return t_Res;
			}
			m_OnDisconnectFromCore = p_OnDisconnectedFromCoreCallback;
			return SDKReturnCode.Success;
		}

		[MonoPInvokeCallback( typeof( OnDisconnectFromCorePtr ) )]
		private static void ProcessInternalCallbackForOnDisconnectedFromCore( IntPtr p_HostPtr )
		{
			m_OnDisconnectFromCore( Marshal.PtrToStructure<ManusHost>( p_HostPtr ) );
		}

		public static SDKReturnCode RegisterCallbackForLandscapeStream( LandscapeStreamCallback p_LandscapeCallback )
		{
			//if( m_OnLandscape == null )
			{
				var t_Res = ManusDLLImport.CoreSdk_RegisterCallbackForLandscapeStream( ProcessInternalCallbackForLandscapeStream );
				if( t_Res != SDKReturnCode.Success ) return t_Res;
			}
			m_OnLandscape = p_LandscapeCallback;
			return SDKReturnCode.Success;
		}

		[MonoPInvokeCallback( typeof( LandscapeStreamCallbackPtr ) )]
		private static void ProcessInternalCallbackForLandscapeStream( IntPtr p_LandscapeDataPtr )
		{
			m_OnLandscape( Marshal.PtrToStructure<Landscape>( p_LandscapeDataPtr ) );
		}

		public static SDKReturnCode RegisterCallbackForSystemStream( SystemStreamCallback p_OnSystem )
		{
			//if( m_OnSystem == null )
			{
				var t_Res = ManusDLLImport.CoreSdk_RegisterCallbackForSystemStream( ProcessInternalCallbackForSystemStream );
				if( t_Res != SDKReturnCode.Success ) return t_Res;
			}
			m_OnSystem = p_OnSystem;
			return SDKReturnCode.Success;
		}

		[MonoPInvokeCallback( typeof( SystemStreamCallbackPtr ) )]
		private static void ProcessInternalCallbackForSystemStream( IntPtr p_SystemDataPtr )
		{
			m_OnSystem( Marshal.PtrToStructure<SystemMessage>( p_SystemDataPtr ) );
		}

		public static SDKReturnCode RegisterCallbackForErgonomicsStream( ErgonomicsStreamCallback p_OnErgonomics )
		{
			//if( m_OnSystem == null )
			{
				var t_Res = ManusDLLImport.CoreSdk_RegisterCallbackForErgonomicsStream( ProcessInternalCallbackForErgonomicsStream );
				if( t_Res != SDKReturnCode.Success ) return t_Res;
			}
			m_OnErgonomics = p_OnErgonomics;
			return SDKReturnCode.Success;
		}

		[MonoPInvokeCallback( typeof( ErgonomicsStreamCallbackPtr ) )]
		private static void ProcessInternalCallbackForErgonomicsStream( IntPtr p_ErgonomicsDataPtr )
		{
			m_OnErgonomics( Marshal.PtrToStructure<ErgonomicsStream>( p_ErgonomicsDataPtr ) );
		}

		#endregion

		#region Basic glove interactions

		public static SDKReturnCode PairGlove( uint p_GloveId, out bool p_Success )
		{
			return ManusDLLImport.CoreSdk_PairGlove( p_GloveId, out p_Success );
		}

		public static SDKReturnCode PairGloveToDongle( uint p_GloveId, uint p_DongleId, out bool p_Success )
		{
			return ManusDLLImport.CoreSdk_PairGloveToDongle( p_GloveId, p_DongleId, out p_Success );
		}

		public static SDKReturnCode UnpairGlove( uint p_GloveId, out bool p_Success )
		{
			return ManusDLLImport.CoreSdk_UnpairGlove( p_GloveId, out p_Success );
		}

		public static SDKReturnCode UpdateFirmware( uint p_DeviceId, out bool p_Result, out string p_Response )
		{
			StringBuilder t_ResponseBuilder = new StringBuilder(s_MaxNumCharsInCommandResponse);
			var t_Res = ManusDLLImport.CoreSdk_UpdateFirmware( p_DeviceId, out p_Result, t_ResponseBuilder );
			p_Response = t_ResponseBuilder.ToString();

			return t_Res;
		}

		public static SDKReturnCode SetLicense( uint p_DongleId, string p_LicenseString, out bool p_Result, out string p_Response )
		{
			StringBuilder t_ResponseBuilder = new StringBuilder(s_MaxNumCharsInCommandResponse);
			var t_Res = ManusDLLImport.CoreSdk_SetLicense( p_DongleId, p_LicenseString, (uint)p_LicenseString.Length, out p_Result, t_ResponseBuilder );
			p_Response = t_ResponseBuilder.ToString();

			return t_Res;
		}

		public static SDKReturnCode VibrateWristOfGlove( uint p_GloveId, float p_UnitStrength, ushort p_DurationInMilliseconds )
		{
			return ManusDLLImport.CoreSdk_VibrateWristOfGlove( p_GloveId, p_UnitStrength, p_DurationInMilliseconds );
		}

		public static SDKReturnCode VibrateFingers( uint p_DongleId, Side p_HandType, float[] p_Powers )
		{
			return ManusDLLImport.CoreSdk_VibrateFingers( p_DongleId, p_HandType, p_Powers );
		}

		public static SDKReturnCode VibrateFingersForGlove( uint p_GloveID, float[] p_Powers )
		{
			return ManusDLLImport.CoreSdk_VibrateFingersForGlove( p_GloveID, p_Powers );
		}

		public static SDKReturnCode VibrateFingersForSkeleton( uint p_SkeletonId, Side p_HandType, float[] p_Powers )
		{
			return ManusDLLImport.CoreSdk_VibrateFingersForSkeleton( p_SkeletonId, p_HandType, p_Powers );
		}

		public static SDKReturnCode GetGloveIdOfUser_UsingUserId( uint p_UserId, Side p_HandType, out uint p_GloveId )
		{
			p_GloveId = 0;
			return ManusDLLImport.CoreSdk_GetGloveIdOfUser_UsingUserId( p_UserId, p_HandType, out p_GloveId );
		}

		public static SDKReturnCode GetNumberOfAvailableGloves( out uint p_NumberOfAvailableGloves )
		{
			p_NumberOfAvailableGloves = 0;
			return ManusDLLImport.CoreSdk_GetNumberOfAvailableGloves( out p_NumberOfAvailableGloves );
		}

		public static SDKReturnCode GetIdsOfAvailableGloves( out uint[] p_IdsOfAvailableGloves, uint p_NumberOfIdsThatFitInArray )
		{
			p_IdsOfAvailableGloves = new uint[p_NumberOfIdsThatFitInArray];
			return ManusDLLImport.CoreSdk_GetIdsOfAvailableGloves( p_IdsOfAvailableGloves, p_NumberOfIdsThatFitInArray );
		}

		public static SDKReturnCode GetGlovesForDongle( uint p_DongleId, out uint p_LeftGloveId, out uint p_RightGloveId )
		{
			p_LeftGloveId = s_UninitalisedId;
			p_RightGloveId = s_UninitalisedId;
			return ManusDLLImport.CoreSdk_GetGlovesForDongle( p_DongleId, out p_LeftGloveId, out p_RightGloveId );
		}

		public static SDKReturnCode GetDataForGlove_UsingGloveId( uint p_GloveId, out GloveLandscapeData p_GloveData )
		{
			p_GloveData = new GloveLandscapeData();
			return ManusDLLImport.CoreSdk_GetDataForGlove_UsingGloveId( p_GloveId, out p_GloveData );
		}

		public static SDKReturnCode GetDataForDongle( uint p_DongleId, out DongleLandscapeData p_DongleData )
		{
			p_DongleData = new DongleLandscapeData();
			return ManusDLLImport.CoreSdk_GetDataForDongle( p_DongleId, out p_DongleData );
		}

		public static SDKReturnCode GetNumberOfDongles( out uint p_NumberOfDongles )
		{
			p_NumberOfDongles = 0;
			return ManusDLLImport.CoreSdk_GetNumberOfDongles( out p_NumberOfDongles );
		}

		public static SDKReturnCode GetDongleIds( out uint[] p_DongleIds, uint p_NumberOfIdsThatFitInArray )
		{
			p_DongleIds = new uint[s_MaxNumberOfDongles];
			return ManusDLLImport.CoreSdk_GetDongleIds( p_DongleIds, p_NumberOfIdsThatFitInArray );
		}

		#endregion

		#region Haptics module

		public static SDKReturnCode GetNumberOfHapticsDongles( out uint p_NumberOfHapticsDongles )
		{
			p_NumberOfHapticsDongles = 0;
			return ManusDLLImport.CoreSdk_GetNumberOfHapticsDongles( out p_NumberOfHapticsDongles );
		}

		public static SDKReturnCode GetHapticsDongleIds( out uint[] p_HapticDongleIds, uint p_NumberOfIdsThatFitInArray )
		{
			p_HapticDongleIds = new uint[p_NumberOfIdsThatFitInArray];
			return ManusDLLImport.CoreSdk_GetHapticsDongleIds( p_HapticDongleIds, p_NumberOfIdsThatFitInArray );
		}

		#endregion

		#region Users
		public static SDKReturnCode AddUser( string p_Name, out uint p_User )
		{
			p_User = 0;
			return ManusDLLImport.CoreSdk_AddUser( p_Name, out p_User );
		}
		public static SDKReturnCode RemoveUser( uint p_UserId )
		{
			return ManusDLLImport.CoreSdk_RemoveUser( p_UserId );
		}
		public static SDKReturnCode SetUserName( uint p_Id, string p_Name )
		{
			return ManusDLLImport.CoreSdk_SetUserName( p_Id, p_Name );
		}
		public static SDKReturnCode AssignDongleToUser( uint p_Id, uint p_DongleId )
		{
			return ManusDLLImport.CoreSdk_AssignDongleToUser( p_Id, p_DongleId );
		}
		public static SDKReturnCode AssignGloveToUser( uint p_UserId, uint p_GloveId, Side p_Side )
		{
			return ManusDLLImport.CoreSdk_AssignGloveToUser( p_UserId, p_GloveId, p_Side );
		}
		public static SDKReturnCode MoveUserUp( uint p_UserId )
		{
			return ManusDLLImport.CoreSdk_MoveUserUp( p_UserId );
		}
		public static SDKReturnCode MoveUserDown( uint p_UserId )
		{
			return ManusDLLImport.CoreSdk_MoveUserDown( p_UserId );
		}
		#endregion

		#region Polygon

		public static SDKReturnCode GetNumberOfAvailableUsers( out uint p_NumberOfAvailableUsers )
		{
			p_NumberOfAvailableUsers = 0;
			return ManusDLLImport.CoreSdk_GetNumberOfAvailableUsers( out p_NumberOfAvailableUsers );
		}

		public static SDKReturnCode GetIdsOfAvailableUsers( out uint[] p_IdsOfAvailablePolygonUsers, uint p_NumberOfIdsThatFitInArray )
		{
			p_IdsOfAvailablePolygonUsers = new uint[p_NumberOfIdsThatFitInArray];
			return ManusDLLImport.CoreSdk_GetIdsOfAvailableUsers( p_IdsOfAvailablePolygonUsers, p_NumberOfIdsThatFitInArray );
		}

		public static SDKReturnCode GetSkeletonInfo( uint p_SkeletonIndex, out SkeletonInfo p_SkeletonInfo )
		{
			p_SkeletonInfo = new SkeletonInfo();
			return ManusDLLImport.CoreSdk_GetSkeletonInfo( p_SkeletonIndex, out p_SkeletonInfo );
		}

		public static SDKReturnCode GetSkeletonData( uint p_SkeletonIndex, out SkeletonNode[] p_Nodes, uint p_NodeCount )
		{
			p_Nodes = new SkeletonNode[p_NodeCount];
			return ManusDLLImport.CoreSdk_GetSkeletonData( p_SkeletonIndex, p_Nodes, p_NodeCount );
		}

		public static SDKReturnCode SetRawSkeletonHandMotion( HandMotion p_HandMotion )
		{
			return ManusDLLImport.CoreSdk_SetRawSkeletonHandMotion( p_HandMotion );
		}

		public static SDKReturnCode GetRawSkeletonHandMotion( out HandMotion p_HandMotion )
		{
			return ManusDLLImport.CoreSdk_GetRawSkeletonHandMotion( out p_HandMotion );
		}

		public static SDKReturnCode SetRawSkeletonPinchCompensationn( bool p_PinchCompensation )
		{
			return ManusDLLImport.CoreSdk_SetRawSkeletonPinchCompensation( p_PinchCompensation );
		}
		public static SDKReturnCode GetRawSkeletonPinchCompensation( out bool p_PinchCompensation )
		{
			p_PinchCompensation = false;
			return ManusDLLImport.CoreSdk_GetRawSkeletonPinchCompensation( out p_PinchCompensation );
		}

		public static SDKReturnCode SetRawSkeletonCasingCompensationn( float p_FilterStrength )
		{
			return ManusDLLImport.CoreSdk_SetRawSkeletonCasingCompensation( p_FilterStrength );
		}
		public static SDKReturnCode GetRawSkeletonCasingCompensation( out float p_FilterStrength )
		{
			p_FilterStrength = 0.0f;
			return ManusDLLImport.CoreSdk_GetRawSkeletonCasingCompensation( out p_FilterStrength );
		}

		public static SDKReturnCode GetRawSkeletonInfo( uint p_SkeletonIndex, out RawSkeletonInfo p_SkeletonInfo )
		{
			p_SkeletonInfo = new RawSkeletonInfo();
			return ManusDLLImport.CoreSdk_GetRawSkeletonInfo( p_SkeletonIndex, out p_SkeletonInfo );
		}

		public static SDKReturnCode GetRawSkeletonData( uint p_SkeletonIndex, out SkeletonNode[] p_Nodes, uint p_NodeCount )
		{
			p_Nodes = new SkeletonNode[p_NodeCount];
			return ManusDLLImport.CoreSdk_GetRawSkeletonData( p_SkeletonIndex, p_Nodes, p_NodeCount );
		}

		public static SDKReturnCode DoesSkeletonGloveSupportHaptics( uint p_SkeletonID, Side p_Side, out bool p_Haptics )
		{
			p_Haptics = false;
			return ManusDLLImport.CoreSdk_DoesSkeletonGloveSupportHaptics( p_SkeletonID, p_Side, out p_Haptics );
		}
		#endregion

		#region Tracking

		public static SDKReturnCode GetNumberOfAvailableTrackers( out uint p_NumberOfAvailableTrackers )
		{
			p_NumberOfAvailableTrackers = 0;
			return ManusDLLImport.CoreSdk_GetNumberOfAvailableTrackers( out p_NumberOfAvailableTrackers );
		}

		public static SDKReturnCode GetIdsOfAvailableTrackers( out TrackerId[] p_IdsOfAvailableTrackers, uint p_NumberOfIdsThatFitInArray )
		{
			p_IdsOfAvailableTrackers = new TrackerId[p_NumberOfIdsThatFitInArray];
			return ManusDLLImport.CoreSdk_GetIdsOfAvailableTrackers( p_IdsOfAvailableTrackers, p_NumberOfIdsThatFitInArray );
		}

		public static SDKReturnCode GetNumberOfAvailableTrackersForUserId( out uint p_NumberOfAvailableTrackers, uint p_UserId )
		{
			p_NumberOfAvailableTrackers = 0;
			return ManusDLLImport.CoreSdk_GetNumberOfAvailableTrackersForUserId( out p_NumberOfAvailableTrackers, p_UserId );
		}

		public static SDKReturnCode GetIdsOfAvailableTrackersForUserId( out TrackerId[] p_IdsOfAvailableTrackers, uint p_UserId,
			uint p_NumberOfIdsThatFitInArray )
		{
			p_IdsOfAvailableTrackers = new TrackerId[p_NumberOfIdsThatFitInArray];
			return ManusDLLImport.CoreSdk_GetIdsOfAvailableTrackersForUserId( p_IdsOfAvailableTrackers, p_UserId, p_NumberOfIdsThatFitInArray );
		}

		public static SDKReturnCode GetNumberOfAvailableTrackersForUserIndex( out uint p_NumberOfAvailableTrackers, uint p_UserIndex )
		{
			p_NumberOfAvailableTrackers = 0;
			return ManusDLLImport.CoreSdk_GetNumberOfAvailableTrackersForUserIndex( out p_NumberOfAvailableTrackers, p_UserIndex );
		}

		public static SDKReturnCode GetIdsOfAvailableTrackersForUserIndex( out TrackerId[] p_IdsOfAvailableTrackers, uint p_UserIndex,
			uint p_NumberOfIdsThatFitInArray )
		{
			p_IdsOfAvailableTrackers = new TrackerId[p_NumberOfIdsThatFitInArray];
			return ManusDLLImport.CoreSdk_GetIdsOfAvailableTrackersForUserIndex( p_IdsOfAvailableTrackers, p_UserIndex, p_NumberOfIdsThatFitInArray );
		}

		public static SDKReturnCode GetDataForTracker_UsingTrackerId( TrackerId p_TrackerId, out TrackerData p_TrackerData )
		{
			p_TrackerData = new TrackerData();
			return ManusDLLImport.CoreSdk_GetDataForTracker_UsingTrackerId( p_TrackerId, out p_TrackerData );
		}

		public static SDKReturnCode GetDataForTracker_UsingIdAndType( uint p_UserID, TrackerType p_TrackerType, out TrackerData p_TrackerData )
		{
			p_TrackerData = new TrackerData();
			uint t_TrackerTypeUInt = (uint)p_TrackerType;
			return ManusDLLImport.CoreSdk_GetDataForTracker_UsingIdAndType( p_UserID, t_TrackerTypeUInt, ref p_TrackerData );
		}

		public static SDKReturnCode SendDataForTrackers( ref TrackerData[] p_TrackerData, uint p_NumberOfTrackers )
		{
			return ManusDLLImport.CoreSdk_SendDataForTrackers( p_TrackerData, p_NumberOfTrackers );
		}

		public static SDKReturnCode SetTrackerOffset( uint p_UserId, ref TrackerOffset p_TrackerOffset )
		{
			return ManusDLLImport.CoreSdk_SetTrackerOffset( p_UserId, p_TrackerOffset );
		}

		#endregion

		#region Glove Calibration

		public static SDKReturnCode GloveCalibrationStart( GloveCalibrationArgs p_Args, out bool p_Result )
		{
			return ManusDLLImport.CoreSdk_GloveCalibrationStart( p_Args, out p_Result );
		}

		public static SDKReturnCode GloveCalibrationStop( GloveCalibrationArgs p_Args, out bool p_Result )
		{
			return ManusDLLImport.CoreSdk_GloveCalibrationStop( p_Args, out p_Result );
		}

		public static SDKReturnCode GloveCalibrationFinish( GloveCalibrationArgs p_Args, out bool p_Result )
		{
			return ManusDLLImport.CoreSdk_GloveCalibrationFinish( p_Args, out p_Result );
		}

		public static SDKReturnCode GloveCalibrationGetNumberOfSteps( GloveCalibrationArgs p_Args, out uint p_NumberOfSteps )
		{
			return ManusDLLImport.CoreSdk_GloveCalibrationGetNumberOfSteps( p_Args, out p_NumberOfSteps );
		}

		public static SDKReturnCode GloveCalibrationGetStepData( GloveCalibrationStepArgs p_Args, out GloveCalibrationStepData p_StepData )
		{
			return ManusDLLImport.CoreSdk_GloveCalibrationGetStepData( p_Args, out p_StepData );
		}

		public static SDKReturnCode GloveCalibrationStartStep( GloveCalibrationStepArgs p_Args, out bool p_Result )
		{
			return ManusDLLImport.CoreSdk_GloveCalibrationStartStep( p_Args, out p_Result );
		}

		public static SDKReturnCode GetGloveCalibration( uint p_GloveID, out byte[] p_CalibrationBytes )
		{
			uint t_Size;
			SDKReturnCode t_Result = ManusDLLImport.CoreSdk_GetGloveCalibrationSize(p_GloveID, out t_Size);
			p_CalibrationBytes = new byte[t_Size];
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				return t_Result;
			}
			return ManusDLLImport.CoreSdk_GetGloveCalibration( p_CalibrationBytes, t_Size );
		}

		public static SDKReturnCode GetGloveCalibrationForUser( uint p_UserID, Side p_Side, GloveProfileType p_ProfileType, out byte[] p_CalibrationBytes )
		{
			uint t_Size;
			SDKReturnCode t_Result = ManusDLLImport.CoreSdk_GetGloveCalibrationSizeForUser(p_UserID, p_Side, p_ProfileType, out t_Size);
			p_CalibrationBytes = new byte[t_Size];
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				return t_Result;
			}
			return ManusDLLImport.CoreSdk_GetGloveCalibration( p_CalibrationBytes, t_Size );
		}

		public static SDKReturnCode SetGloveCalibration( uint p_GloveID, byte[] p_CalibrationBytes, out SetGloveCalibrationReturnCode p_Response )
		{
			return ManusDLLImport.CoreSdk_SetGloveCalibration( p_GloveID, p_CalibrationBytes, (uint)p_CalibrationBytes.Length, out p_Response );
		}

		public static SDKReturnCode SetGloveCalibrationForUser( uint p_UserID, byte[] p_CalibrationBytes, out SetGloveCalibrationReturnCode p_Response )
		{
			return ManusDLLImport.CoreSdk_SetGloveCalibrationForUser( p_UserID, p_CalibrationBytes, (uint)p_CalibrationBytes.Length, out p_Response );
		}

		#endregion

		#region Skeleton

		public static SDKReturnCode RegisterCallbackForSkeletonStream( SkeletonStreamCallback p_Callback )
		{
			var t_Res = ManusDLLImport.CoreSdk_RegisterCallbackForSkeletonStream( ProcessInternalSkeletonStream );
			if( t_Res != SDKReturnCode.Success ) return t_Res;

			m_OnSkeletonData = p_Callback;
			return SDKReturnCode.Success;
		}

		public static SDKReturnCode RegisterCallbackForTrackerStream( TrackerStreamCallback p_Callback )
		{
			var t_Res = ManusDLLImport.CoreSdk_RegisterCallbackForTrackerStream( ProcessInternalTrackerStream );
			if( t_Res != SDKReturnCode.Success ) return t_Res;

			m_OnTrackerData = p_Callback;
			return SDKReturnCode.Success;
		}

		public static SDKReturnCode RegisterCallbackForRawSkeletonStream( RawSkeletonStreamCallback p_Callback )
		{
			var t_Res = ManusDLLImport.CoreSdk_RegisterCallbackForRawSkeletonStream( ProcessInternalRawSkeletonStream );
			if( t_Res != SDKReturnCode.Success ) return t_Res;

			m_OnRawSkeletonData = p_Callback;
			return SDKReturnCode.Success;
		}

		[MonoPInvokeCallback( typeof( SkeletonStreamCallbackPtr ) )]
		private static void ProcessInternalSkeletonStream( IntPtr p_DataPtr )
		{
			var t_StreamInfo = Marshal.PtrToStructure<SkeletonStreamInfo>( p_DataPtr );

			SkeletonStream t_Data = new SkeletonStream();
			t_Data.publishTime = t_StreamInfo.publishTime;
			t_Data.skeletons = new List<Skeleton>( (int)t_StreamInfo.skeletonsCount );
			for( int i = 0; i < t_StreamInfo.skeletonsCount; i++ )
			{
				SkeletonInfo t_Info = new SkeletonInfo();
				var t_Res = ManusDLLImport.CoreSdk_GetSkeletonInfo( (uint)i, out t_Info );
				if( t_Res != SDKReturnCode.Success ) return;
				var t_Skl = new CoreSDK.Skeleton();
				t_Skl.id = t_Info.id;
				t_Skl.nodes = new SkeletonNode[t_Info.nodesCount];
				t_Res = ManusDLLImport.CoreSdk_GetSkeletonData( (uint)i, t_Skl.nodes, t_Info.nodesCount );
				if( t_Res != SDKReturnCode.Success ) return;
				t_Data.skeletons.Add( t_Skl );
			}

			m_OnSkeletonData( t_Data );
		}

		[MonoPInvokeCallback( typeof( TrackerStreamCallbackPtr ) )]
		private static void ProcessInternalTrackerStream( IntPtr p_DataPtr )
		{
			var t_StreamInfo = Marshal.PtrToStructure<InternalTrackerStreamInfo>( p_DataPtr );

			TrackerStream t_Data = new TrackerStream();
			t_Data.publishTime = t_StreamInfo.publishTime;
			t_Data.trackers = new List<TrackerData>( (int)t_StreamInfo.trackerCount );
			for( int i = 0; i < t_StreamInfo.trackerCount; i++ )
			{
				TrackerData t_TrackerData = new TrackerData();
				var t_Res = ManusDLLImport.CoreSdk_GetTrackerData( (uint)i, out t_TrackerData );
				if( t_Res != SDKReturnCode.Success ) return;
				t_Data.trackers.Add( t_TrackerData );
			}

			m_OnTrackerData( t_Data );
		}

		[MonoPInvokeCallback( typeof( RawSkeletonStreamCallbackPtr ) )]
		private static void ProcessInternalRawSkeletonStream( IntPtr p_DataPtr )
		{
			var t_StreamInfo = Marshal.PtrToStructure<SkeletonStreamInfo>( p_DataPtr );

			RawSkeletonStream t_Data = new RawSkeletonStream();
			t_Data.publishTime = t_StreamInfo.publishTime;
			t_Data.skeletons = new List<RawSkeleton>( (int)t_StreamInfo.skeletonsCount );
			for( int i = 0; i < t_StreamInfo.skeletonsCount; i++ )
			{
				RawSkeletonInfo t_Info = new RawSkeletonInfo();
				var t_Res = ManusDLLImport.CoreSdk_GetRawSkeletonInfo( (uint)i, out t_Info );
				if( t_Res != SDKReturnCode.Success ) return;
				var t_Skl = new RawSkeleton();
				t_Skl.gloveId = t_Info.gloveId;
				t_Skl.nodes = new SkeletonNode[t_Info.nodesCount];
				t_Res = ManusDLLImport.CoreSdk_GetRawSkeletonData( (uint)i, t_Skl.nodes, t_Info.nodesCount );
				if( t_Res != SDKReturnCode.Success ) return;
				t_Data.skeletons.Add( t_Skl );
			}

			m_OnRawSkeletonData( t_Data );
		}

		public static SDKReturnCode OverwriteSkeletonSetup( uint p_SkeletonSetupIndex, ref SkeletonSetupInfo p_Skeleton )
		{
			return ManusDLLImport.CoreSdk_OverwriteSkeletonSetup( p_SkeletonSetupIndex, p_Skeleton );
		}

		public static SDKReturnCode CreateSkeletonSetup( ref SkeletonSetupInfo p_Skeleton, out uint p_SkeletonSetupIndex )
		{
			p_SkeletonSetupIndex = 0;
			return ManusDLLImport.CoreSdk_CreateSkeletonSetup( p_Skeleton, out p_SkeletonSetupIndex );
		}

		public static SDKReturnCode AddNodeToSkeletonSetup( uint p_SkeletonSetupIndex, NodeSetup p_Node )
		{
			return ManusDLLImport.CoreSdk_AddNodeToSkeletonSetup( p_SkeletonSetupIndex, p_Node );
		}

		public static SDKReturnCode AddChainToSkeletonSetup( uint p_SkeletonSetupIndex, ChainSetup p_Chain )
		{
			p_Chain.settings.hand.fingerChainIdsUsed = p_Chain.settings.hand.fingerChainIds?.Length ?? 0;
			p_Chain.settings.foot.toeChainIdsUsed = p_Chain.settings.foot.toeChainIds?.Length ?? 0;
			p_Chain.settings.usedSettings = p_Chain.type;

			return ManusDLLImport.CoreSdk_AddChainToSkeletonSetup( p_SkeletonSetupIndex, p_Chain );
		}

		public static SDKReturnCode AddColliderToSkeletonSetup( uint p_SkeletonSetupIndex, ColliderSetup p_Collider )
		{
			return ManusDLLImport.CoreSdk_AddColliderToSkeletonSetup( p_SkeletonSetupIndex, p_Collider );
		}

		public static SDKReturnCode AddMeshSetupToSkeletonSetup( uint p_SkeletonSetupIndex, uint p_NodeId, out uint p_MeshSetupIndex )
		{
			p_MeshSetupIndex = 0;
			return ManusDLLImport.CoreSdk_AddMeshSetupToSkeletonSetup( p_SkeletonSetupIndex, p_NodeId, out p_MeshSetupIndex );
		}

		public static SDKReturnCode AddVertexToMeshSetup( uint p_SkeletonSetupIndex, uint p_MeshSetupIndex, Vertex p_Vertex )
		{
			return ManusDLLImport.CoreSdk_AddVertexToMeshSetup( p_SkeletonSetupIndex, p_MeshSetupIndex, p_Vertex );
		}

		public static SDKReturnCode AddTriangleToMeshSetup( uint p_SkeletonSetupIndex, uint p_MeshSetupIndex, Triangle p_Triangle )
		{
			return ManusDLLImport.CoreSdk_AddTriangleToMeshSetup( p_SkeletonSetupIndex, p_MeshSetupIndex, p_Triangle );
		}

		public static SDKReturnCode OverwriteChainToSkeletonSetup( uint p_SkeletonSetupIndex, ChainSetup p_Chain )
		{
			p_Chain.settings.hand.fingerChainIdsUsed = p_Chain.settings.hand.fingerChainIds?.Length ?? 0;
			p_Chain.settings.foot.toeChainIdsUsed = p_Chain.settings.foot.toeChainIds.Length;
			p_Chain.settings.usedSettings = p_Chain.type;
			return ManusDLLImport.CoreSdk_OverwriteChainToSkeletonSetup( p_SkeletonSetupIndex, p_Chain );
		}

		public static SDKReturnCode GetSkeletonSetupArraySizes( uint p_SkeletonSetupIndex, out SkeletonSetupArraySizes p_SkeletonInfo )
		{
			p_SkeletonInfo = new SkeletonSetupArraySizes();
			return ManusDLLImport.CoreSdk_GetSkeletonSetupArraySizes( p_SkeletonSetupIndex, out p_SkeletonInfo );
		}

		public static SDKReturnCode AllocateChainsForSkeletonSetup( uint p_SkeletonSetupIndex )
		{
			return ManusDLLImport.CoreSdk_AllocateChainsForSkeletonSetup( p_SkeletonSetupIndex );
		}

		public static SDKReturnCode PrepareSkeletonSetup( uint p_SkeletonSetupIndex )
		{
			return ManusDLLImport.CoreSdk_PrepareSkeletonSetup( p_SkeletonSetupIndex );
		}

		public static SDKReturnCode GetSkeletonSetupInfo( uint p_SkeletonSetupIndex, out SkeletonSetupInfo p_SkeletonSetupInfo )
		{
			return ManusDLLImport.CoreSdk_GetSkeletonSetupInfo( p_SkeletonSetupIndex, out p_SkeletonSetupInfo );
		}

		public static SDKReturnCode GetSkeletonSetupChains( uint p_SkeletonSetupIndex, out ChainSetup[] p_ChainSetup )
		{
			var t_Res = GetSkeletonSetupArraySizes( p_SkeletonSetupIndex, out SkeletonSetupArraySizes t_Info );
			if( t_Res != SDKReturnCode.Success || t_Info.chainsCount == 0 )
			{
				p_ChainSetup = Array.Empty<ChainSetup>();
				return t_Res;
			}
			p_ChainSetup = new ChainSetup[t_Info.chainsCount];
			return ManusDLLImport.CoreSdk_GetSkeletonSetupChains( p_SkeletonSetupIndex, p_ChainSetup );
		}

		public static SDKReturnCode GetSkeletonSetupNodes( uint p_SkeletonSetupIndex, out NodeSetup[] p_NodeSetup )
		{
			var t_Res = GetSkeletonSetupArraySizes( p_SkeletonSetupIndex, out SkeletonSetupArraySizes t_Info );
			if( t_Res != SDKReturnCode.Success || t_Info.nodesCount == 0 )
			{
				p_NodeSetup = Array.Empty<NodeSetup>();
				return t_Res;
			}
			p_NodeSetup = new NodeSetup[t_Info.nodesCount];
			return ManusDLLImport.CoreSdk_GetSkeletonSetupNodes( p_SkeletonSetupIndex, p_NodeSetup );
		}

		public static SDKReturnCode GetSkeletonSetupColliders( uint p_SkeletonSetupIndex, out ColliderSetup[] p_ColliderSetup )
		{
			var t_Res = GetSkeletonSetupArraySizes( p_SkeletonSetupIndex, out SkeletonSetupArraySizes t_Info );
			if( t_Res != SDKReturnCode.Success || t_Info.collidersCount == 0 )
			{
				p_ColliderSetup = Array.Empty<ColliderSetup>();
				return t_Res;
			}
			p_ColliderSetup = new ColliderSetup[t_Info.collidersCount];
			return ManusDLLImport.CoreSdk_GetSkeletonSetupColliders( p_SkeletonSetupIndex, p_ColliderSetup );
		}

		public static SDKReturnCode GetRawSkeletonNodeCount( uint p_GloveId, out uint p_NodeCount )
		{
			return ManusDLLImport.CoreSdk_GetRawSkeletonNodeCount( p_GloveId, out p_NodeCount );
		}

		public static SDKReturnCode GetRawSkeletonNodeInfo( uint p_GloveId, out NodeInfo[] p_NodeInfo )
		{
			var t_Res =  GetRawSkeletonNodeCount( p_GloveId, out uint t_NodeCount);
			if( t_Res != SDKReturnCode.Success || t_NodeCount == 0 )
			{
				p_NodeInfo = Array.Empty<NodeInfo>();
				return t_Res;
			}
			p_NodeInfo = new NodeInfo[t_NodeCount];
			return ManusDLLImport.CoreSdk_GetRawSkeletonNodeInfo( p_GloveId, p_NodeInfo );
		}

		public static SDKReturnCode LoadSkeleton( uint p_SkeletonSetupIndex, out uint p_SkeletonId )
		{
			p_SkeletonId = 0;
			return ManusDLLImport.CoreSdk_LoadSkeleton( p_SkeletonSetupIndex, out p_SkeletonId );
		}

		public static SDKReturnCode UnloadSkeleton( uint p_SkeletonId )
		{
			return ManusDLLImport.CoreSdk_UnloadSkeleton( p_SkeletonId );
		}

		public static SDKReturnCode AllocateChains( uint p_SkeletonSetupIndex )
		{
			return ManusDLLImport.CoreSdk_AllocateChainsForSkeletonSetup( p_SkeletonSetupIndex );
		}

		public static SDKReturnCode GetTemporarySkeletonCountForAllSessions( ref TemporarySkeletonCountForAllSessions p_TemporarySkeletonCountForSessions )
		{
			return ManusDLLImport.CoreSdk_GetTemporarySkeletonCountForAllSessions( out p_TemporarySkeletonCountForSessions );
		}

		public static SDKReturnCode GetTemporarySkeletonsInfoForSession( uint p_SessionId, ref TemporarySkeletonsInfoForSession p_TemporarySkeletonsInfoForSession )
		{
			return ManusDLLImport.CoreSdk_GetTemporarySkeletonsInfoForSession( p_SessionId, out p_TemporarySkeletonsInfoForSession );
		}

		public static SDKReturnCode GetTemporarySkeleton( uint p_SkeletonSetupIndex, uint p_SessionId )
		{
			return ManusDLLImport.CoreSdk_GetTemporarySkeleton( p_SkeletonSetupIndex, p_SessionId );
		}

		public static SDKReturnCode SaveTemporarySkeleton( uint p_SkeletonSetupIndex, uint p_SessionId, bool p_IsSkeletonModified )
		{
			return ManusDLLImport.CoreSdk_SaveTemporarySkeleton( p_SkeletonSetupIndex, p_SessionId, p_IsSkeletonModified );
		}

		public static SDKReturnCode GetCompressedTemporarySkeletonData( uint p_SkeletonSetupIndex, uint p_SkeletonId, out byte[] p_TemporarySkeletonData )
		{
			uint t_TemporarySkeletonLengthInBytes;
			CoreSDK.SDKReturnCode t_Result = ManusDLLImport.CoreSdk_CompressTemporarySkeletonAndGetSize( p_SkeletonSetupIndex, p_SkeletonId, out t_TemporarySkeletonLengthInBytes );
			p_TemporarySkeletonData = new byte[t_TemporarySkeletonLengthInBytes];
			if( t_Result != CoreSDK.SDKReturnCode.Success )
			{
				return t_Result;
			}
			return ManusDLLImport.CoreSdk_GetCompressedTemporarySkeletonData( p_TemporarySkeletonData, t_TemporarySkeletonLengthInBytes );
		}

		public static SDKReturnCode GetTemporarySkeletonFromCompressedData( uint p_SkeletonSetupIndex, uint p_SessionId, byte[] p_TemporarySkeletonData )
		{
			return ManusDLLImport.CoreSdk_GetTemporarySkeletonFromCompressedData( p_SkeletonSetupIndex, p_SessionId, p_TemporarySkeletonData, (uint)p_TemporarySkeletonData.Length );
		}

		public static SDKReturnCode ClearAllTemporarySkeletons()
		{
			return ManusDLLImport.CoreSdk_ClearAllTemporarySkeletons();
		}

		public static SDKReturnCode GetSessionId( out uint p_SessionId )
		{
			p_SessionId = 0;
			return ManusDLLImport.CoreSdk_GetSessionId( out p_SessionId );
		}

		#endregion

		#region Gesture

		public static SDKReturnCode GetGestureLandscapeData( uint p_ArraySize, out GestureLandscapeData[] p_GestureLandscapeData )
		{
			p_GestureLandscapeData = new GestureLandscapeData[p_ArraySize];
			var t_Res = ManusDLLImport.CoreSdk_GetGestureLandscapeData(p_GestureLandscapeData, p_ArraySize);
			return t_Res;
		}

		public static SDKReturnCode RegisterCallbackForGestureStream( GestureStreamCallback p_Callback )
		{
			//if( m_OnSkeletonData == null )
			{
				var t_Res = ManusDLLImport.CoreSdk_RegisterCallbackForGestureStream( ProcessInternalGestureStream );
				if( t_Res != SDKReturnCode.Success ) return t_Res;
			}

			m_OnGestureData = p_Callback;
			return SDKReturnCode.Success;
		}

		[MonoPInvokeCallback( typeof( GestureStreamCallbackPtr ) )]
		private static void ProcessInternalGestureStream( IntPtr p_DataPtr )
		{
			var t_StreamInfo = Marshal.PtrToStructure<GestureStreamInfo>( p_DataPtr );

			GestureStream t_AllData = new GestureStream();
			t_AllData.publishTime = t_StreamInfo.publishTime;
			t_AllData.gestureProbabilities = new List<GestureProbabilities>( (int)t_StreamInfo.gestureProbabilitiesCount );
			for( int i = 0; i < t_StreamInfo.gestureProbabilitiesCount; i++ )
			{
				GestureProbabilities t_InternalProbs = new GestureProbabilities();
				var t_Res = ManusDLLImport.CoreSdk_GetGestureStreamData( (uint)i, 0, out t_InternalProbs );
				if( t_Res != SDKReturnCode.Success ) return;

				var t_Probs = new GestureProbabilities();
				t_Probs.id = t_InternalProbs.id;
				t_Probs.isUserID = t_InternalProbs.isUserID;
				t_Probs.gestureData = new GestureProbability[(int)t_InternalProbs.totalGestureCount];

				int t_BatchCount = ((int)t_InternalProbs.totalGestureCount / GESTUREPROBABILITIES_GESTUREDATA_ARRAY_SIZE) + 1;
				uint t_ProbabilityIdx = 0;
				for( uint b = 0; b < t_BatchCount; b++ )
				{
					for( int j = 0; j < (int)t_InternalProbs.gestureCount; j++ )
					{
						t_Probs.gestureData[j] = t_InternalProbs.gestureData[j];
					}
					t_ProbabilityIdx += t_InternalProbs.gestureCount;
					t_Res = ManusDLLImport.CoreSdk_GetGestureStreamData( (uint)i, t_ProbabilityIdx, out t_InternalProbs ); //this will get more data, if needed for the next iteration.
					if( t_Res != SDKReturnCode.Success ) return;
				}
				t_AllData.gestureProbabilities.Add( t_Probs );
			}

			m_OnGestureData( t_AllData );
		}
		#endregion
		
		public static SDKReturnCode RegisterCallbackForRawDeviceDataStream( OnRawDeviceData p_RawDeviceData )
		{
			var t_Res = ManusDLLImport.CoreSdk_RegisterCallbackForRawDeviceDataStream( ProcessInternalCallbackForOnRawDeviceData );
			if( t_Res != SDKReturnCode.Success ) return t_Res;

			m_OnRawDeviceData = p_RawDeviceData;
			return SDKReturnCode.Success;
		}

		private static void ProcessInternalCallbackForOnRawDeviceData( RawDeviceDataInfo p_RawDeviceDataInfo )
		{
			RawData t_RawData = new RawData()
			{
				publishTime = p_RawDeviceDataInfo.publishTime,
				rawDeviceData = new List<RawDeviceData>( (int)p_RawDeviceDataInfo.rawDeviceDataCount )
			};
			
			for( uint i = 0; i < p_RawDeviceDataInfo.rawDeviceDataCount; i++ )
			{
				RawDeviceData t_Data = new RawDeviceData();
				var t_Res = ManusDLLImport.CoreSdk_GetRawDeviceData( i, out t_Data );
				if (t_Res != SDKReturnCode.Success) break;
				t_RawData.rawDeviceData.Add( t_Data );
			}

			m_OnRawDeviceData(t_RawData);
		}

		[StructLayout( LayoutKind.Sequential )]
		[System.Serializable]
		protected struct InternalTrackerStreamInfo
		{
			public ManusTimestamp publishTime;
			public uint trackerCount;
		}

		[StructLayout( LayoutKind.Sequential )]
		[System.Serializable]
		public struct Skeleton
		{
			public uint id;
			public SkeletonNode[] nodes;
		}

		[StructLayout( LayoutKind.Sequential )]
		[System.Serializable]
		public struct RawSkeleton
		{
			public uint gloveId;
			public SkeletonNode[] nodes;
		}

		[StructLayout( LayoutKind.Sequential )]
		[System.Serializable]
		public struct SkeletonStream
		{
			public ManusTimestamp publishTime;
			public List<Skeleton> skeletons;
		}

		[StructLayout( LayoutKind.Sequential )]
		[System.Serializable]
		public struct TrackerStream
		{
			public ManusTimestamp publishTime;
			public List<TrackerData> trackers;
		}

		[StructLayout( LayoutKind.Sequential )]
		[System.Serializable]
		public struct RawSkeletonStream
		{
			public ManusTimestamp publishTime;
			public List<RawSkeleton> skeletons;
		}

		[StructLayout( LayoutKind.Sequential )]
		[System.Serializable]
		public struct GestureStream
		{
			public ManusTimestamp publishTime;
			public List<GestureProbabilities> gestureProbabilities;
		}
		
		[System.Serializable]
		[StructLayout( LayoutKind.Sequential )]
		public struct RawData
		{
			public ManusTimestamp publishTime;
			public List<RawDeviceData> rawDeviceData; 
		}

		protected partial class ManusDLLImport
		{
			#region Wrapper startup and shutdown.

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_InitializeCore();

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_ShutDown();

			#endregion

			#region Utility functions

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_WasDllBuiltInDebugConfiguration( out bool p_WasBuiltInDebugConfiguration );

			/// @brief Gets the timestamp info (more readable form of timestamp).
			/// @param p_Timestamp Timestamp to get info from
			/// @param p_Info Info of the timestamp
			/// @return SDKReturnCode_Success if successful.
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetTimestampInfo( ManusTimestamp p_Timestamp, out ManusTimestampInfo p_Info );

			/// @brief Sets the timestamp according to the info (more readable form of timestamp).
			/// @param p_Timestamp the Timestamp to set info of
			/// @param p_Info Info to get info from
			/// @return SDKReturnCode_Success if successful.
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SetTimestampInfo( out ManusTimestamp p_Timestamp, ManusTimestampInfo p_Info );
			#endregion

			#region Connection handling

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_LookForHosts( uint p_WaitSeconds = 1, bool p_LoopbackOnly = false );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetNumberOfAvailableHostsFound( out uint p_NumberOfAvailableHostsFound );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetAvailableHostsFound( [Out] ManusHost[] p_Host, uint p_NumberOfHostsThatFitInArray );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetIsConnectedToCore( out bool p_ConnectedToCore );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_ConnectGRPC();

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_ConnectToHost( ManusHost p_Host );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SetSessionType( SessionType p_SessionType );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_InitializeCoordinateSystemWithVUH( CoordinateSystemVUH p_CoordinateSystem,
				bool p_UseWorldCoordinates = true );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_InitializeCoordinateSystemWithDirection( CoordinateSystemDirection p_CoordinateSystem,
				bool p_UseWorldCoordinates = true );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetVersionsAndCheckCompatibility( out ManusVersion p_SdkVersion,
				out ManusVersion p_CoreVersion, out bool p_AreVersionsCompatible );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetSessionId( out uint p_SessionId );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_RegisterCallbackForOnConnect( OnConnectedToCorePtr p_OnConnectToCore );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_RegisterCallbackForOnDisconnect( OnDisconnectFromCorePtr p_OnDisconnectFromCore );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_RegisterCallbackForSkeletonStream( InternalSkeletonStreamCallbackPtr p_OnSkeletonInfo );
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_RegisterCallbackForTrackerStream( InternalTrackerStreamCallbackPtr p_OnTrackerInfo );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_RegisterCallbackForRawSkeletonStream( InternalRawSkeletonStreamCallbackPtr p_OnSkeletonInfo );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_RegisterCallbackForLandscapeStream( LandscapeStreamCallbackPtr p_OnLandscape );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_RegisterCallbackForSystemStream( SystemStreamCallbackPtr p_OnSystem );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_RegisterCallbackForErgonomicsStream( ErgonomicsStreamCallbackPtr p_OnErgonomics );

			#endregion

			#region Basic glove interactions
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_PairGlove( uint p_GloveId, out bool p_Success );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_PairGloveToDongle( uint p_GloveId, uint p_DongleId, out bool p_Success );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_UnpairGlove( uint p_GloveId, out bool p_Success );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SetLicense( uint p_DongleId, string p_LicenseString, uint p_LicenseStringSize, out bool p_Result, StringBuilder p_Response );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_UpdateFirmware( uint p_DeviceID, out bool p_Result, StringBuilder p_Response);

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_VibrateWristOfGlove( uint p_GloveId, float p_UnitStrength, ushort p_DurationInMilliseconds );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_VibrateFingers( uint p_DongleId, Side p_HandType, [In] float[] p_Powers );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_VibrateFingersForGlove( uint p_GloveID, [In] float[] p_Powers );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_VibrateFingersForSkeleton( uint p_SkeletonId, Side p_HandType, [In] float[] p_Powers );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetGloveIdOfUser_UsingUserId( uint p_UserId, Side p_HandType, out uint p_GloveId );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetNumberOfAvailableGloves( out uint p_NumberOfAvailableGloves );

			//Test again see if it works as intended
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetIdsOfAvailableGloves( [Out] uint[] p_IdsOfAvailableGloves,
				uint p_NumberOfIdsThatFitInArray );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetGlovesForDongle( uint p_DongleId, out uint p_LeftGloveId, out uint p_RightGloveId );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetDataForGlove_UsingGloveId( uint p_GloveId, out GloveLandscapeData p_GloveData );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetDataForDongle( uint p_DongleId, out DongleLandscapeData p_DongleData );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetNumberOfDongles( out uint p_NumberOfDongles );

			//Look into this could be marshaling issue? Always returns 0 as ID
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetDongleIds( [Out] uint[] p_DongleIds, uint p_NumberOfIdsThatFitInArray );

			#endregion

			#region Haptics module

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetNumberOfHapticsDongles( out uint p_NumberOfHapticsDongles );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetHapticsDongleIds( [Out] uint[] p_HapticDongleIds, uint p_NumberOfIdsThatFitInArray );

			#endregion

			#region Users

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetNumberOfAvailableUsers( out uint p_NumberOfAvailableUsers );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetIdsOfAvailableUsers( [Out] uint[] p_IdsOfAvailablePolygonUsers,
				uint p_NumberOfIdsThatFitInArray );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_AddUser( string p_Name, out uint p_UserID );
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_RemoveUser( uint p_Id );
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SetUserName( uint p_Id, string p_Name );
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_AssignDongleToUser( uint p_Id, uint p_DongleId );
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_AssignGloveToUser( uint p_UserId, uint p_GloveId, Side p_Side );
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_MoveUserUp( uint p_UserId );
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_MoveUserDown( uint p_UserId );
			#endregion

			#region Tracking

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetTrackerData( uint p_TrackerIndex, out TrackerData p_TrackerData );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetNumberOfAvailableTrackers( out uint p_NumberOfAvailableTrackers );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetIdsOfAvailableTrackers( [Out] TrackerId[] p_IdsOfAvailableTrackers,
				uint p_NumberOfIdsThatFitInArray );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetNumberOfAvailableTrackersForUserId( out uint p_NumberOfAvailableTrackers, uint p_UserId );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetIdsOfAvailableTrackersForUserId( [Out] TrackerId[] p_IdsOfAvailableTrackers, uint p_UserId,
				uint p_NumberOfIdsThatFitInArray );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetNumberOfAvailableTrackersForUserIndex( out uint p_NumberOfAvailableTrackers, uint p_UserIndex );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetIdsOfAvailableTrackersForUserIndex( [Out] TrackerId[] p_IdsOfAvailableTrackers, uint p_UserIndex,
				uint p_NumberOfIdsThatFitInArray );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetDataForTracker_UsingTrackerId( TrackerId p_TrackerId, out TrackerData p_TrackerData );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetDataForTracker_UsingIdAndType( uint p_UserId, uint p_TrackerType,
				ref TrackerData p_TrackerData );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SendDataForTrackers( [In] TrackerData[] p_TrackerData, uint p_NumberOfTrackers );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SetTrackerOffset( uint p_UserId, [In] TrackerOffset p_TrackerOffset );
			#endregion

			#region Glove calibration

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GloveCalibrationStart( GloveCalibrationArgs p_Args, out bool p_Result );
			
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GloveCalibrationStop( GloveCalibrationArgs p_Args, out bool p_Result );
			
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GloveCalibrationFinish( GloveCalibrationArgs p_Args, out bool p_Result );


			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GloveCalibrationGetNumberOfSteps( GloveCalibrationArgs p_Args, out uint p_NumberOfSteps );
			
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GloveCalibrationGetStepData( GloveCalibrationStepArgs p_Args, out GloveCalibrationStepData p_Data );
			
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GloveCalibrationStartStep( GloveCalibrationStepArgs p_Args, out bool p_Result );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetGloveCalibrationSize( uint p_GloveID, out uint p_Size );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetGloveCalibrationSizeForUser( uint p_UserID, Side p_Side, GloveProfileType p_ProfileType, out uint p_Size );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetGloveCalibration( [In, Out] byte[] p_CalibrationBytes, uint p_Size );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SetGloveCalibration( uint p_GloveID, byte[] p_CalibrationBytes, uint p_BytesLength, out SetGloveCalibrationReturnCode p_Response );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SetGloveCalibrationForUser( uint p_UserID, byte[] p_CalibrationBytes, uint p_BytesLength, out SetGloveCalibrationReturnCode p_Response );

			#endregion

			#region Skeletal System

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetSkeletonInfo( uint p_SkeletonIndex, out SkeletonInfo p_SklInfo );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetSkeletonData( uint p_SkeletonIndex, [Out] SkeletonNode[] p_Nodes, uint p_NodeCount );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SetRawSkeletonHandMotion( HandMotion p_HandMotion );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetRawSkeletonHandMotion( out HandMotion p_HandMotion );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SetRawSkeletonPinchCompensation( bool p_PinchCompensation );
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetRawSkeletonPinchCompensation( out bool p_PinchCompensation );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SetRawSkeletonCasingCompensation( float p_FilterStrength );
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetRawSkeletonCasingCompensation( out float p_FilterStrength );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetRawSkeletonInfo( uint p_SkeletonIndex, out RawSkeletonInfo p_SklInfo );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetRawSkeletonData( uint p_SkeletonIndex, [Out] SkeletonNode[] p_Nodes, uint p_NodeCount );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_DoesSkeletonGloveSupportHaptics( uint p_SkeletonId, Side p_HandType, out bool p_IsHaptics );
			#endregion

			#region Skeletal Setup

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_OverwriteSkeletonSetup( uint p_SkeletonSetupIndex, SkeletonSetupInfo p_Skeleton );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_CreateSkeletonSetup( SkeletonSetupInfo p_Skeleton, out uint p_SkeletonSetupIndex );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_AddNodeToSkeletonSetup( uint p_SkeletonSetupIndex, NodeSetup p_Node );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_AddChainToSkeletonSetup( uint p_SkeletonSetupIndex, ChainSetup p_Chain );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_AddColliderToSkeletonSetup( uint p_SkeletonSetupIndex, ColliderSetup p_Collider );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_AddMeshSetupToSkeletonSetup( uint p_SkeletonSetupIndex, uint p_NodeId, out uint p_MeshSetupIndex );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_AddVertexToMeshSetup( uint p_SkeletonSetupIndex, uint p_MeshSetupIndex, Vertex p_Vertex );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_AddTriangleToMeshSetup( uint p_SkeletonSetupIndex, uint p_MeshSetupIndex, Triangle p_Triangle );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_OverwriteChainToSkeletonSetup( uint p_SkeletonSetupIndex, ChainSetup p_Chain );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetSkeletonSetupArraySizes( uint p_SkeletonSetupIndex,
				out SkeletonSetupArraySizes p_ChainSetup );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_AllocateChainsForSkeletonSetup( uint p_SkeletonSetupIndex );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_PrepareSkeletonSetup( uint p_SkeletonSetupIndex );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetSkeletonSetupInfo( uint p_SkeletonSetupIndex, out SkeletonSetupInfo p_SkeletonSetupInfo );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetSkeletonSetupChains( uint p_SkeletonSetupIndex, [Out] ChainSetup[] p_ChainSetup );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetSkeletonSetupNodes( uint p_SkeletonSetupIndex, [Out] NodeSetup[] p_NodeSetup );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetSkeletonSetupColliders( uint p_SkeletonSetupIndex, [Out] ColliderSetup[] p_ColliderSetup );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetRawSkeletonNodeCount( uint p_GloveId, out uint p_NodeCount );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetRawSkeletonNodeInfo( uint p_GloveId, [Out] NodeInfo[] p_NodeInfo );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_LoadSkeleton( uint p_SkeletonSetupIndex, out uint p_SkeletonId );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_UnloadSkeleton( uint p_SkeletonId );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_SaveTemporarySkeleton( uint p_SkeletonSetupIndex, uint p_SkeletonId,
				bool p_IsSkeletonModified );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_CompressTemporarySkeletonAndGetSize( uint p_SkeletonSetupIndex, uint p_SkeletonId, out uint p_TemporarySkeletonLengthInBytes );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetCompressedTemporarySkeletonData( [In, Out] byte[] p_TemporarySkeletonData, uint p_TemporarySkeletonLengthInBytes );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetTemporarySkeleton( uint p_SkeletonSetupIndex, uint p_SessionId );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetTemporarySkeletonFromCompressedData( uint p_SkeletonSetupIndex, uint p_SessionId, [Out] byte[] p_TemporarySkeletonData, uint p_TemporarySkeletonLengthInBytes );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_ClearTemporarySkeleton( uint p_SkeletonSetupIndex, uint p_SessionId );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_ClearAllTemporarySkeletons();

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetTemporarySkeletonCountForAllSessions(
				out TemporarySkeletonCountForAllSessions p_TemporarySkeletonCountForSessions );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetTemporarySkeletonsInfoForSession(
				uint p_SessionId, out TemporarySkeletonsInfoForSession p_TemporarySkeletonsInfoForSession );

			#endregion

			#region Gestures

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetGestureLandscapeData( [Out] GestureLandscapeData[] p_GestureLandscapeDataArray, uint p_ArraySize );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_RegisterCallbackForGestureStream( InternalGestureStreamCallbackPtr p_GestureStreamCallback );

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetGestureStreamData( uint p_GestureStreamDataIndex, uint p_StartDataIndex, out GestureProbabilities p_GestureProbabilitiesCollection );
			#endregion
			
			#region Raw Device Data (Metaglove pro)

			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_GetRawDeviceData( uint p_RawDeviceDataIndex, out RawDeviceData p_RawDeviceData );
			
			[DllImport( s_DLLName, CallingConvention = s_ImportCallingConvention, CharSet = s_ImportCharSet )]
			public static extern SDKReturnCode CoreSdk_RegisterCallbackForRawDeviceDataStream( OnInternalRawDeviceData p_OnRawDeviceData );

			#endregion
			
		}
	}
}
