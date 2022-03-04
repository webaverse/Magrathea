using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.EditorCoroutines.Editor;
using UnityEngine;
using SeinJS;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Magrathea.CustomNode;
using System.Linq;
using System;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

namespace Magrathea
{
    public class ExportScene : EditorWindow
    {
        public enum State
        {
            INITIAL,
            PRE_EXPORT,
            EXPORTING,
            POST_EXPORT,
            RESTORING,
            ERROR
        }

        State state;
        bool doDebug;
        string defaultMatPath = @"Assets/Magrathea/Materials/Default.mat";

        [MenuItem("Magrathea/Export Scene")]
        static void Init()
        {
            ExportScene window = (ExportScene)EditorWindow.GetWindow(typeof(ExportScene));
            window.state = State.INITIAL;
            window.Show();
        }

        Exporter exporter;

        public string ConversionPath => Path.Combine(PipelineSettings.ConversionFolder, PipelineSettings.GLTFName);


        public string ExportPath
        {
            get
            {
                string exportFolder = PipelineSettings.ProjectFolder;

                return exportFolder;
            }
        }

        private void OnFocus()
        {
            if (exporter == null)
            {
                exporter = new Exporter();
            }

            Config.Load();
            if (!Utils.inited)
            {
                Utils.Init();
            }

            ExtensionManager.Init();
        }

        private void OnGUI()
        {
            #region Initial Menu
            switch (state)
            {

                case State.INITIAL:
                    Scene scene = SceneManager.GetActiveScene();

                    PipelineSettings.GLTFName = scene.name;

                    PipelineSettings.ProjectFolder = "/../Outputs";
                    GUILayout.Space(8);
                    GUILayout.Label("Export Components:");
                    PipelineSettings.ExportColliders = EditorGUILayout.Toggle("Colliders", PipelineSettings.ExportColliders);
                    PipelineSettings.ExportSkybox = EditorGUILayout.Toggle("Skybox", PipelineSettings.ExportSkybox);
                    PipelineSettings.ExportEnvmap = EditorGUILayout.Toggle("Envmap", PipelineSettings.ExportEnvmap);
                    GUILayout.Space(8);
                    PipelineSettings.meshMode = (MeshExportMode)EditorGUILayout.EnumPopup("Mesh Export Options", PipelineSettings.meshMode);
                    GUILayout.Space(8);
                    PipelineSettings.lightmapMode = (LightmapMode)EditorGUILayout.EnumPopup("Lightmap Mode", PipelineSettings.lightmapMode);

                    // GUILayout.Space(16);
                    // if (GUILayout.Button("Save Settings as Default"))
                    // {
                    //     PipelineSettings.SaveSettings();
                    // }
                    // GUILayout.Space(16);
                    // if (GUILayout.Button("Serialize Assets"))
                    // {
                    //     SerializeMaterials(true);
                    //     CreateUVBakedMeshes(true);
                    // }
                    // GUILayout.Space(8);
                    PipelineSettings.preserveLightmapping = EditorGUILayout.ToggleLeft("Preserve Lightmapping", PipelineSettings.preserveLightmapping);
                    // if(GUILayout.Button("Do MeshBake"))
                    // {
                    //     CombineMeshes(true);
                    // }
                    // GUILayout.Space(8);
                    if (GUILayout.Button("Format LODGroups"))
                    {
                        LODFormatter.FormatLODs();
                    }
                    // GUILayout.Space(8);
                    // if (GUILayout.Button("Deserialize Assets"))
                    // {
                    //     RestoreGLLinks();
                    //     DeserializeMaterials();
                    // }
                    // GUILayout.Space(8);
                    // if (GUILayout.Button("Undo MeshBake"))
                    // {
                    //     CleanupMeshCombine();
                    // }
                    GUILayout.Space(16);

                    if (PipelineSettings.ProjectFolder != null)
                    {
                        doDebug = EditorGUILayout.Toggle("Debug Execution", doDebug);

                        if (GUILayout.Button("Export"))
                        {
                            state = State.PRE_EXPORT;
                            Export(false);
                        }
                    }
                    break;


                #endregion

                #region Debugging Stepper
                case State.PRE_EXPORT:
                    if (GUILayout.Button("Continue"))
                    {
                        state++;
                    }
                    break;

                case State.POST_EXPORT:
                    if (GUILayout.Button("Continue"))
                    {
                        state++;
                    }
                    break;
                #endregion
                default:
                    GUILayout.Label("Exporting...");
                    break;
            }
        }

