#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using TCA_VR.Extras;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TCA_VR;

struct CameraRig
{
    public GameObject leftEye;
    public GameObject rightEye;

    public Camera leftEyeCam;
    public Camera rightEyeCam;
    public bool isCockpitCam;
}

[BepInPlugin("io.github.hrolfurgylfa.tca-vr", "TCA VR", "0.0.1")]
[BepInProcess("Arena.exe")]
public class Plugin : BaseUnityPlugin
{
    private HeadsetListener headsetListener = null!;
    private XRServerRunner xrServerRunner = null!;
    private List<CameraRig> rigs = new List<CameraRig>();
    private HeadsetPosData headsetOffset = new HeadsetPosData();
    private Config config;

    private void Awake()
    {
        var conf = ArgParse.ParseArgs();
        if (conf.HasValue) config = conf.Value;
        Logger.LogInfo("TCA VR is loaded!");
        SceneManager.sceneLoaded += this.OnSceneLoaded;
    }

    private void Start()
    {
        Logger.LogInfo("Render Pipeline: " + UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.GetType().Name);

        // Start listening for an xr server.
        headsetListener = new HeadsetListener(Logger);
        xrServerRunner = new XRServerRunner(Logger);
        xrServerRunner.Start(config);
        Logger.LogInfo("Finished starting headset listener and XR server runner.");
    }

    private void OnDestroy()
    {
        Logger.LogInfo("Unloading TCA VR.");
        SceneManager.sceneLoaded -= this.OnSceneLoaded;
        // TODO: Destroy headsetListener properly
        xrServerRunner.Dispose();
    }

    private void SetRigData(Data data)
    {
        // Calculate the current headset position with the offset
        var headsetPos = HeadsetPosData.FromEyes(data.leftEye, data.rightEye);
        var headsetPosWithOffset = headsetPos.sub(this.headsetOffset);

        foreach (var rig in rigs)
        {
            // Set the headset position and rotation with the current offset
            rig.leftEye.transform.localPosition = headsetPosWithOffset.leftEyePos;
            rig.rightEye.transform.localPosition = headsetPosWithOffset.rightEyePos;
            rig.leftEye.transform.localRotation = headsetPosWithOffset.leftEyeQuaternion;
            rig.rightEye.transform.localRotation = headsetPosWithOffset.rightEyeQuaternion;

            // Set the projection matrix
            float near_z;
            float far_z;
            if (rig.isCockpitCam)
            {
                near_z = 0.01f;
                far_z = 1000f;
            }
            else
            {
                near_z = 1f;
                far_z = 75000f;
            }
            rig.leftEyeCam.projectionMatrix = ProjectionMatrixExtras.CreateProjectionFov(data.leftEye.fov, near_z, far_z);
            rig.rightEyeCam.projectionMatrix = ProjectionMatrixExtras.CreateProjectionFov(data.rightEye.fov, near_z, far_z);
        }
    }

    private void Update()
    {
        // Reset position if the user requests it
        if (Input.GetKey(KeyCode.Comma))
        {
            var data = headsetListener.Read();
            this.headsetOffset = HeadsetPosData.FromEyes(data.leftEye, data.rightEye);
            this.headsetOffset.CenterEyes();
            SetRigData(data);
        }

        // Update the headset position if it wasn't reset already
        else if (!headsetListener.HasBeenRead())
            SetRigData(headsetListener.Read());

        // Enable/Disable the UI
        if (Input.GetKeyDown(KeyCode.Insert))
        {
            var UIObjectNames = new string[] { "GameScreenHUD", "PauseMenu", "GameAircraftHUD" };
            var uiItems = Resources
                .FindObjectsOfTypeAll<GameObject>()
                .Where<GameObject>(obj => UIObjectNames.Contains(obj.name))
                .ToArray();
            var uiEnabled = uiItems.FirstOrDefault()?.activeSelf ?? true;
            foreach (var uiItem in uiItems)
                uiItem.SetActive(!uiEnabled);
        }
    }

