using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Texture2DDecoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;

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
    public static Image<Rgba32> LoadFromTextureFile(TextureFile textureFile, AssetsFileInstance inst, AssetBundleFile? bundle = null)
    {
        PixelFormat format;
        var bytes = bundle == null ? DecodeTextureFile(textureFile, inst, out format) : DecodeTextureFile(textureFile, inst, out format, bundle);
        if (bytes == null) return null;
        Image<Rgba32> image;
        if (format == PixelFormat.RGBA) image = Image.LoadPixelData<Rgba32>(Config.imageConfig, bytes, textureFile.m_Width, textureFile.m_Height);
        else if (format == PixelFormat.RGB) using (var img = Image.LoadPixelData<Rgb24>(bytes, textureFile.m_Width, textureFile.m_Height)) { image = img.CloneAs<Rgba32>(Config.imageConfig); }
        else using (var img = Image.LoadPixelData<Bgra32>(bytes, textureFile.m_Width, textureFile.m_Height)) { image = img.CloneAs<Rgba32>(Config.imageConfig); }
        image.Mutate(i => i.Flip(FlipMode.Vertical));
        return image;
    }
    private static byte[] DecodeTextureFile(TextureFile textureFile, AssetsFileInstance inst, out PixelFormat format, AssetBundleFile? bundle = null)
    {
        var textureFormat = (TextureFormat)textureFile.m_TextureFormat;
        byte[] textureData;
        if (bundle != null)
        {
            if (!textureFile.m_StreamData.path.StartsWith("archive:/a/")) textureFile.m_StreamData.path = "archive:/a/" + textureFile.m_StreamData.path;
            textureData = textureFile.GetTextureData("", bundle);
        }
        else textureData = textureFile.GetTextureData(inst);
        format = PixelFormat.BGRA;
        Console.WriteLine($"{textureFile.m_Name}: {(TextureFormat)textureFormat}");
        if (textureData == null || textureData.Length == 0)
        {
            if (textureFormat == TextureFormat.DXT1Crunched)
            {
                textureData = new byte[textureFile.m_Width * textureFile.m_Height * 4];
                var unpacked = TextureDecoder.UnpackUnityCrunch(textureFile.pictureData);
                TextureDecoder.DecodeDXT1(unpacked, textureFile.m_Width, textureFile.m_Height, textureData);
            }
            else if (textureFormat == TextureFormat.DXT5Crunched)
            {
                textureData = new byte[textureFile.m_Width * textureFile.m_Height * 4];
                var unpacked = TextureDecoder.UnpackUnityCrunch(textureFile.pictureData);
                TextureDecoder.DecodeDXT5(unpacked, textureFile.m_Width, textureFile.m_Height, textureData);
            }
            /*else if (textureFormat == TextureFormat.ETC2_RGBA8Crunched)
            {
                textureData = DecodeETC2(UnpackCrunch(textureFile.pictureData), textureFile.m_Width, textureFile.m_Height, 4);
                format = PixelFormat.RGBA;
            }
            else if (textureFormat == TextureFormat.ETC2_RGBA8)
            {
                textureData = DecodeETC2(textureFile.pictureData, textureFile.m_Width, textureFile.m_Height, 4);
                format = PixelFormat.RGBA;
            }*/ // ETC2 isn't supported in assetstools
            else if (textureFormat == TextureFormat.ETC_RGB4Crunched)
            {
                textureData = DecodeETC(UnpackCrunch(textureFile.pictureData), textureFile.m_Width, textureFile.m_Height, 4);
                format = PixelFormat.RGB;
            }
            else if (textureFormat == TextureFormat.RGBA32)
            {
                textureData = textureFile.pictureData;
                format = PixelFormat.RGBA;
            }
        }
        if (textureData == null || textureData.Length == 0) return null;
        return textureData;
    }
    private static byte[] UnpackCrunch(byte[] bytes)
    {
        return TextureDecoder.UnpackUnityCrunch(bytes);
    }
    private static byte[] DecodeETC2(byte[] bytes, int width, int height, int bpp)
    {
        byte[] image = new byte[width * height * bpp];
        TextureDecoder.DecodeETC2(bytes, width, height, image);
        return image;
    }
    private static byte[] DecodeETC(byte[] bytes, int width, int height, int bpp)
    {
        byte[] image = new byte[width * height * bpp];
        TextureDecoder.DecodeETC1(bytes, width, height, image);
        return image;
    }
    private enum PixelFormat
    {
        RGBA, BGRA, RGB
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
    public TextureData(AssetFileInfoEx info, AssetsFileInstance instance, AssetBundleFile bundle, AssetsManager am)
    {
        this.bundle = bundle;
        this.instance = instance;
        this.info = info;
        this.baseField = am.GetTypeInstance(instance, info).GetBaseField();
        this.textureFile = TextureFile.ReadTextureFile(baseField);
        this.name = textureFile.m_Name;
        this.width = textureFile.m_Width;
        this.height = textureFile.m_Height;
        this.id = $"{instance.name}.{info.absoluteFilePos}";
        this.image = ImageHelper.LoadFromTextureFile(textureFile, instance, bundle);
        if (image == null) throw new Exception($"Failed to load texture from bundle: {textureFile.m_Name}, format: {(TextureFormat)textureFile.m_TextureFormat}, w:{textureFile.m_Width} h:{textureFile.m_Height}");
    }
    public AssetBundleFile? bundle = null;
    public AssetsFileInstance instance;
    public AssetFileInfoEx info;
    public string name;
    public int width;
    public int height;
    public AssetTypeValueField baseField;
    public TextureFile textureFile;
    public Image<Rgba32>? image = null;
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
    public static string GenerateRandomPath()
    {
        return Path.Combine(Path.GetTempPath(), DateTime.Now.Ticks.ToString() + "_" + Guid.NewGuid().ToString());
    }
}
internal struct InstanceInfoPair
{
    public AssetsFileInstance instance;
    public AssetFileInfoEx info;
}