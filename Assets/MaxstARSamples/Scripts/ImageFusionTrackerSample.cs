/*==============================================================================
Copyright 2017 Maxst, Inc. All Rights Reserved.
==============================================================================*/

using UnityEngine;
using System.Collections.Generic;

using maxstAR;

public class ImageFusionTrackerSample : ARBehaviour
{
	private Dictionary<string, ImageTrackableBehaviour> imageTrackablesMap =
		new Dictionary<string, ImageTrackableBehaviour>();

    private CameraBackgroundBehaviour cameraBackgroundBehaviour = null;
    public GameObject guideView;

    void Awake()
    {
		Init();

        AndroidRuntimePermissions.Permission[] result = AndroidRuntimePermissions.RequestPermissions("android.permission.WRITE_EXTERNAL_STORAGE", "android.permission.CAMERA");
        if (result[0] == AndroidRuntimePermissions.Permission.Granted && result[1] == AndroidRuntimePermissions.Permission.Granted)
            Debug.Log("We have all the permissions!");
        else
            Debug.Log("Some permission(s) are not granted...");


        cameraBackgroundBehaviour = FindObjectOfType<CameraBackgroundBehaviour>();
        if (cameraBackgroundBehaviour == null)
        {
            Debug.LogError("Can't find CameraBackgroundBehaviour.");
            return;
        }
    }

	void Start()
	{

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

		imageTrackablesMap.Clear();
		ImageTrackableBehaviour[] imageTrackables = FindObjectsOfType<ImageTrackableBehaviour>();
		foreach (var trackable in imageTrackables)
		{
			imageTrackablesMap.Add(trackable.TrackableName, trackable);
			Debug.Log("Trackable add: " + trackable.TrackableName);
		}

        if(TrackerManager.GetInstance().IsFusionSupported())
        {
            CameraDevice.GetInstance().SetARCoreTexture();
			CameraDevice.GetInstance().SetFusionEnable();
			CameraDevice.GetInstance().Start();
			TrackerManager.GetInstance().StartTracker(TrackerManager.TRACKER_TYPE_IMAGE_FUSION);
            AddTrackerData();
        }
        else
        {
            TrackerManager.GetInstance().RequestARCoreApk();
        }
    }

	private void AddTrackerData()
	{
		foreach (var trackable in imageTrackablesMap)
		{
			if (trackable.Value.TrackerDataFileName.Length == 0)
			{
				continue;
			}

			if (trackable.Value.StorageType == StorageType.AbsolutePath)
			{
				TrackerManager.GetInstance().AddTrackerData(trackable.Value.TrackerDataFileName);
				TrackerManager.GetInstance().LoadTrackerData();
			}
			else if (trackable.Value.StorageType == StorageType.StreamingAssets)
			{
				if (Application.platform == RuntimePlatform.Android)
				{
					StartCoroutine(MaxstARUtil.ExtractAssets(trackable.Value.TrackerDataFileName, (filePah) =>
					{
						TrackerManager.GetInstance().AddTrackerData(filePah, false);
						TrackerManager.GetInstance().LoadTrackerData();
					}));
				}
				else
				{
					TrackerManager.GetInstance().AddTrackerData(Application.streamingAssetsPath + "/" + trackable.Value.TrackerDataFileName);
					TrackerManager.GetInstance().LoadTrackerData();
				}
			}
		}
	}

	private void DisableAllTrackables()
	{
		foreach (var trackable in imageTrackablesMap)
		{
			trackable.Value.OnTrackFail();
		}
	}

	void Update()
	{
		DisableAllTrackables();

		TrackingState state = TrackerManager.GetInstance().UpdateTrackingState();

        if (state == null)
        {
            return;
        }

        cameraBackgroundBehaviour.UpdateCameraBackgroundImage(state);

        int fusionState = TrackerManager.GetInstance().GetFusionTrackingState();
        if(fusionState == -1)
        {
            guideView.SetActive(true);
            return;
        } else
        {
            guideView.SetActive(false);
        }

		TrackingResult trackingResult = state.GetTrackingResult();

		for(int i = 0; i < trackingResult.GetCount(); i++)
		{
			Trackable trackable = trackingResult.GetTrackable(i);
			imageTrackablesMap[trackable.GetName()].OnTrackSuccess(
				trackable.GetId(), trackable.GetName(), trackable.GetPose());
		}
	}

	void OnApplicationPause(bool pause)
	{
		if (pause)
		{
			CameraDevice.GetInstance().Stop();
			TrackerManager.GetInstance().StopTracker();
		}
		else
		{
            if (TrackerManager.GetInstance().IsFusionSupported())
            {
                CameraDevice.GetInstance().SetARCoreTexture();
				CameraDevice.GetInstance().SetFusionEnable();
				CameraDevice.GetInstance().Start();
				TrackerManager.GetInstance().StartTracker(TrackerManager.TRACKER_TYPE_IMAGE_FUSION);
                AddTrackerData();
            }
			else
            {
                TrackerManager.GetInstance().RequestARCoreApk();
            }
        }
	}

	void OnDestroy()
	{
		imageTrackablesMap.Clear();
		CameraDevice.GetInstance().Stop();
		TrackerManager.GetInstance().SetTrackingOption(TrackerManager.TrackingOption.NORMAL_TRACKING);
		TrackerManager.GetInstance().StopTracker();
		TrackerManager.GetInstance().DestroyTracker();
	}
}