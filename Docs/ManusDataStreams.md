# Manus ZMQ Data Streams — JSON Wire Format

Reference for every message published by `ManusDataStreamReceiver`
(`Assets/Scripts/ManusDataStreamReceiver.cs`). Unity subscribes to all Manus Core data and
connection streams, serializes each payload with Unity `JsonUtility`, wraps it in a common
envelope, and publishes it over ZeroMQ. This document is the contract for external consumers
(e.g. the Python side): topics, envelope, per-stream payload schemas, examples, caveats, and
enum value tables.

Derived from the C# types in `Assets/Manus/unity-plugin/Scripts/Core/ManusSDKTypes.cs`,
`ManusDLLImport.cs`, `CommunicationHub.cs`, and the receiver itself. Field names in JSON are
**exactly** the C# field names.

---

## 1. Transport

| Property | Value |
|---|---|
| Socket | ZeroMQ **PUB** (NetMQ), bound by Unity |
| Address | `tcp://127.0.0.1:5560` (Inspector-configurable on `ManusDataStreamReceiver`) |
| Message | 2 frames: **frame 0** = stream name (UTF-8), **frame 1** = envelope JSON (UTF-8) |
| Send HWM | 1000 messages per subscriber; frames beyond it are dropped |

- Subscribers can filter on frame 0 (the topic) without parsing JSON.
- **PUB/SUB is lossy.** Late subscribers miss earlier messages (slow joiner); a stalled
  subscriber drops frames past the high-water mark. Continuous streams self-heal on the next
  tick, but the one-shot `connected` / `disconnected` events can be missed entirely.
  **`landscape` fires ~1 Hz and is the liveness signal.**
- Each stream has independent `log` / `publish` toggles in the Inspector
  (`StreamOutput`). A stream appears on the wire only if its `publish` toggle is on.
- One JSON object per ZMQ message; no newline framing, no extra whitespace.

Minimal Python subscriber (pyzmq, as installed in `code\UltraPose\.venv`):

```python
import json, zmq

ctx = zmq.Context.instance()
sub = ctx.socket(zmq.SUB)
sub.connect("tcp://127.0.0.1:5560")
sub.setsockopt_string(zmq.SUBSCRIBE, "")          # "" = all streams, or e.g. "skeleton"

while True:
    stream, payload = sub.recv_multipart()        # [topic][envelope JSON]
    msg = json.loads(payload)                     # msg["stream"] == stream.decode()
```

---

## 2. Envelope

Every message on every topic has the same wrapper:

```jsonc
{
  "stream": string,                       // topic name, identical to ZMQ frame 0
  "unityEmitUnixTimeSec": number,         // wall clock, UTC seconds since 1970-01-01, 3 decimals (ms precision)
  "manusPublishUnixTimeSec": number,      // Manus Core publishTime as UTC Unix seconds, 3 decimals (ms precision)
  "unityRealtimeSinceStartupSec": number, // monotonic seconds since Unity started, 6 decimals
  "payload": object                       // stream-specific (sections below)
}
```

Example:

```json
{"stream":"skeleton","unityEmitUnixTimeSec":1781191812.345,"manusPublishUnixTimeSec":1781191812.339,"unityRealtimeSinceStartupSec":127.483912,"payload":{}}
```

Timestamp semantics:

- All three times are JSON **numbers** (not strings). The two Unity times are captured **when
  the envelope is built** in Unity — they measure emit time, not capture time on the gloves.
- `unityEmitUnixTimeSec` — wall clock (UTC Unix seconds, millisecond precision in the
  fractional part).
- `manusPublishUnixTimeSec` — Manus Core's own `publishTime` (the §3.4 bitfield) decoded to
  UTC Unix seconds (ms precision) so consumers don't have to decode it. Unity uses the SDK's
  official decoder (`CoreSdk_GetTimestampInfo`). The streams that carry a `publishTime`
  (`skeleton`, `ergonomics`, `rawSkeleton`, `rawDevice`, `gesture`, `tracker`) report the decoded
  Core time, which **slightly precedes** `unityEmitUnixTimeSec` (the Core→Unity hop). The streams
  that don't (`landscape`, `system`, `connected`, `disconnected`, `hostsFound`) — and the rare
  cases where decoding fails or the timestamp is in timecode mode — set this **equal to**
  `unityEmitUnixTimeSec`. So you can always read this one field; if you must tell "real Core time"
  from the fallback, compare it against `unityEmitUnixTimeSec` and/or key off the topic.
