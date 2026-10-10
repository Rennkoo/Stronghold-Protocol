using UnityEditor;
public class BenchmarkTextureImporter : AssetPostprocessor {
    void OnPreprocessTexture() {
        if(!assetPath.StartsWith("Assets/Resources/Models/")) return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.alphaIsTransparency=false;
        importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=4096;
    }
}
