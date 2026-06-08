using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Manus.Editor
{
	[InitializeOnLoad]
	public class XRPluginManifestChecker
	{
		// Path to the project's manifest.json file relative to the Assets folder
		private static readonly string s_ManifestPath;

		// List of common Unity XR package names we want to check for in the manifest
		private static readonly string[] s_XRPackageNames = new[]
		{
			"com.unity.xr.openxr",
			"com.unity.xr.oculus",
			"com.unity.xr.windowsmr",
			"com.unity.xr.visionos"
		};

		// Static constructor is called when the Unity Editor is loaded
		static XRPluginManifestChecker()
		{
			// Construct the full path to manifest.json.
			// Application.dataPath points to the "Assets" folder.
			// We go up one directory ("..") to the project root, then into the "Packages" folder.
			s_ManifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

			// We use delayCall to wait until the Editor is fully initialized and files are accessible
			EditorApplication.delayCall += CheckForInstalledXRPlugins;
		}

		private static void CheckForInstalledXRPlugins()
		{
			// Check if the manifest file exists
			if (!File.Exists(s_ManifestPath))
			{
				// This case is highly unusual for a valid Unity project
				Debug.LogError("Error: Packages manifest.json not found at the expected path: " + s_ManifestPath);
				return;
			}

			bool t_AnyXRPluginFound = false;
			try
			{
				// Read the entire content of the manifest.json file
				string t_ManifestContent = File.ReadAllText(s_ManifestPath);

				// Check if the content contains any of the known XR package names
				foreach (string t_PackageName in s_XRPackageNames)
				{
					// We look for the package name surrounded by quotes, as it appears in the JSON key
					if (t_ManifestContent.Contains($"\"{t_PackageName}\""))
					{
						 t_AnyXRPluginFound = true;
						 // Found at least one, no need to check for others for the general warning
						 break;
					}
				}

				// If any XR plugin package name was found in the manifest
				if (!t_AnyXRPluginFound)
				{
					Debug.LogWarning(
						"Your project isn't set up for XR.\n" +
						"The MANUS plugin Interaction Scene might not work correctly."
					);
				}
				else
				{
					Debug.Log("Found at least one Unity XR plugin package in the project's Packages/manifest.json.");
				}
			}
			catch (System.Exception t_Exception)
			{
				// Catch any potential errors during file reading
				Debug.LogError("Error reading manifest.json to check for XR plugins: " + t_Exception.Message);
			}
		}
	}
}