- `unityRealtimeSinceStartupSec` — monotonic since Unity startup (editor launch in the
  Editor, app launch in builds — **not** since entering play mode). Use for ordering /
  intervals; immune to wall-clock jumps.

---

## 3. Common types

These shapes recur throughout the payloads. All numeric types are serialized as JSON numbers.
Type abbreviations used below: `u32` = uint32, `i32` = int32, `u64` = uint64, `f32` = float,
`bool` = JSON true/false, `int(EnumName)` = integer, see enum appendix (§6).

### 3.1 ManusVec3

```jsonc
{ "x": f32, "y": f32, "z": f32 }
```

### 3.2 ManusQuaternion — note `w` first

```jsonc
{ "w": f32, "x": f32, "y": f32, "z": f32 }
```

### 3.3 ManusTransform

```jsonc
{
  "position": ManusVec3,
  "rotation": ManusQuaternion,
  "scale":    ManusVec3
}
```

**Coordinate system** (requested from Core at SDK init, `ManusDLLImport.cs:129-136`):
left-handed, **+Y up**, **+Z away from the viewer**, unit scale **1.0 = meters** — i.e. the
Unity convention. All positions/rotations in all streams use it.

### 3.4 ManusTimestamp (`publishTime`, `lastUpdateTime`, `lastSeen`, …)

```jsonc
{ "time": u64 }
```

`time` is a **packed UTC calendar bitfield — not Unix time, not local time**. The bit layout
is not in the Manus docs (official decoder is `CoreSdk_GetTimestampInfo` in ManusSDK.dll);
it was determined and verified empirically against Unity's UTC clock:

| Bits (LSB up) | Width | Field |
|---|---|---|
| 0–9 | 10 | fraction — milliseconds (frames if bit 63 set) |
| 10–15 | 6 | second |
| 16–21 | 6 | minute |
| 22–26 | 5 | hour |
| 27–31 | 5 | day |
| 32–35 | 4 | month |
| 36–62 | 27 | year |
| 63 | 1 | timecode flag — when set, fraction counts frames at the `landscape` `time.fps` rate, not ms |

Python decode:

```python
from datetime import datetime, timezone

def manus_time_to_unix(t: int) -> float:
    if t >> 63:
        raise ValueError("timecode mode: fraction is frames, needs landscape time.fps")
    return datetime(
        year=(t >> 36) & 0x7FFFFFF,
        month=(t >> 32) & 0xF,
        day=(t >> 27) & 0x1F,
        hour=(t >> 22) & 0x1F,
        minute=(t >> 16) & 0x3F,
        second=(t >> 10) & 0x3F,
        microsecond=(t & 0x3FF) * 1000,
        tzinfo=timezone.utc,                  # ALWAYS UTC — local time is wrong
    ).timestamp()
```

Worked example: `139252970959193` decodes to `2026-06-11 15:30:12.345 UTC`.

Never do arithmetic (deltas, comparisons across field boundaries) on the raw `time` value —
it rolls over odometer-style at each calendar field. Decode first.

For the **stream-level** `publishTime`, the envelope already exposes this decoded to UTC Unix
seconds as `manusPublishUnixTimeSec` (§2) — prefer that. You only need this decoder for the other
timestamps embedded in payloads (`lastUpdateTime`, `lastSeen`, `firmwareTimestamp`, calibration
times, …).

### 3.5 Version

```jsonc
{ "major": u32, "minor": u32, "patch": u32, "label": string, "sha": string, "tag": string }
```

### 3.6 ManusHost

```jsonc
{ "hostName": string, "ipAddress": string, "manusCoreVersion": Version }
```

Either `hostName` or `ipAddress` may be blank (`""`); the other is used when connecting.

---

## 4. Streams

