# Reference BuildReports

These example files are used for testing UnityDataTool support for BuildReports.  They are in the Unity binary format, copied from `Library/LastBuild.buildReport` after performing a build in Unity.

They were output from the TestProject in the [BuildReportInspector](https://github.com/Unity-Technologies/BuildReportInspector/tree/master/TestProject).

* **AssetBundle.buildreport** - Example report from an AssetBundle build (BuildPipeline.BuildAssetBundles).
* **Player.buildreport** - BuildReport for a Windows Player build with detailed build reporting (generated with Unity 6000.0.65f1)
* **happyHarvest.buildreport** - BuildReport for a Windows Player build of the [Happy Harvest](https://assetstore.unity.com/packages/essentials/tutorial-projects/happy-harvest-2d-sample-project-259218) 2D sample project (generated with Unity 6000.6). A larger report with a ContentSummary object and a diverse range of content; the example output in `Documentation/analyze-examples-buildreport.md` is based on it.