        #region UTILITY FUNCTIONS

        Dictionary<Material, Material> matLinks;
        Dictionary<string, Material> matRegistry;
        private Material BackupMaterial(Material material, Renderer _renderer, bool savePersistent)
        {
            if (matRegistry == null) matRegistry = new Dictionary<string, Material>();

            string registryKey = string.Format("{0}_{1}", material.name, _renderer ? _renderer.lightmapIndex : -2);

            if (matRegistry.ContainsKey(registryKey))
                return matRegistry[registryKey];

            string origPath = AssetDatabase.GetAssetPath(material);
            if (origPath == null || Regex.IsMatch(origPath, @".*\.glb"))
            {
                UnityEngine.Debug.Log("Creating link Material to glb");
                Material dupe = new Material(material);
                string dupeRoot = savePersistent ? PipelineSettings.PipelinePersistentFolder : PipelineSettings.PipelineAssetsFolder;
                string dupePath = dupeRoot + material.name + "_" + DateTime.Now.Ticks + ".mat";
                string dupeDir = Regex.Match(dupePath, @"(.*[\\\/])[\w\.\d\-]+").Value;
                dupePath = dupePath.Replace(Application.dataPath, "Assets");
                if (!Directory.Exists(dupeDir))
                {
                    Directory.CreateDirectory(dupeDir);
                }
                AssetDatabase.CreateAsset(dupe, dupePath);

                if (matLinks == null)
                {
                    matLinks = new Dictionary<Material, Material>();
                }
                matLinks[dupe] = material;
                UnityEngine.Debug.Log("material " + dupe.name + " linked to glb material " + material.name);
                matRegistry[registryKey] = dupe;
                Material check = material;
                foreach (var renderer in FindObjectsOfType<Renderer>())
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select((sharedMat) =>
                        sharedMat &&
                        sharedMat.name == check.name ? dupe : sharedMat
                    ).ToArray();
                }

                return dupe;
            }
            return material;
        }
        private Tuple<Material, string, string>[] BackupTextures(ref Material mat)
        {
            //Material mat = _mat;
            /*
            string[] mapTests = new string[]
            {
                "_MainTex",
                "_BumpMap",
                "_EmissionMap",
                "_MetallicGlossMap",
                "_OcclusionMap",
                "_baseColorMap"
            };
            */
            var maps = mat.GetTexturePropertyNames();
            var matTexes = new List<Texture>();
            for (int i = 0; i < maps.Length; i++)
            {
                matTexes.Add(mat.GetTexture(maps[i]));
            }
            var textures = maps
                .Select((map, i) =>
                {
                    Texture tex = matTexes[i];
                    if (tex == null) return null;
                    return new Tuple<string, Texture>(map, tex);
                })
                .Where((x) => x != null && x.Item2.GetType() == typeof(Texture2D))
                .Select((x) => new Tuple<string, Texture2D>(x.Item1, (Texture2D)x.Item2))
                .ToArray();
            var texPaths = new List<Tuple<Material, string, string>>();
            foreach (var texture in textures)
            {
                var tex = texture.Item2;
                string texPath = AssetDatabase.GetAssetPath(tex);
                if (texPath == null || texPath == "" || Regex.IsMatch(texPath, @".*\.glb"))
                {
                    string nuPath;
                    Texture2D nuTex = GenerateAsset(tex, out nuPath);
                    texPaths.Add(new Tuple<Material, string, string>(mat, texture.Item1, nuPath));
                }
            }
            return texPaths.ToArray();
        }

