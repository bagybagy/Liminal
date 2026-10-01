using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Liminal.Editor
{
    public static class PcVrSetup
    {
        const string SettingsFolder = "Assets/Liminal/XR/Settings";
        const string PerBuildTargetPath = SettingsFolder + "/XRGeneralSettingsPerBuildTarget.asset";
        const string GeneralSettingsPath = SettingsFolder + "/WindowsXRGeneralSettings.asset";
        const string ManagerSettingsPath = SettingsFolder + "/WindowsXRManagerSettings.asset";
        const string OpenXRLoaderPath = SettingsFolder + "/WindowsOpenXRLoader.asset";
        const string OpenXRSettingsFolder = "Assets/XR/Settings";
        const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";

        static readonly string[] WindowsControllerProfiles =
        {
            "OculusTouchControllerProfile",
            "ValveIndexControllerProfile",
            "HTCViveControllerProfile",
            "MicrosoftMotionControllerProfile",
            "KHRSimpleControllerProfile"
        };

        public static void Configure()
        {
            EnsureFolder("Assets/Liminal/XR");
            EnsureFolder(SettingsFolder);
            ConfigureActiveInputHandling();

            XRGeneralSettingsPerBuildTarget perBuildTarget = GetPerBuildTargetSettings();
            XRGeneralSettings general = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (general == null)
            {
                general = CreateAsset<XRGeneralSettings>(GeneralSettingsPath);
                perBuildTarget.SetSettingsForBuildTarget(BuildTargetGroup.Standalone, general);
            }

            XRManagerSettings manager = general.Manager;
            if (manager == null)
            {
                manager = CreateAsset<XRManagerSettings>(ManagerSettingsPath);
                general.Manager = manager;
            }

            ConfigureOpenXRLoader(manager);
            ConfigureOpenXRSettings();

            general.InitManagerOnStart = false;
            manager.automaticLoading = false;
            manager.automaticRunning = false;
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(perBuildTarget);
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perBuildTarget, true);
            AssetDatabase.SaveAssets();
        }

        static XRGeneralSettingsPerBuildTarget GetPerBuildTargetSettings()
        {
            if (EditorBuildSettings.TryGetConfigObject(
                    XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget configured) && configured != null)
                return configured;

            XRGeneralSettingsPerBuildTarget asset = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                PerBuildTargetPath);
            if (asset != null)
                return asset;

            return CreateAsset<XRGeneralSettingsPerBuildTarget>(PerBuildTargetPath);
        }

        static void ConfigureOpenXRLoader(XRManagerSettings manager)
        {
            for (int i = 0; i < manager.activeLoaders.Count; i++)
            {
                if (!(manager.activeLoaders[i] is OpenXRLoader existingLoader))
                    continue;
                if (i > 0)
                {
                    manager.TryRemoveLoader(existingLoader);
                    manager.TryAddLoader(existingLoader, 0);
                }
                return;
            }

            OpenXRLoader loader = AssetDatabase.LoadAssetAtPath<OpenXRLoader>(OpenXRLoaderPath);
            if (loader == null)
                loader = CreateAsset<OpenXRLoader>(OpenXRLoaderPath);

            if (!manager.TryAddLoader(loader, 0))
                throw new InvalidOperationException(
                    "[LIMINAL PCVR] Could not add the Windows OpenXR loader to XR Management.");
        }

        static void ConfigureOpenXRSettings()
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (settings == null)
            {
                CreateOpenXRSettings();
                settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
                if (settings == null)
                    throw new InvalidOperationException(
                        "[LIMINAL PCVR] OpenXR settings were created but the Standalone settings accessor still returned null.");
            }

            settings.renderMode = OpenXRSettings.RenderMode.MultiPass;
            HashSet<string> foundProfiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (OpenXRFeature feature in settings.GetFeatures())
            {
                if (feature == null)
                    continue;
                if (Array.IndexOf(WindowsControllerProfiles, feature.GetType().Name) < 0)
                    continue;

                feature.enabled = true;
                foundProfiles.Add(feature.GetType().Name);
                EditorUtility.SetDirty(feature);
            }

            List<string> missingProfiles = new List<string>();
            foreach (string profile in WindowsControllerProfiles)
            {
                if (!foundProfiles.Contains(profile))
                    missingProfiles.Add(profile);
            }
            if (missingProfiles.Count > 0)
                throw new InvalidOperationException(
                    "[LIMINAL PCVR] OpenXR settings are missing Windows controller interaction profiles: " +
                    string.Join(", ", missingProfiles) + ". Ensure the installed OpenXR package provides them.");

            EditorUtility.SetDirty(settings);
        }

        static OpenXRSettings CreateOpenXRSettings()
        {
            OpenXRManagementSettings package = new OpenXRManagementSettings();
            string settingsTypeName = package.metadata.settingsType;
            Type settingsType = FindLoadedType(settingsTypeName);
            if (settingsType == null || !typeof(ScriptableObject).IsAssignableFrom(settingsType))
                throw new InvalidOperationException(
                    "[LIMINAL PCVR] OpenXR package metadata has an invalid settings type: " + settingsTypeName);

            EnsureFolder("Assets/XR");
            EnsureFolder(OpenXRSettingsFolder);
            string settingsPath = OpenXRSettingsFolder + "/" + settingsType.Name + ".asset";
            ScriptableObject packageSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(settingsPath);
            if (packageSettings == null)
            {
                packageSettings = ScriptableObject.CreateInstance(settingsType);
                if (!package.PopulateNewSettingsInstance(packageSettings))
                {
                    UnityEngine.Object.DestroyImmediate(packageSettings);
                    throw new InvalidOperationException(
                        "[LIMINAL PCVR] The OpenXR package could not initialize its settings/profile assets.");
                }
                AssetDatabase.CreateAsset(packageSettings, settingsPath);
                AssetDatabase.SaveAssets();
            }

            OpenXRSettings settings = packageSettings as OpenXRSettings;
            if (settings == null)
                throw new InvalidOperationException(
                    "[LIMINAL PCVR] OpenXR package metadata settings type is not OpenXRSettings: " + settingsTypeName);
            return settings;
        }

        static Type FindLoadedType(string fullName)
        {
            string typeName = fullName.Split(',')[0].Trim();
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        static void ConfigureActiveInputHandling()
        {
            UnityEngine.Object[] settingsAssets = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsPath);
            foreach (UnityEngine.Object settingsAsset in settingsAssets)
            {
                if (settingsAsset == null)
                    continue;

                SerializedObject serializedSettings;
                try
                {
                    serializedSettings = new SerializedObject(settingsAsset);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                SerializedProperty activeInputHandler = serializedSettings.FindProperty("activeInputHandler");
                if (activeInputHandler == null ||
                    (activeInputHandler.propertyType != SerializedPropertyType.Integer &&
                     activeInputHandler.propertyType != SerializedPropertyType.Enum))
                    continue;

                if (activeInputHandler.intValue != 2)
                {
                    activeInputHandler.intValue = 2;
                    serializedSettings.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settingsAsset);
                }
                return;
            }

            throw new InvalidOperationException(
                "[LIMINAL PCVR] Could not locate PlayerSettings.activeInputHandler to enable Both input backends.");
        }

        static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
