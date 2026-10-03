using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Liminal
{
    public sealed class DisplayBrightness : MonoBehaviour
    {
        public const string PreferenceKey = "brightnessExposure";
        public const float MinOffset = -1f;
        public const float MaxOffset = 3f;

        public float Offset { get; private set; }
        public float BaseExposure { get; private set; }
        public float AppliedExposure => BaseExposure + Offset;
        public bool Available { get; private set; }

        private Volume targetVolume;
        private VolumeProfile previousProfile;
        private VolumeProfile runtimeProfile;
        private ColorAdjustments runtimeAdjustments;
        private Bloom runtimeBloom;
        float bloomIntensity,bloomScatter,bloomClamp;
        bool bloomClampOverride;
        public float FinaleGlow { get; private set; }
        private List<VolumeComponent> runtimeComponents;
        private bool hadPreviousProfile;

        public void Initialize(Camera camera)
        {
            RestoreRuntimeProfile();
            Available = false;
            BaseExposure = 0f;
            Reload();

            if (camera == null)
            {
                Debug.LogWarning("DisplayBrightness requires a camera.", this);
                return;
            }

            var volumeLayerMask = 1;
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
                volumeLayerMask = cameraData.volumeLayerMask.value;

            Volume selectedVolume = null;
            var selectedPriority = float.NegativeInfinity;
            var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var volume in volumes)
            {
                // Other scene objects may not have reached OnEnable during Experience.Awake.
                if (!volume.enabled || !volume.gameObject.activeInHierarchy || !volume.isGlobal || volume.weight <= 0f)
                    continue;

                if ((volumeLayerMask & (1 << volume.gameObject.layer)) == 0)
                    continue;

                var sharedProfile = volume.sharedProfile;
                if (sharedProfile == null || !sharedProfile.TryGet<ColorAdjustments>(out _))
                    continue;

                if (selectedVolume == null || volume.priority > selectedPriority)
                {
                    selectedVolume = volume;
                    selectedPriority = volume.priority;
                }
            }

            if (selectedVolume == null)
            {
                Debug.LogWarning("DisplayBrightness could not find an active global Volume with Color Adjustments on the camera's volume layer mask.", this);
                return;
            }

            hadPreviousProfile = selectedVolume.HasInstantiatedProfile();
            previousProfile = hadPreviousProfile ? selectedVolume.profile : null;
            var sourceProfile = previousProfile != null ? previousProfile : selectedVolume.sharedProfile;
            ColorAdjustments sourceAdjustments = null;
            if (sourceProfile == null || !sourceProfile.TryGet<ColorAdjustments>(out sourceAdjustments)
                || sourceAdjustments.postExposure == null)
            {
                sourceProfile = selectedVolume.sharedProfile;
                if (sourceProfile != null)
                    sourceProfile.TryGet<ColorAdjustments>(out sourceAdjustments);
            }

            if (sourceProfile == null || sourceAdjustments == null || sourceAdjustments.postExposure == null)
            {
                Debug.LogWarning("DisplayBrightness could not read Color Adjustments from the selected Volume profile.", this);
                return;
            }

            var baseExposure = sourceAdjustments.postExposure.value;
            if (float.IsNaN(baseExposure) || float.IsInfinity(baseExposure))
            {
                Debug.LogWarning("DisplayBrightness found a non-finite base exposure; brightness adjustment is unavailable.", this);
                return;
            }

            List<VolumeComponent> clonedComponents = new List<VolumeComponent>();
            try
            {
                runtimeProfile = CloneProfile(sourceProfile, clonedComponents);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return;
            }

            if (!runtimeProfile.TryGet<ColorAdjustments>(out runtimeAdjustments)
                || runtimeAdjustments.postExposure == null)
            {
                DestroyOwnedProfile(runtimeProfile, clonedComponents);
                runtimeProfile = null;
                runtimeAdjustments = null;
                Debug.LogWarning("DisplayBrightness could not clone Color Adjustments from the selected Volume profile.", this);
                return;
            }

            targetVolume = selectedVolume;
            previousProfile = hadPreviousProfile ? previousProfile : null;
            runtimeComponents = clonedComponents;
            BaseExposure = baseExposure;
            targetVolume.profile = runtimeProfile;
            if(runtimeProfile.TryGet<Bloom>(out runtimeBloom)) {
                bloomIntensity=runtimeBloom.intensity.value;
                bloomScatter=runtimeBloom.scatter.value;
                bloomClamp=runtimeBloom.clamp.value;
                bloomClampOverride=runtimeBloom.clamp.overrideState;
            }
            Available = true;
            ApplyOffset();
        }

        public void SetOffset(float offset)
        {
            Offset = NormalizeOffset(offset, "brightness offset");
            ApplyOffset();
        }
        public void SetFinaleGlow(float amount)
        {
            FinaleGlow=Mathf.Clamp01(amount);
            if(runtimeBloom==null) return;
            runtimeBloom.intensity.value=Mathf.Lerp(bloomIntensity,Mathf.Min(.75f,bloomIntensity),FinaleGlow);
            runtimeBloom.scatter.value=Mathf.Lerp(bloomScatter,Mathf.Min(.58f,bloomScatter),FinaleGlow);
            runtimeBloom.clamp.value=Mathf.Lerp(bloomClamp,12f,FinaleGlow);
            runtimeBloom.clamp.overrideState=FinaleGlow>0 || bloomClampOverride;
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(PreferenceKey, Offset);
        }

        public void Reload()
        {
            Offset = NormalizeOffset(PlayerPrefs.GetFloat(PreferenceKey, 0f), "saved brightness offset");
            ApplyOffset();
        }

        private float NormalizeOffset(float value, string source)
        {
            if (float.IsNaN(value))
            {
                Debug.LogWarning($"DisplayBrightness received a NaN {source}; using the default offset.", this);
                value = 0f;
            }
            else if (float.IsInfinity(value))
            {
                Debug.LogWarning($"DisplayBrightness received a non-finite {source}; clamping it to the supported range.", this);
            }

            return Mathf.Clamp(value, MinOffset, MaxOffset);
        }

        private void ApplyOffset()
        {
            if (!Available || runtimeAdjustments == null)
                return;

            runtimeAdjustments.postExposure.value = AppliedExposure;
            runtimeAdjustments.postExposure.overrideState = true;
        }

        private void RestoreRuntimeProfile()
        {
            if (runtimeProfile != null)
            {
                if (targetVolume != null && targetVolume.HasInstantiatedProfile() && targetVolume.profile == runtimeProfile)
                    targetVolume.profile = hadPreviousProfile ? previousProfile : null;

                DestroyOwnedProfile(runtimeProfile, runtimeComponents);
            }

            targetVolume = null;
            previousProfile = null;
            runtimeProfile = null;
            runtimeAdjustments = null;
            runtimeBloom = null;FinaleGlow=0;
            runtimeComponents = null;
            hadPreviousProfile = false;
        }

        private static VolumeProfile CloneProfile(VolumeProfile source, List<VolumeComponent> clonedComponents)
        {
            var clone = ScriptableObject.CreateInstance<VolumeProfile>();
            clone.name = source.name + " (Display Brightness Runtime)";
            clone.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                foreach (var component in source.components)
                {
                    if (component == null)
                        continue;

                    var componentClone = UnityEngine.Object.Instantiate(component);
                    componentClone.hideFlags = HideFlags.HideAndDontSave;
                    clonedComponents.Add(componentClone);
                    clone.components.Add(componentClone);
                }

                return clone;
            }
            catch
            {
                DestroyOwnedProfile(clone, clonedComponents);
                throw;
            }
        }

        private static void DestroyOwnedProfile(VolumeProfile profile, List<VolumeComponent> components)
        {
            if (profile != null && profile.components != null)
                profile.components.Clear();

            if (components != null)
            {
                foreach (var component in components)
                    DestroyOwnedObject(component);

                components.Clear();
            }

            DestroyOwnedObject(profile);
        }

        private static void DestroyOwnedObject(UnityEngine.Object instance)
        {
            if (instance == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(instance);
            else
                UnityEngine.Object.DestroyImmediate(instance);
        }

        private void OnDestroy()
        {
            RestoreRuntimeProfile();
        }
    }
}