        Dictionary<Texture2D, Texture2D> texLinks;
        private Texture2D GenerateAsset(Texture2D tex, out string path)
        {
            Texture2D nuTex = new Texture2D(tex.width, tex.height, tex.format, tex.mipmapCount, false);
            nuTex.name = tex.name + "_" + System.DateTime.Now.Ticks;
            Graphics.CopyTexture(tex, nuTex);
            nuTex.Apply();
            if (!Directory.Exists(PipelineSettings.PipelineAssetsFolder))
            {
                Directory.CreateDirectory(PipelineSettings.PipelineAssetsFolder);
            }
            string nuPath = PipelineSettings.PipelineAssetsFolder.Replace(Application.dataPath, "Assets") + nuTex.name + ".png";
            string localPath = nuPath.Replace(Application.dataPath, "Assets");
            File.WriteAllBytes(nuPath, nuTex.EncodeToPNG());
            //AssetDatabase.ImportAsset(localPath);

            UnityEngine.Debug.Log("Generated texture " + nuTex + " from " + tex);
            if (texLinks == null)
                texLinks = new Dictionary<Texture2D, Texture2D>();
            texLinks[nuTex] = tex;
            path = nuPath;
            return nuTex;
        }
        #endregion

        #region LODS
        Dictionary<Transform, string> lodRegistry;
        private void FormatForExportingLODs()
        {
            lodRegistry = new Dictionary<Transform, string>();
            LODGroup[] lodGroups = GameObject.FindObjectsOfType<LODGroup>();
            foreach (var lodGroup in lodGroups)
            {
                Transform tr = lodGroup.transform;
                lodRegistry.Add(tr, tr.name);
                tr.name += "_LODGroup";
            }
        }

        private void CleanupExportingLODs()
        {
            if (lodRegistry != null)
            {
                foreach (var kv in lodRegistry)
                {
                    kv.Key.name = kv.Value;
                }
                lodRegistry = null;
            }
        }
        #endregion

        #region LIGHTS
        Light[] bakeLights;
        public void StageLights()
        {
            bakeLights = FindObjectsOfType<Light>().Where((light) => light.gameObject.activeInHierarchy && light.lightmapBakeType == LightmapBakeType.Baked).ToArray();

            foreach (var light in bakeLights)
            {
                light.gameObject.SetActive(false);
            }
        }

        public void CleanupLights()
        {
            foreach (var light in bakeLights)
            {
                light.gameObject.SetActive(true);
            }
            bakeLights = null;
        }
        #endregion