| # | Topic | Payload | Cadence |
|---|---|---|---|
| 4.1 | `skeleton` | animated skeleton poses | continuous, Core stream rate |
| 4.2 | `ergonomics` | finger-joint angle sets | continuous |
| 4.3 | `rawSkeleton` | per-glove estimated hand skeletons | continuous |
| 4.4 | `rawDevice` | raw glove sensor data (Metaglove Pro) | continuous |
| 4.5 | `gesture` | gesture probabilities | continuous |
| 4.6 | `tracker` | tracker poses | continuous |
| 4.7 | `landscape` | full system snapshot | ~1 Hz |
| 4.8 | `system` | Core system messages | event |
| 4.9 | `connected` | host info | once per connect |
| 4.10 | `disconnected` | `{}` | once per disconnect |
| 4.11 | `hostsFound` | discovered hosts | re-fires ~10 s while disconnected |

### 4.1 `skeleton` — `CoreSDK.SkeletonStream`

Animated skeleton data for every skeleton loaded in Manus Core / the Unity session.

```jsonc
{
  "publishTime": ManusTimestamp,
  "skeletons": [
    {
      "id": u32,                       // skeleton id (see landscape skeletons[].id)
      "nodes": [
        {
          "id": u32,                   // node/joint id within the skeleton
          "transform": ManusTransform
        }
      ]
    }
  ]
}
```

Example (1 skeleton, 2 nodes):

```json
{"stream":"skeleton","unityEmitUnixTimeSec":1781191812.351,"manusPublishUnixTimeSec":1781191812.345,"unityRealtimeSinceStartupSec":127.489102,"payload":{
  "publishTime":{"time":139252970959193},
  "skeletons":[{"id":3,"nodes":[
    {"id":0,"transform":{"position":{"x":0.012,"y":1.342,"z":0.207},"rotation":{"w":0.998,"x":0.011,"y":-0.052,"z":0.003},"scale":{"x":1.0,"y":1.0,"z":1.0}}},
    {"id":1,"transform":{"position":{"x":0.031,"y":1.355,"z":0.231},"rotation":{"w":0.985,"x":0.094,"y":-0.141,"z":0.022},"scale":{"x":1.0,"y":1.0,"z":1.0}}}
  ]}]
}}
```

### 4.2 `ergonomics` — `CommunicationHub.ErgonomicsStream`

Joint-angle ("ergonomics") values per user/skeleton. The list is pre-trimmed by the plugin to
valid entries only.

```jsonc
{
  "publishTime": ManusTimestamp,
  "data": [
    {
      "id": u32,                 // user id if isUserID, otherwise skeleton id
      "isUserID": bool,
      "data": [ f32 ]            // ALWAYS exactly 40 values; index = ErgonomicsDataType (§6.1)
    }
  ]
}
```

- `data` (inner) is a fixed-size marshaled array: always 40 floats, even for one-handed
  setups. Index it with the `ErgonomicsDataType` table (§6.1) — left hand 0–19, right hand
  20–39, four values (MCP spread, MCP stretch, PIP stretch, DIP stretch) per finger.

```jsonc
// example payload (40-element array abbreviated)
{
  "publishTime":{"time":139252970959193},
  "data":[{"id":1,"isUserID":true,"data":[12.1,33.5,18.2,9.4 /* …36 more, indices 4–39 */]}]
}
```

### 4.3 `rawSkeleton` — `CoreSDK.RawSkeletonStream`

Hand skeletons estimated by Core directly from glove data (no retargeting to a user
skeleton). Same node shape as `skeleton`, but keyed by glove.

```jsonc
{
  "publishTime": ManusTimestamp,
  "skeletons": [
    {
      "gloveId": u32,                  // which glove produced this estimate
      "nodes": [
        { "id": u32, "transform": ManusTransform }
      ]
    }
  ]
}
```

### 4.4 `rawDevice` — `CoreSDK.RawData`

Raw sensor data per glove (Metaglove Pro).

```jsonc
{
  "publishTime": ManusTimestamp,
  "rawDeviceData": [
    {
      "id": u32,                        // glove id
      "sensorCount": u32,               // number of VALID entries in sensorData
      "sensorData": [ ManusTransform ], // ALWAYS exactly 5 entries; only the first sensorCount are valid
      "rotation": ManusQuaternion       // overall device rotation from the IMU
    }
  ]
}
```