    private CameraRig CreateVRRig(GameObject leftEye, bool isCockpitCam)
    {
        // Create the right eye from the left eye
        GameObject rightEye = UnityEngine.Object.Instantiate(leftEye);
        rightEye.transform.SetParent(leftEye.transform.parent);
        rightEye.transform.position = leftEye.transform.position;
        rightEye.transform.rotation = leftEye.transform.rotation;
        rightEye.transform.localScale = leftEye.transform.localScale;
        Camera leftEyeCam = leftEye.GetComponent<Camera>();
        Camera rightEyeCam = rightEye.GetComponent<Camera>();

        Logger.LogInfo("Before:");
        Logger.LogInfo("Left eye FOV: " + leftEyeCam.fieldOfView.ToString());
        Logger.LogInfo("Right eye FOV: " + rightEyeCam.fieldOfView.ToString());
        // TODO: Get fov from headset and find a way to modify leftEyeCam without TCA reverting it
        rightEyeCam.fieldOfView = leftEyeCam.fieldOfView;
        Logger.LogInfo("After:");
        Logger.LogInfo("Left eye FOV: " + leftEyeCam.fieldOfView.ToString());
        Logger.LogInfo("Right eye FOV: " + rightEyeCam.fieldOfView.ToString());

        leftEyeCam.useOcclusionCulling = false;
        rightEyeCam.useOcclusionCulling = false;

        float num = 63f / (1000f * 2);
        leftEye.transform.Translate(Vector3.left * num);
        rightEye.transform.Translate(Vector3.right * num);
        leftEyeCam.rect = new Rect(0.0f, 0.0f, 0.5f, 1.0f);
        rightEyeCam.rect = new Rect(0.5f, 0.0f, 0.5f, 1.0f);

        return new CameraRig
        {
            leftEye = leftEye,
            rightEye = rightEye,
            leftEyeCam = leftEyeCam,
            rightEyeCam = rightEyeCam,
            isCockpitCam = isCockpitCam,
        };
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Logger.LogInfo("Scene load detected, searching for Camera Rigs to VRify");
        var cameraRigs = Resources
            .FindObjectsOfTypeAll<GameObject>()
            .Where<GameObject>(obj => obj.name == "CameraRoot")
            .ToArray();

        rigs.Clear();
        if (rigs.Capacity < cameraRigs.Length)
            rigs.Capacity = cameraRigs.Length;
        foreach (var rig in cameraRigs)
        {
            // Get the left eye of the current camera rig
            var oldCam = NavigateGameObjectChildren(rig.transform,
                new string[3] { "TrackIRChannel", "ShakeChannel", "MainCamera" });
            if (oldCam == null)
            {
                Logger.LogWarning("Skipping camera rig as it doesn't have the expected children.");
                Logger.LogWarning(rig);
                continue;
            }

            // Add the main camera of the current rig
            var mainRig = CreateVRRig(oldCam, false);
            rigs.Add(mainRig);

            // Add the CockpitInternalCam if it exists (not on all rigs)
            var oldCockpitCam = NavigateGameObjectChildren(rig.transform,
                new string[3] { "TrackIRChannel", "ShakeChannel", "CockpitInternalCam" });
            if (oldCockpitCam != null)
            {
                var cockpitRig = CreateVRRig(oldCockpitCam, true);
                rigs.Add(cockpitRig);

                // Add the cockpit cameras over the main cameras
                var leftEyeExtraProps = mainRig.leftEye.GetComponent<UniversalAdditionalCameraData>();
                var rightEyeExtraProps = mainRig.rightEye.GetComponent<UniversalAdditionalCameraData>();
                leftEyeExtraProps.cameraStack.Clear();
                rightEyeExtraProps.cameraStack.Clear();
                leftEyeExtraProps.cameraStack.Add(cockpitRig.leftEyeCam);
                rightEyeExtraProps.cameraStack.Add(cockpitRig.rightEyeCam);
            }
        }
        Logger.LogInfo($"Successfully VRified {rigs.Count} camera rigs in the scene.");

        StartCoroutine(RemoveAnnoyingPlaneParts());
    }

    private IEnumerator RemoveAnnoyingPlaneParts()
    {
        // Wait for the plane to be spawned
        yield return new WaitForSecondsRealtime(1f);

        // Hide the outside of the plane, it appears weirdly in VR
        // TODO: Make sure this doesn't disable enemies too.
        var planeModels = Resources
            .FindObjectsOfTypeAll<GameObject>()
            .Where(go => go.name.StartsWith("Kestrel 1 ("));
        foreach (var model in planeModels)
        {
            var a = NavigateGameObjectChildren(model.transform, new string[] { "Model", "Canopy" });
            if (a == null) continue;
            var b = NavigateGameObjectChildren(model.transform, new string[] { "Model", "Fuselage" });
            if (b == null) continue;

            a.SetActive(false);
            b.SetActive(false);
        }
        yield break;
    }

    private Transform? GetChildWithName(Transform transform, string name)
    {
        for (int index = 0; index < transform.childCount; ++index)
        {
            Transform child = transform.GetChild(index);
            if (child.name == name)
                return child;
        }
        return null;
    }

    private GameObject? NavigateGameObjectChildren(Transform? transform, IEnumerable<string> childNames)
    {
        Transform? currentTransform = transform;
        foreach (var childName in childNames)
        {
            if (currentTransform == null)
                return null;
            currentTransform = GetChildWithName(currentTransform, childName);
        }
        return currentTransform?.gameObject;
    }

    private void LogList(ICollection<object> collection)
    {
        Logger.LogInfo($"List length: {collection.Count.ToString()}");
        int i = 0;
        foreach (var obj in collection)
            Logger.LogInfo($"{i++}: {obj}");
    }
}