        #region SKYBOX
        private void FormatForExportingSkybox()
        {
            if (PipelineSettings.ExportSkybox)
            {
                var skyMat = RenderSettings.skybox;
                string[] fNames = new string[]
                    {
                    "negx",
                    "posx",
                    "posy",
                    "negy",

                    "posz",
                    "negz"
                    };
                string nuPath = Path.Combine(PipelineSettings.ProjectFolder, "cubemap");
                if (!Directory.Exists(nuPath))
                {
                    Directory.CreateDirectory(nuPath);
                }
                SkyBox.Mode outMode = SkyBox.Mode.CUBEMAP;
                if (skyMat.shader.name == "Skybox/6 Sided")
                {
                    string[] texNames = new[]
                    {
                        "_FrontTex",
                        "_BackTex",
                        "_UpTex",
                        "_DownTex",
                        "_LeftTex",
                        "_RightTex",
                    };
                    string[] faceTexes = texNames.Select((x, i) =>
                    {
                        string facePath = string.Format("{0}/{1}.jpg", nuPath, fNames[i]);
                        File.WriteAllBytes(facePath, ((Texture2D)skyMat.GetTexture(x)).EncodeToJPG());
                        return x;
                    }).ToArray();
                }
                else if (skyMat.shader.name.Contains("Skybox/Panoramic"))
                {
                    var hdri = skyMat.GetTexture("_MainTex") as Texture2D;
                    outMode = SkyBox.Mode.EQUIRECTANGULAR;
                    string outPath = nuPath + "/rect.jpg";
                    File.WriteAllBytes(outPath, hdri.EncodeToJPG(100));
                }
                else
                {
                    var cubemap = skyMat.GetTexture("_Tex") as Cubemap;
                    string srcPath = AssetDatabase.GetAssetPath(cubemap);
                    string srcName = Regex.Match(srcPath, @"(?<=.*/)\w*(?=\.hdr)").Value;

                    var cubemapDir = new DirectoryInfo(nuPath);
                    if (!cubemapDir.Exists)
                    {
                        cubemapDir.Create();
                    }

                    CubemapFace[] faces = Enumerable.Range(0, 6).Select((i) => (CubemapFace)i).ToArray();

                    Texture2D[] faceTexes = faces.Select((x, i) =>
                    {
                        Texture2D result = new Texture2D(cubemap.width, cubemap.height);// cubemap.format, false);
                        var pix = cubemap.GetPixels(x);
                        System.Array.Reverse(pix);
                        result.SetPixels(pix);
                        result.Apply();

                        string facePath = string.Format("{0}/{1}.jpg", nuPath, fNames[i]);
                        File.WriteAllBytes(facePath, result.EncodeToJPG());
                        return result;
                    }).ToArray();
                }


                GameObject skyboxGO = new GameObject("__skybox__");
                skyboxGO.AddComponent<SkyBox>().mode = outMode;
            }
        }

        private void CleanupExportingSkybox()
        {
            var skyboxes = FindObjectsOfType<SkyBox>();
            for (int i = 0; i < skyboxes.Length; i++)
            {
                DestroyImmediate(skyboxes[i].gameObject);
            }
        }
        #endregion

        #region ENVMAP
        private void FormatForExportingEnvmap()
        {
            GameObject envmapGO = new GameObject("__envmap__");
            envmapGO.AddComponent<Envmap>();
        }

        private void CleanupExportEnvmap()
        {
            var envmaps = FindObjectsOfType<Envmap>();
            for (int i = 0; i < envmaps.Length; i++)
            {
                DestroyImmediate(envmaps[i].gameObject);
            }
        }
        #endregion