- `sensorData` is a fixed-size marshaled array — it always serializes 5 transforms.
  Entries at index ≥ `sensorCount` are garbage/zero; ignore them.

### 4.5 `gesture` — `CoreSDK.GestureStream`

Gesture recognition probabilities per user/skeleton.

```jsonc
{
  "publishTime": ManusTimestamp,
  "gestureProbabilities": [
    {
      "id": u32,                 // user id if isUserID, otherwise skeleton id
      "isUserID": bool,
      "totalGestureCount": u32,  // ALWAYS 0 — do not use (see below)
      "gestureData": [
        { "id": u32, "percent": f32 }   // gesture id (see landscape gestures[]), confidence
      ],
      "gestureCount": u32        // ALWAYS 0 — do not use (see below)
    }
  ]
}
```

- **The two count fields are always 0.** The plugin's DLL wrapper allocates `gestureData`
  to the real total and fills it completely, but never copies the counts into the published
  struct (`ManusDLLImport.cs:972-1003`). **`gestureData.length` is the real count.**
- Gesture `id` values map to names via the `landscape` payload's `gestures[]` list.

### 4.6 `tracker` — `CoreSDK.TrackerStream`

```jsonc
{
  "publishTime": ManusTimestamp,
  "trackers": [
    {
      "lastUpdateTime": ManusTimestamp,
      "trackerId": { "id": string },     // tracker name, ≤ 32 chars
      "userId": u32,
      "isHmd": bool,
      "trackerType": int(TrackerType),   // §6.2
      "rotation": ManusQuaternion,
      "position": ManusVec3,
      "quality": int(TrackingQuality)    // §6.2
    }
  ]
}
```

### 4.7 `landscape` — `CommunicationHub.Landscape`

Full system snapshot: devices, users, skeletons, trackers, gestures, net devices, settings,
timecode state. Fires ~1 Hz — use as heartbeat and as the lookup table for ids appearing in
the data streams. All seven lists are pre-trimmed by the plugin to valid entries only
(`CommunicationHub.cs:1682-1727`).

```jsonc
{
  "dongles": [ DongleLandscapeData ],
  "gloves": [ GloveLandscapeData ],
  "users": [ UserLandscapeData ],
  "skeletons": [ SkeletonLandscapeData ],
  "trackers": [ TrackerLandscapeData ],
  "gestures": [ { "id": u32, "name": string } ],
  "netDevices": [ { "netDeviceID": u32, "hostname": string, "ip": string } ],
  "settings": SettingsLandscape,
  "time": TimeLandscape
}
```

**DongleLandscapeData**

```jsonc
{
  "id": u32,
  "classType": int(DeviceClassType),       // §6.3
  "familyType": int(DeviceFamilyType),     // §6.3
  "isHaptics": bool,
  "hardwareVersion": Version,
  "firmwareVersion": Version,
  "firmwareTimestamp": ManusTimestamp,
  "chargingState": u32,
  "channel": i32,                          // radio channel
  "updateStatus": int(UpdateStatusEnum),   // §6.4
  "licenseType": string,
  "lastSeen": ManusTimestamp,
  "leftGloveID": u32,
  "rightGloveID": u32,
  "licenseLevel": int(LicenseType),        // §6.4
  "licenseExpiration": ManusTimestamp,
  "licenseMaxNumberOfGlovePairs": u32,
  "netDeviceID": u32
}
```

**GloveLandscapeData**

```jsonc
{
  "id": u32,
  "classType": int(DeviceClassType),
  "familyType": int(DeviceFamilyType),
  "side": int(Side),                       // §6.3 — 1=Left, 2=Right
  "isHaptics": bool,
  "pairedState": int(DevicePairedState),   // §6.3
  "dongleID": u32,
  "hardwareVersion": Version,
  "firmwareVersion": Version,
  "firmwareTimestamp": ManusTimestamp,
  "updateStatus": int(UpdateStatusEnum),
  "batteryPercentage": u32,                // 0–100
  "transmissionStrength": i32,             // signal strength
  "iMUCalibrationInfo": [                  // ALWAYS exactly 6 entries (one per IMU)
    { "mag": u32, "acc": u32, "gyr": u32, "sys": u32 }
  ],
  "lastSeen": ManusTimestamp,
  "excluded": bool,
  "netDeviceID": u32
}
```

