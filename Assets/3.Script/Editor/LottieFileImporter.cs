// LottieFileImporter.cs
// Assets/3.Script/Editor/ 폴더에 넣으세요

using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Gilzoide.LottiePlayer;

[ScriptedImporter(1, "lottie")]
public class LottieFileImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        LottieAnimationAsset animation = ScriptableObject.CreateInstance<LottieAnimationAsset>();
        animation.Json = File.ReadAllText(ctx.assetPath);
        animation.CacheKey = AssetDatabase.AssetPathToGUID(ctx.assetPath);
        animation.ResourcePath = "";
        animation.UpdateMetadata();
        ctx.AddObjectToAsset("main", animation);
        ctx.SetMainObject(animation);
    }
}