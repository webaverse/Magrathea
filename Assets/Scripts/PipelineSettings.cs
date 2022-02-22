using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using System.IO;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using System.Linq;



namespace Magrathea
{
    public enum LightmapMode
    {
        IGNORE,
        BAKE_COMBINED,
        BAKE_SEPARATE
    }

    public enum MeshExportMode
    {
        DEFAULT,
        COMBINE,
        NO_MESHES
    }

    [InitializeOnLoad]
    public static class PipelineSettings
    {
        [System.Serializable]
        struct Data
        {
            public string GLTFName;
            public string ProjectFolder;
            public bool ExportColliders;
            public bool ExportSkybox;
            public bool ExportEnvmap;
            public MeshExportMode meshMode;
            public bool preserveLightmapping;
            public LightmapMode lightmapMode;
            public int CombinedTextureResolution;
            public void Apply()
            {
                PipelineSettings.GLTFName = GLTFName;
                PipelineSettings.ProjectFolder = this.ProjectFolder;
                
                PipelineSettings.ExportColliders = this.ExportColliders;
                PipelineSettings.ExportSkybox = this.ExportSkybox;
                PipelineSettings.ExportEnvmap = ExportEnvmap;

                PipelineSettings.lightmapMode = this.lightmapMode;
                PipelineSettings.preserveLightmapping = this.preserveLightmapping;
                PipelineSettings.meshMode = meshMode;

                PipelineSettings.CombinedTextureResolution = this.CombinedTextureResolution;
            }
            public void Set()
            {
                GLTFName = PipelineSettings.GLTFName;
                ProjectFolder = PipelineSettings.ProjectFolder;
               
                ExportColliders = PipelineSettings.ExportColliders;
                ExportSkybox = PipelineSettings.ExportSkybox;
                ExportEnvmap = PipelineSettings.ExportEnvmap;
                
                lightmapMode = PipelineSettings.lightmapMode;
                meshMode = PipelineSettings.meshMode;

                preserveLightmapping = PipelineSettings.preserveLightmapping;
                CombinedTextureResolution = PipelineSettings.CombinedTextureResolution;
            }
        }

        public static readonly string ConversionFolder = Application.dataPath + "/Outputs/";
        public static readonly string configFile = Application.dataPath + "/settings.conf";
        public static readonly string PipelineAssetsFolder = Application.dataPath + "/Magrathea/PipelineAssets/";
        public static readonly string PipelinePersistentFolder = Application.dataPath + "/Magrathea/PersistentAssets/";
        public static string GLTFName;
        public static string ProjectFolder;

        public static string ProjectName => 
            Regex.Match(ProjectFolder, @"(?<=[\\\/])[\w-_]+(?=[\\\/]*$)").Value;

        public static string LocalPath => "https://localhost:8642/" + 
            Regex.Match(ProjectFolder, @"[\w-]+[\\/]+[\w-]+[\\/]*$").Value;

        public static string ScriptsFolder => ProjectFolder + "/scripts/";

        public static bool ExportColliders;
        public static bool ExportSkybox;
        public static bool ExportEnvmap;

        public static MeshExportMode meshMode;

        public static bool preserveLightmapping;

        public static LightmapMode lightmapMode = LightmapMode.BAKE_COMBINED;

        public static int CombinedTextureResolution = 4096;
        
        static PipelineSettings()
        {
            ReadSettingsFromConfig();
        }

        public static void ReadSettingsFromConfig()
        {
            var config = new FileInfo(configFile);
            if (!config.Exists)
            {
                return;
            }
            var data = JsonConvert.DeserializeObject<Data>
            (
                File.ReadAllText(configFile)
            );
            data.Apply();
        }

        public static void SaveSettings()
        {
            var data = new Data();
            data.Set();
            File.WriteAllText(configFile, JsonConvert.SerializeObject(data, Formatting.Indented));
        }
    }

    
}