**UserLandscapeData**

```jsonc
{
  "id": u32,
  "name": string,
  "dongleID": u32,
  "leftGloveID": u32,
  "rightGloveID": u32,
  "profile": {
    "profileType": int(ProfileType),       // §6.5 — 0=Hands, 1=FullBody
    "measurements": [                      // ALWAYS exactly 20 entries
      {
        "entryType": int(MeasurementType), // §6.5
        "value": f32,
        "unit": int(MeasurementUnit),      // §6.5 — 0=Meters, 1=Percentage
        "category": int(MeasurementCategory), // §6.5
        "displayName": string
      }
    ],
    "trackerOffsets": [                    // ALWAYS exactly 16 entries
      {
        "entryType": int(TrackerOffsetType), // §6.5
        "translation": ManusVec3,
        "rotation": ManusQuaternion
      }
    ],
    "extraTrackerOffsets": [               // ALWAYS exactly 4 entries
      { "entryType": int(ExtraTrackerOffsetType), "value": f32 }  // §6.5
    ],
    "leftGloveCalibrationTimestamp": ManusTimestamp,
    "rightGloveCalibrationTimestamp": ManusTimestamp
  },
  "userIndex": u32
}
```

**SkeletonLandscapeData**

```jsonc
{
  "id": u32,                     // matches skeleton stream skeletons[].id
  "session": string,
  "userId": u32,
  "type": int(SkeletonType),     // §6.5 — 1=Hand, 2=Body, 3=Both
  "rootBoneName": string,
  "scaled": bool
}
```

**TrackerLandscapeData**

```jsonc
{
  "id": string,                          // matches tracker stream trackerId.id
  "type": int(TrackerType),              // §6.2
  "systemType": int(TrackerSystemType),  // §6.2
  "user": u32,
  "isHMD": bool,                         // note: capital MD here; tracker stream uses isHmd
  "manufacturer": string,
  "productName": string
}
```

**SettingsLandscape**

```jsonc
{
  "manusCoreVersion": Version,
  "license": {
    "maxGlovePairs": u32,
    // 24 feature flags:
    "recording": bool, "exporting": bool, "advancedExporting": bool,
    "unitySession": bool, "unrealSession": bool, "openXRSession": bool,
    "sdk": bool, "raw": bool, "mobuSession": bool, "xsensSession": bool,
    "optitrackSession": bool, "qualisysSession": bool, "viconSession": bool,
    "nokovSession": bool, "icidoSession": bool, "siemensSession": bool,
    "vredSession": bool, "integrated": bool, "cortexSession": bool,
    "fzMotionSession": bool, "worldVizSession": bool, "noraxonSession": bool,
    "noitomSession": bool, "abletonSession": bool
  },
  "playbackMode": bool,
  "ignoreSessionTimeOuts": bool,
  "firmwareOne": { "version": i32, "timestamp": ManusTimestamp },
  "firmwareTwo": { "version": i32, "timestamp": ManusTimestamp },
  "recordingMode": bool,
  "isNetDevice": bool,
  "isConnectedAsNetDevice": bool
}
```

**TimeLandscape**

```jsonc
{
  "interfaces": [                          // ALWAYS exactly 32 entries; only interfaceCount valid
    { "name": string, "api": string, "index": i32 }
  ],
  "interfaceCount": u32,
  "currentInterface": { "name": string, "api": string, "index": i32 },
  "fps": int(TimecodeFPS),                 // §6.6 — needed to decode timecode-mode timestamps
  "fakeTimecode": bool,
  "useSyncPulse": bool,
  "deviceKeepAlive": bool,
  "syncStatus": bool,
  "timecodeStatus": bool,
  "ltcChannel": i32
}
```

### 4.8 `system` — `CoreSDK.SystemMessage`

Status/diagnostic messages from Core (event-driven).

```jsonc
{
  "type": int(SystemMessageType),  // §6.7
  "infoString": string,            // ≤ 256 chars
  "infoUInt": u32                  // meaning depends on type (e.g. modified skeleton id)
}
```

