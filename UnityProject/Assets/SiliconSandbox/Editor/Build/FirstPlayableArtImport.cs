using System;
using System.IO;
using SiliconSandbox.Presentation;
using UnityEditor;
using UnityEngine;

namespace SiliconSandbox.EditorBuild
{
    public static class FirstPlayableArtImport
    {
        private const string Root = "Assets/SiliconSandbox/Art";
        private const string Generated = Root + "/Generated";
        private const string Resources = Root + "/Resources";
        private const string LibraryPath = Resources +
            "/SiliconSandboxVisualArt.asset";

        public static void Ensure()
        {
            var texturePath = Root + "/Textures/SS_SymbolAtlas_64.png";
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Visual atlas is missing: " + texturePath);
            if (importer.filterMode != FilterMode.Point ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                !importer.sRGBTexture)
            {
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null || texture.width != 64 || texture.height != 64)
                throw new InvalidOperationException("Visual atlas must be 64x64.");

            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory(Resources);
            var atlas = MaterialAt(Generated + "/SS_Atlas.mat", texture);
            var tint = MaterialAt(Generated + "/SS_Tint.mat", null);
            var art = AssetDatabase.LoadAssetAtPath<OneBitVisualArt>(LibraryPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<OneBitVisualArt>();
                AssetDatabase.CreateAsset(art, LibraryPath);
            }
            art.SourceBody = MeshAt("SS_SourceBody");
            art.AndBody = MeshAt("SS_AndBody");
            art.SrBody = MeshAt("SS_SrBody");
            art.ModuleBody = MeshAt("SS_ModuleBody");
            art.SourceZero = MeshAt("SS_SourceValue_0");
            art.SourceOne = MeshAt("SS_SourceValue_1");
            art.SourceX = MeshAt("SS_SourceValue_X");
            art.SourceZ = MeshAt("SS_SourceValue_Z");
            art.Pin = MeshAt("SS_Pin");
            art.WireStraight = MeshAt("SS_WireStraight");
            art.WireElbow = MeshAt("SS_WireElbow");
            art.Junction = MeshAt("SS_Junction");
            art.IdentityRing = MeshAt("SS_IdentityRing");
            art.AtlasMaterial = atlas;
            art.TintMaterial = tint;
            if (!art.IsComplete)
                throw new InvalidOperationException("Visual art library is incomplete.");
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
        }

        private static Mesh MeshAt(string name)
        {
            var path = Root + "/Meshes/" + name + ".fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var filter = prefab == null ? null :
                prefab.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException("Visual mesh is missing: " + path);
            return filter.sharedMesh;
        }

        private static Material MaterialAt(string path, Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null)
                    throw new InvalidOperationException("Standard shader is unavailable.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = Path.GetFileNameWithoutExtension(path);
            material.color = Color.white;
            material.mainTexture = texture;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.28f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
