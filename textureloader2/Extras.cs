using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Texture2DDecoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Advanced;

internal static class Config
{
    public static void Init()
    {
        imageConfig = Configuration.Default.Clone();
        imageConfig.PreferContiguousImageBuffers = true;
    }
    public static Configuration imageConfig;
    public static readonly string[] blockNameBlacklist = new string[] { "", "on", "off", "red", "blue", "connected", "base", "laser", "head", "shaft" };
    public static readonly string[] terrainNames = new string[] { "rock", "grass", "sand", "ice" };
    public static readonly string[] noIconBlocks = new string[] { "solar block", "station core", "station corridor corner", "station corridor cross", "station corridor straight", "station corridor t piece", "small piston", "magnet off" };
}
internal class ImageHelper
{
    public static Image<Rgba32> LoadFromTextureFile(TextureFile textureFile, AssetsFileInstance inst)
    {
        var bytes = DecodeTextureFile(textureFile, inst, out bool isRGBA);
        if (bytes == null) return null;
        Image<Rgba32> image;
        if (isRGBA) image = Image.LoadPixelData<Rgba32>(Config.imageConfig, bytes, textureFile.m_Width, textureFile.m_Height);
        else
        {
            using (var bgra = Image.LoadPixelData<Bgra32>(Config.imageConfig, bytes, textureFile.m_Width, textureFile.m_Height))
            {
                image = bgra.CloneAs<Rgba32>(bgra.GetConfiguration());
            }
        }
        image.Mutate(i => i.Flip(FlipMode.Vertical));
        return image;
    }
    private static byte[] DecodeTextureFile(TextureFile textureFile, AssetsFileInstance inst, out bool isRGBA)
    {
        isRGBA = false;
        var textureData = textureFile.GetTextureData(inst);
        if (textureData == null || textureData.Length == 0)
        {
            if ((TextureFormat)textureFile.m_TextureFormat == TextureFormat.DXT1Crunched)
            {
                textureData = new byte[textureFile.m_Width * textureFile.m_Height * 4];
                var unpacked = TextureDecoder.UnpackUnityCrunch(textureFile.pictureData);
                TextureDecoder.DecodeDXT1(unpacked, textureFile.m_Width, textureFile.m_Height, textureData);
            }
            else if ((TextureFormat)textureFile.m_TextureFormat == TextureFormat.DXT5Crunched)
            {
                textureData = new byte[textureFile.m_Width * textureFile.m_Height * 4];
                var unpacked = TextureDecoder.UnpackUnityCrunch(textureFile.pictureData);
                TextureDecoder.DecodeDXT5(unpacked, textureFile.m_Width, textureFile.m_Height, textureData);
            }
            else if ((TextureFormat)textureFile.m_TextureFormat == TextureFormat.RGBA32)
            {
                isRGBA = true;
                textureData = textureFile.pictureData;
            }
        }
        if (textureData == null || textureData.Length == 0) return null;
        return textureData;
    }
}
internal class TextureData
{
    public TextureData(AssetFileInfoEx info, AssetsFileInstance instance, AssetsManager am)
    {
        this.instance = instance;
        this.info = info;
        this.baseField = am.GetTypeInstance(instance, info).GetBaseField();
        this.textureFile = TextureFile.ReadTextureFile(baseField);
        this.name = textureFile.m_Name;
        this.width = textureFile.m_Width;
        this.height = textureFile.m_Height;
        this.id = $"{instance.name}.{info.absoluteFilePos}";
        this.image = ImageHelper.LoadFromTextureFile(textureFile, instance);
        if (image == null) throw new Exception($"Failed to load texture: {textureFile.m_Name}, format: {(TextureFormat)textureFile.m_TextureFormat}, w:{textureFile.m_Width} h:{textureFile.m_Height}");
    }
    public AssetsFileInstance instance;
    public AssetFileInfoEx info;
    public string name;
    public int width;
    public int height;
    public AssetTypeValueField baseField;
    public TextureFile textureFile;
    public Image<Rgba32> image;
    public string id;
}
internal static class StringHelper
{
    public static string TrimStart(this string str, string sStartValue)
    {
        if (str.StartsWith(sStartValue))
        {
            str = str.Remove(0, sStartValue.Length);
        }
        return str;
    }
    public static string TrimEnd(this string str, string sEndValue)
    {
        if (str.EndsWith(sEndValue))
        {
            str = str.Remove(str.Length - sEndValue.Length, sEndValue.Length);
        }
        return str;
    }
}
internal struct InstanceInfoPair
{
    public AssetsFileInstance instance;
    public AssetFileInfoEx info;
}