### 4.9 `connected`

Fires once when Unity connects to Manus Core. Payload is a `ManusHost` (§3.6) for the
current host, or `{}` if the host record is unavailable.

```json
{"hostName":"DESKTOP-AB12CD","ipAddress":"127.0.0.1","manusCoreVersion":{"major":2,"minor":4,"patch":0,"label":"","sha":"1a2b3c4d","tag":""}}
```

### 4.10 `disconnected`

Fires once when the connection to Core is lost. Payload is always `{}`.

### 4.11 `hostsFound`

Result of host discovery. **Not one-shot** — re-fires every ~10 s while disconnected (host
auto-find cycle); expect repeats.

```jsonc
{ "hosts": [ ManusHost ] }   // §3.6; may be empty: {"hosts":[]}
```

---

## 5. Serialization notes

- **Serializer is Unity `JsonUtility`.** Field names are the exact C# names (note oddities
  kept verbatim: `iMUCalibrationInfo`, `isHmd` vs `isHMD`). Every field is always present —
  no omissions, no `null`s; blank strings are `""`.
- **Enums are integers.** Use the tables in §6; never assume names travel on the wire.
- **Bools are JSON `true`/`false`.**
- **Fixed-size marshaled arrays serialize in full**, valid or not:
  `rawDevice` `sensorData` (5, valid count in `sensorCount`), ergonomics inner `data` (40),
  glove `iMUCalibrationInfo` (6), profile `measurements` (20) / `trackerOffsets` (16) /
  `extraTrackerOffsets` (4), `time.interfaces` (32, valid count in `interfaceCount`).
- **Plugin-trimmed lists contain only valid entries** (no count field needed): all seven
  `landscape` lists and the outer `ergonomics` `data` list.
- **Gesture counts**: `totalGestureCount` and `gestureCount` are always 0;
  `gestureData.length` is the truth (§4.5).
- **uint64 precision (JavaScript)**: normal `publishTime.time` values (~1.4 × 10¹⁴) are
  exact in a double, but with the bit-63 timecode flag set the value exceeds 2⁵³ and
  `JSON.parse` will corrupt it — use a BigInt-aware parser. Python `json` is exact either way.
- All stream handlers run on the Unity main thread; messages within one topic are in order
  (ZMQ PUB preserves per-connection ordering), but drops are possible (§1).

---

## 6. Enum appendix

Integers on the wire; tables from `ManusSDKTypes.cs`.

### 6.1 ErgonomicsDataType (index into ergonomics `data[40]`)

Four values per finger: MCP spread, MCP stretch, PIP stretch, DIP stretch.

| Finger | MCP Spread | MCP Stretch | PIP Stretch | DIP Stretch |
|---|---|---|---|---|
| Left Thumb | 0 | 1 | 2 | 3 |
| Left Index | 4 | 5 | 6 | 7 |
| Left Middle | 8 | 9 | 10 | 11 |
| Left Ring | 12 | 13 | 14 | 15 |
| Left Pinky | 16 | 17 | 18 | 19 |
| Right Thumb | 20 | 21 | 22 | 23 |
| Right Index | 24 | 25 | 26 | 27 |
| Right Middle | 28 | 29 | 30 | 31 |
| Right Ring | 32 | 33 | 34 | 35 |
| Right Pinky | 36 | 37 | 38 | 39 |

(For the thumb, the four slots are named CMC/MCP/IP in anatomical terms but keep the same
spread/stretch ordering in the SDK.)

### 6.2 Tracker enums

**TrackerType**: 0 Unknown, 1 Head, 2 Waist, 3 LeftHand, 4 RightHand, 5 LeftFoot,
6 RightFoot, 7 LeftUpperArm, 8 RightUpperArm, 9 LeftUpperLeg, 10 RightUpperLeg,
11 Controller, 12 Camera

**TrackingQuality**: 0 Untrackable, 1 BadTracking, 2 Trackable

**TrackerSystemType**: 0 Unknown, 1 Antilatency, 2 ART, 3 OpenVR, 4 Optitrack, 5 Vicon,
6 OpenXR

### 6.3 Device enums

