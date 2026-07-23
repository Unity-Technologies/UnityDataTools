// This file defines the structure of the BuildReportSummary.json file written into each build
// history folder (default location Library/BuildHistory) by Player and content directory builds
// (available starting in Unity 6.6).
//
// It is a convenience type for reading that data outside of the Unity Editor. The fields mirror
// the Editor's BuildReportSummary; see
// https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Build.BuildReportSummary.html
// for their documentation.

using System;

namespace UnityDataTools.Models
{
    public class BuildReportSummary
    {
        public int Version;
        public string[] BuildContentOptions;
        public string BuildManifestHash;
        public string BuildName;
        public string[] BuildOptions;
        public string BuildProfilePath;
        public int BuildResult;
        public string BuildResultName;
        public string BuildSessionGUID;
        public DateTime BuildStartedAt;
        public int BuildType;
        public string BuildTypeName;
        public string OutputPath;
        public int Platform;
        public string PlatformName;
        public int Subtarget;
        public string SubtargetName;
        public int TotalErrors;
        public long TotalSizeBytes;
        public long TotalTimeMs;
        public int TotalWarnings;
    }
}
