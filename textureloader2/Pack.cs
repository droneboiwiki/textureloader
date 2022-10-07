using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Advanced;
using YamlDotNet.Serialization;

internal static class Pack
{
    public static void Run(string assetDir, string texturesDir, string outputDir)
    {
        var map = new Deserializer().Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(Path.Combine(texturesDir, "map.yaml")));
        
        var am = new AssetsManager();
        var dict = Extract.GetTextureDictionary(am, assetDir);

        var replacers = new Dictionary<AssetsFileInstance, List<AssetsReplacer>>();
        foreach (var kvPair in map)
        {
            var dir = new DirectoryInfo(Path.Combine(texturesDir, kvPair.Key));
            foreach (var pair in kvPair.Value)
            {
                var name = pair.Key;
                var id = pair.Value;
                var file = new FileInfo(Path.Combine(dir.FullName, name + ".png"));
                var data = dict[pair.Key].Where(d => d.id == id).ToArray()[0];

                var image1 = data.image;
                var image2 = Image.Load<Rgba32>(file.FullName);
                if (ImageEquals(image1, image2)) continue;

                var texture = data.textureFile;
                var imageBytes = new byte[4 * data.width * data.height];
                image2.Mutate(i => i.Flip(FlipMode.Vertical));
                image2.CloneAs<Bgra32>().CopyPixelDataTo(imageBytes);
                texture.m_TextureFormat = (int)TextureFormat.RGBA32;
                texture.SetTextureData(imageBytes, data.width, data.height);
                texture.WriteTo(data.baseField);

                var bytes = data.baseField.WriteToByteArray();
                if (!replacers.ContainsKey(data.instance)) replacers[data.instance] = new List<AssetsReplacer>();
                var replacer = new AssetsReplacerFromMemory(0, data.info.index, (int)data.info.curFileType, 0xffff, bytes);
                replacers[data.instance].Add(replacer);
            }
        }

        foreach (var kvPair in replacers)
        {
            var instance = kvPair.Key;
            var list = kvPair.Value;

            var filename = Path.GetFileName(instance.path);
            using (var stream = File.OpenWrite(Path.Combine(outputDir, filename)))
            {
                using (var writer = new AssetsFileWriter(stream))
                {
                    instance.file.Write(writer, 0, list, 0);
                }
            }
        }

        am.UnloadAll();
    }
    private static bool ImageEquals(Image<Rgba32> image1, Image<Rgba32> image2)
    {
        if (image1.Width != image2.Width || image1.Height != image2.Height) return false;
        var list = new List<Rgba32>(image1.Width * image1.Height);
        foreach (var mem in image1.GetPixelMemoryGroup()) foreach (var pixel in mem.ToArray()) list.Add(pixel);
        int i = 0;
        foreach (var mem in image2.GetPixelMemoryGroup())
        {
            foreach (var pixel2 in mem.ToArray())
            {
                var pixel1 = list[i];
                if (pixel1.R != pixel2.R || pixel1.G != pixel2.G || pixel1.B != pixel2.B || pixel1.A != pixel2.A) return false;
                i++;
            }
        }
        return true;
    }
}