**DeviceClassType**: 0 Unknown, 1 Dongle, 2 Glove, 3 Glongle

**DeviceFamilyType**: 0 Unknown, 1 Prime1, 2 Prime2, 3 PrimeX, 4 Metaglove, 5 Prime3,
6 Virtual, 7 MetaglovePro, 8 MetagloveProPrecision, 9 MetagloveProHaptics,
10 MetagloveProPrecisionHaptics

**Side**: 0 Invalid, 1 Left, 2 Right, 3 Center

**DevicePairedState**: 0 Unknown, 1 Paired, 2 Unpaired, 3 Pairing

### 6.4 Update / license enums

**UpdateStatusEnum**: 0 Unknown, 1 NoUpdateAvailable, 2 UpdateAvailable,
3 MandatoryUpdateAvailable, 4 UpdatingInitializing, 5 UpdatingBootloader,
6 UpdatingFirmware, 7 UpdatingFinalizing, 8 UpdatingFailed

**LicenseType**: 0 Undefined, 1 Polygon, 2 CoreXO, 3 CorePro, 4 CoreXOPro, 5 CoreX,
6 CoreO, 7 CoreQ, 8 CoreXPro, 9 CoreOPro, 10 CoreQPro, 11 CoreXOQPro, 12 CoreXR,
13 CorePrimeThree, 14 Feature

### 6.5 User-profile / skeleton enums

**ProfileType**: 0 Hands, 1 FullBody

**MeasurementType**: 0 Unknown, 1 PlayerHeight, 2 SpineLength, 3 NeckLength,
4 UpperArmLength, 5 LowerArmLength, 6 ArmLength, 7 ArmSpan, 8 UpperLegLength,
9 LowerLegLength, 10 LegLength, 11 HandLength, 12 FootLength, 13 HipWidth,
14 ShoulderWidth, 15 ShoulderHeight, 16 HeadLength, 17 Thickness, 18 ArmRatio, 19 LegRatio

**MeasurementUnit**: 0 Meters, 1 Percentage

**MeasurementCategory**: 0 Misc, 1 Generic, 2 Arms, 3 Legs, 4 Body

**TrackerOffsetType**: 0 Unknown, 1 HeadTrackerToHead, 2 HeadTrackerToTopOfHead,
3 LeftHandTrackerToWrist, 4 RightHandTrackerToWrist, 5 LeftFootTrackerToAnkle,
6 RightFootTrackerToAnkle, 7 HipTrackerToHip, 8 HipTrackerToLeftLeg,
9 HipTrackerToRightLeg, 10 LeftUpperArmTrackerToElbow, 11 RightUpperArmTrackerToElbow,
12 LeftUpperArmTrackerToShoulder, 13 RightUpperArmTrackerToShoulder,
14 LeftHandTrackerToCasing, 15 RightHandTrackerToCasing

**ExtraTrackerOffsetType**: 0 Unknown, 1 HeadForward, 2 HipForward, 3 HipHeight

**SkeletonType**: 0 Invalid, 1 Hand, 2 Body, 3 Both

### 6.6 TimecodeFPS

0 Undefined, 1 = 23.976, 2 = 24, 3 = 25, 4 = 29.97, 5 = 30, 6 = 48, 7 = 50, 8 = 59.94,
9 = 60, 10 = 29.97DF, 11 = 59.94DF (DF = drop-frame)

### 6.7 SystemMessageType

0 Unknown, 1 LibDebugReplugDongle, 2 LibDebugRxStall, 3 LibDebugTxStall, 4 TrackerError,
5 TrackerOk, 6 TrackerSystemOutOfDate, 7 GloveSanityErrorPSOCInit, 8 GloveSanityErrorQCBatV,
9 GloveSanityErrorQCLRACalib, 10 GloveSanityErrorQCFlexInit, 11 GloveSanityErrorQCIMUInit,
12 GloveSanityErrorQCIMUCalib, 13 GloveSanityErrorQCID, 14 GloveSanityErrorQCInterCPU,
15 SessionConnectionVersionMismatch, 16 TemporarySkeletonModified,
17 SessionRefusedDueToLicenseIssue, 18 LaunchDevTools, 19 MaxSeatMixing, 20 DeprecatedLicense