        #region MESH BAKING
        class MatRend
        {
            public Material mat;
            public Renderer rend;
            public MatRend(Material _mat, Renderer _rend)
            {
                mat = _mat;
                rend = _rend;
            }
        }
        private void SerializeMaterials(bool savePersistent = false)
        {
            matRegistry = new Dictionary<string, Material>();
            matLinks = new Dictionary<Material, Material>();
            texLinks = new Dictionary<Texture2D, Texture2D>();

            var mats = FindObjectsOfType<Renderer>()
                .Where((x) => x.gameObject.activeInHierarchy && x.enabled)
                .SelectMany((rend) => rend.sharedMaterials.Select((mat) => new MatRend(mat, rend)))
                .Where((x) => x != null && x.mat != null && x.rend != null).ToArray();
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i].mat;
                var rend = mats[i].rend;
                mats[i].mat = BackupMaterial(mat, rend, savePersistent);
            }
            AssetDatabase.Refresh();
            var remaps = new List<Tuple<Material, string, string>>();
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i].mat;
                remaps.AddRange(BackupTextures(ref mat));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            for (int i = 0; i < remaps.Count; i++)
            {
                var remap = remaps[i];
                var nuMat = remap.Item1;
                var nuTex = AssetDatabase.LoadAssetAtPath<Texture2D>(remap.Item3);
                nuMat.SetTexture(remap.Item2, nuTex);
                UnityEngine.Debug.Log("material " + nuMat + " path of " + AssetDatabase.GetAssetPath(nuMat));
                UnityEngine.Debug.Log("setting material " + nuMat + " texture " + remap.Item2 + " to " + nuTex + ", path of " + AssetDatabase.GetAssetPath(nuTex));

            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("processed materials");
            var updates = mats.GroupBy((mat) => mat.rend).Select((rGroup) =>
            new Dictionary<Renderer, Material[]>
            (
                new KeyValuePair<Renderer, Material[]>[]
                {
                    new KeyValuePair<Renderer, Material[]>
                    (
                        rGroup.Key,
                        rGroup.Select((r) => r.mat).ToArray()
                    )
                }
            )).Aggregate((rGroup1, rGroup2) =>
            {
                if (rGroup2 == null) return rGroup1;
                if (rGroup1 == null) return rGroup2;
                foreach (var key in rGroup2.Keys)
                {
                    rGroup1[key] = rGroup2[key];
                }
                return rGroup2;
            });
            foreach (var update in updates)
            {
                update.Key.sharedMaterials = update.Value;
            }
        }

        private void DeserializeMaterials()
        {
            var renderers = FindObjectsOfType<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select
                (
                    (mat) =>
                    {
                        if (matLinks.ContainsKey(mat))
                        {
                            mat = matLinks[mat];
                        }
                        //DeserializeTextures(ref mat);
                        return mat;
                    }
                ).ToArray();
            }
            matRegistry = null;
            matLinks = null;
            texLinks = null;
        }

        private void CreateBakedMeshes(bool savePersistent)
        {
            if (PipelineSettings.meshMode == MeshExportMode.DEFAULT ||
               PipelineSettings.meshMode == MeshExportMode.COMBINE)
            {
                CreateUVBakedMeshes(savePersistent);
                if (PipelineSettings.meshMode == MeshExportMode.COMBINE)
                {
                    CombineMeshes();
                }
            }
        }

        Dictionary<UnityEngine.Mesh, UnityEngine.Mesh> glLinks;
        private void CreateUVBakedMeshes(bool savePersistent = false)
        {
            glLinks = new Dictionary<Mesh, Mesh>();
            var renderers = FindObjectsOfType<Renderer>();
            foreach (var renderer in renderers)
            {
                bool isSkinned = renderer.GetType() == typeof(SkinnedMeshRenderer);
                Mesh mesh = null;
                if (isSkinned)
                {
                    mesh = ((SkinnedMeshRenderer)renderer).sharedMesh;
                }
                else
                {
                    var filt = renderer.GetComponent<MeshFilter>();
                    if (filt != null)
                        mesh = filt.sharedMesh;
                }
                if (mesh == null) continue;

                bool hasLightmap = renderer.lightmapIndex >= 0 && LightmapSettings.lightmaps.Length > renderer.lightmapIndex;

                if ((hasLightmap && PipelineSettings.lightmapMode == LightmapMode.BAKE_SEPARATE) ||
                     Regex.IsMatch(AssetDatabase.GetAssetPath(mesh), @".*\.glb"))
                {

                    string assetFolder = savePersistent ? PipelineSettings.PipelinePersistentFolder : PipelineSettings.PipelineAssetsFolder;

                    if (!Directory.Exists(assetFolder))
                    {
                        Directory.CreateDirectory(assetFolder);
                    }
                    string nuMeshPath = assetFolder.Replace(Application.dataPath, "Assets") + renderer.transform.name + "_" + System.DateTime.Now.Ticks + ".asset";
                    UnityEngine.Mesh nuMesh = UnityEngine.Object.Instantiate(mesh);

                    AssetDatabase.CreateAsset(nuMesh, nuMeshPath);

                    if (hasLightmap)
                    {
                        var off = renderer.lightmapScaleOffset;
                        var nuUv2s = nuMesh.uv2.Select((uv2) => uv2 * new Vector2(off.x, off.y) + new Vector2(off.z, off.w)).ToArray();
                        nuMesh.uv2 = nuUv2s;
                        nuMesh.UploadMeshData(false);
                    }
                    if (!isSkinned)
                        renderer.GetComponent<MeshFilter>().sharedMesh = nuMesh;
                    else
                    {
                        ((SkinnedMeshRenderer)renderer).sharedMesh = nuMesh;
                    }
                    glLinks[nuMesh] = mesh;
                    mesh = nuMesh;
                }
            }
            AssetDatabase.Refresh();
        }

        MeshBakeResult[] bakeResults;
        private void CombineMeshes(bool savePersistent = false)
        {
#if UNITY_EDITOR && USE_MESH_BAKER

            var stagers = FindObjectsOfType<MeshBake>();
            bakeResults = stagers.Select((baker) => baker.Bake(savePersistent)).Where((x) => x != null).ToArray();
            foreach(var result in bakeResults)
            {
                foreach(var original in result.originals)
                {
                    original.GetComponent<MeshRenderer>().enabled = false;
                }
            }
            AssetDatabase.Refresh();
#endif
        }

        private void CleanupMeshCombine()
        {
            if (bakeResults != null)
            {
                foreach (var result in bakeResults)
                {
                    foreach (var original in result.originals)
                    {
                        if (original && original.GetComponent<MeshRenderer>())
                            original.GetComponent<MeshRenderer>().enabled = true;
                    }
                    foreach (var combined in result.combined)
                    {
                        if (combined != null)
                            DestroyImmediate(combined.gameObject);
                    }
                }

            }
            //MeshStager.ResetAll();
        }

        private void RestoreGLLinks()
        {
            if (glLinks != null)
            {
                MeshFilter[] filts = GameObject.FindObjectsOfType<MeshFilter>();
                foreach (var filt in filts)
                {
                    if (filt &&
                        filt.sharedMesh != null &&
                        glLinks.ContainsKey(filt.sharedMesh))
                    {
                        filt.sharedMesh = glLinks[filt.sharedMesh];
                    }
                }
            }
            glLinks = null;
        }
        #endregion

        #region COLLIDERS
        /// <summary>
        /// Formats the scene to correctly export colliders to match Magrathea colliders spec
        /// </summary>
        GameObject cRoot;
        private void FormatForExportingColliders()
        {
            cRoot = new GameObject("Colliders", typeof(ColliderParent));
            //Dictionary<Collider, Transform> parents = new Dictionary<Collider, Transform>();
            Material defaultMat = AssetDatabase.LoadMainAssetAtPath(defaultMatPath) as Material;
            Collider[] colliders = GameObject.FindObjectsOfType<Collider>().Where((col) => col.gameObject.activeInHierarchy).ToArray();
            foreach (var collider in colliders)
            {
                Transform xform = collider.transform;
                Vector3 position = xform.position;
                Quaternion rotation = xform.rotation;
                Vector3 scale = xform.lossyScale;
                //parents[collider] = xform;
                if (collider.GetType() == typeof(BoxCollider))
                {
                    var box = (BoxCollider)collider;
                    GameObject clone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    clone.name = xform.gameObject.name + "__COLLIDER__";
                    clone.transform.position = position;
                    clone.transform.rotation = rotation;
                    clone.transform.localScale = scale;



                    clone.transform.position += clone.transform.localToWorldMatrix.MultiplyVector(box.center);
                    Vector3 nuScale = clone.transform.localScale;
                    nuScale.x *= box.size.x;
                    nuScale.y *= box.size.y;
                    nuScale.z *= box.size.z;
                    clone.transform.localScale = nuScale;
                    MeshRenderer rend = clone.GetComponent<MeshRenderer>();
                    //rend.lightmapIndex = -1;
                    rend.material = defaultMat;
                    clone.transform.SetParent(cRoot.transform, true);
                }
                else
                {
                    GameObject clone = Instantiate(xform.gameObject, cRoot.transform, true);
                    clone.transform.position = position;
                    clone.transform.rotation = rotation;
                    clone.name += "__COLLIDER__";
                    MeshRenderer rend = clone.GetComponent<MeshRenderer>();
                    rend.enabled = true;
                    rend.lightmapIndex = -1;
                }




            }
        }
        private void CleanUpExportingColliders()
        {
            if (cRoot)
            {
                DestroyImmediate(cRoot);
            }
        }

        #endregion

        private void Export(bool savePersistent)
        {
            EditorCoroutineUtility.StartCoroutine(ExportSequence(savePersistent), this);
        }
        private IEnumerator ExportSequence(bool savePersistent)
        {

            DirectoryInfo directory = new DirectoryInfo(PipelineSettings.ConversionFolder);
            if (!directory.Exists)
            {
                Directory.CreateDirectory(PipelineSettings.ConversionFolder);
            }
            string exportFolder = Path.Combine(PipelineSettings.ProjectFolder, "assets");
            DirectoryInfo outDir = new DirectoryInfo(exportFolder);
            if (!outDir.Exists)
            {
                Directory.CreateDirectory(exportFolder);
            }

            var files = directory.GetFiles();
            var subDirectories = directory.GetDirectories();

            //delete files in pipeline folder to make way for new export
            foreach (var file in files)
            {
                file.Delete();
            }

            foreach (var subDir in subDirectories)
            {
                subDir.Delete(true);
            }

            //set exporter path
            ExporterSettings.Export.name = PipelineSettings.GLTFName;
            ExporterSettings.Export.folder = PipelineSettings.ConversionFolder;

            //before we start staging the scene for export, invoke the OnExport event to have all
            //active  nodes export files to the linked project

            RPComponent.InvokeExport();

            //TODO: move most of these export helper functions into the OnExport handlers of the
            //      customnode classes

            StageLights();

            FormatForExportingLODs();

            if (PipelineSettings.ExportColliders)
            {
                FormatForExportingColliders();
            }



            SerializeMaterials();

            CreateBakedMeshes(savePersistent);

            if (PipelineSettings.ExportSkybox)
            {
                FormatForExportingSkybox();
            }

            if (PipelineSettings.ExportEnvmap)
            {
                FormatForExportingEnvmap();
            }



            //convert materials to SeinPBR
            StandardToSeinPBR.AllToSeinPBR();
            if (doDebug)
            {
                while (state != State.EXPORTING) yield return null;
            }

            try
            {
                exporter.Export();
            }
            catch (System.NullReferenceException e)
            {
                UnityEngine.Debug.LogError("export error:" + e);
            }

            state = State.POST_EXPORT;

            if (doDebug)
            {
                while (state != State.RESTORING) yield return null;
            }


            //now execute the GLTF conversion script in the Pipeline folder
            UnityEngine.Debug.Log("System info is " + SystemInfo.operatingSystem.ToLower());
            UnityEngine.Debug.Log("ExportPath " + ExportPath);
            var converter = new GLTFToGLBConverter();
            converter.ConvertToGLB(PipelineSettings.ConversionFolder + PipelineSettings.GLTFName);
            var GLBName = PipelineSettings.ConversionFolder + PipelineSettings.GLTFName + ".glb";

            SendToWebaverse(GLBName);
            CreateMetaverseFile(GLBName);
            CreateSceneFile(GLBName);
            // File.Delete(Path.Combine(PipelineSettings.ConversionFolder, PipelineSettings.GLTFName + ".glb"));
            // File.Delete(Path.Combine(PipelineSettings.ConversionFolder, PipelineSettings.GLTFName + ".gltf"));
            // File.Delete(Path.Combine(PipelineSettings.ConversionFolder, PipelineSettings.GLTFName + ".bin"));


            if (PipelineSettings.ExportColliders)
            {
                CleanUpExportingColliders();
            }


            //restore materials
            StandardToSeinPBR.RestoreMaterials();
            if (PipelineSettings.meshMode == MeshExportMode.COMBINE)
            {
                CleanupMeshCombine();
            }
            RestoreGLLinks();
            DeserializeMaterials();

            CleanupExportingLODs();

            if (PipelineSettings.ExportSkybox)
            {
                CleanupExportingSkybox();
            }

            if (PipelineSettings.ExportEnvmap)
            {
                CleanupExportEnvmap();
            }

            CleanupLights();


            state = State.INITIAL;
        }

        private void SendToWebaverse(string GLBName)
        {
            // if project folder directory doesn't exist, create it
            DirectoryInfo projectDir = new DirectoryInfo(PipelineSettings.ConversionFolder);
            if (!projectDir.Exists)
                Directory.CreateDirectory(PipelineSettings.ConversionFolder);

            // if project folder directory doesn't exist, create it
            projectDir = new DirectoryInfo(PipelineSettings.ConversionFolder + "/Webaverse");
            if (!projectDir.Exists)
                Directory.CreateDirectory(PipelineSettings.ConversionFolder + "/Webaverse");

            projectDir = new DirectoryInfo(PipelineSettings.ConversionFolder + "/Webaverse/" + PipelineSettings.GLTFName);
            if (!projectDir.Exists)
                Directory.CreateDirectory(PipelineSettings.ConversionFolder + "/Webaverse/" + PipelineSettings.GLTFName);

            // copy the glb to the project folder
            File.Copy(GLBName, Path.Combine(PipelineSettings.ConversionFolder + "/./Webaverse", PipelineSettings.GLTFName, PipelineSettings.GLTFName + ".glb"), true);
        }

        private void CreateMetaverseFile(string GLBName)
        {
            String metaverseFile = "{\"name\": \"" + PipelineSettings.GLTFName + "\", \"start_url\": \"" + PipelineSettings.GLTFName + ".glb\" }";
            // write the metaverse file to the project folder
            File.WriteAllText(Path.Combine(PipelineSettings.ConversionFolder + "/./Webaverse", PipelineSettings.GLTFName, ".metaverseFile"), metaverseFile);
        }

        private void CreateSceneFile(string GLBName)
        {
            SceneFile scene = new SceneFile();

            foreach (var light in bakeLights)
            {
                UnityEngine.Debug.Log("Handling light loop...");
                UnityEngine.Debug.Log(light.ToString());
                SceneObject lightObject = new SceneObject();
                lightObject.type = "application/light";
                // lightObject.args = [];
                // lightObject.shadow = [];

                Transform t = light.GetComponent<Transform>();

                lightObject.position = new float[3] { t.position.x, t.position.y, t.position.z };
                lightObject.quaternion = new float[4] { t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w };
                lightObject.scale = new float[3] { t.localScale.x, t.localScale.y, t.localScale.z };

                UnityEngine.Debug.Log(lightObject.ToString());


                scene.objects.Add(lightObject);
            }

            var sceneObject = new SceneObject();
            sceneObject.position = new float[3]{0,0,0};
            sceneObject.start_url = "https://webaverse.github.io/atmospheric-sky/";
            scene.objects.Add(sceneObject);

            var setting = new JsonSerializerSettings();
            setting.Formatting = Formatting.Indented;
            setting.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            setting.NullValueHandling = NullValueHandling.Ignore;

            var json = JsonConvert.SerializeObject(scene, setting);
            var path = Path.Combine(PipelineSettings.ConversionFolder + "/./Webaverse", PipelineSettings.GLTFName, PipelineSettings.GLTFName + ".scn");

            File.WriteAllText(path, json);

            // JsonConvert.Serialize
        }
    }
}

public class SceneFile
{
    public List<SceneObject> objects = new List<SceneObject>();
}

public class SceneObject
{
    public string type;
    public string start_url;
    public float[] position;
    public float[] quaternion;
    public float[] scale;
    public bool dynamic;
    public bool physics;
    public SceneObjectContent content;
}

public class SceneObjectContent
{
    public string lightType;
    public string args;
    public float[] position;
    public float[] quaternion;
    public float[] scale;
    public float[] shadow;
}