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
using YamlDotNet.Serialization;

internal static class Extract
{
    internal static string outputDir;
    internal static HashSet<TextureData> ignoreList;
    internal static Dictionary<string, Dictionary<string, string>> map;
    public static void Run(string assetDir, string outputDir)
    {
        Extract.outputDir = outputDir;
        ignoreList = new HashSet<TextureData>();
        map = new Dictionary<string, Dictionary<string, string>>();

        var am = new AssetsManager();
        var dict = GetTextureDictionary(am, assetDir);

        foreach (KeyValuePair<string, List<TextureData>> kvPair in dict)
        {
            foreach (var data in kvPair.Value)
            {
                if (ignoreList.Contains(data))
                {
                    continue;
                };

                var inst = data.instance;
                var info = data.info;
                var baseField = am.GetTypeInstance(inst, info).GetBaseField();
                var textureFile = TextureFile.ReadTextureFile(baseField);
                string name = textureFile.m_Name;
                int w = textureFile.m_Width;
                int h = textureFile.m_Height;
                var image = data.image;

                if (!(name.All((c) =>
                {
                    if (char.IsLetter(c)) return char.IsLower(c);
                    return true;
                }) || name.EndsWith(" Example") || name == "Droneboi"))
                {
                    continue;
                }
                if (w == 8 && h == 8) Save("items", data);
                else if (w == 32 && h == 32 && Config.terrainNames.Contains(name)) Save("terrain", data);
                else if (w % 16 == 0 && h % 16 == 0 && (name.EndsWith(" Example") || name == "Droneboi")) Save("ui", data);
                else if ((h == 1080 || h == 512) && data.name.StartsWith("gradient")) Save("ui", data);
                else if (w == 20 && h == 20 && data.name.StartsWith("asteroid")) Save("asteroids", data);
                else if (w % 16 == 0 && h % 16 == 0 && w <= 16 * 3 && h <= 16 * 4)
                {
                    if (name.EndsWith("capacity mask")) Save("capacity masks", data);
                    else if (name.EndsWith("capacity")) Save("capacity", data);
                    else if (name.EndsWith("skin mask")) Save("skin masks", data);
                    else if (name.EndsWith("mask")) Save("masks", data);
                    else
                    {
                        var blocks = GetRelatedBlocks(name, dict, am).ToList();
                        blocks = blocks.Where((block) =>
                        {
                            return block.width % 16 == 0 && block.height % 16 == 0 && block.width <= 16 * 3 && block.height <= 16 * 4;
                        }).ToList();

                        bool noIcon = false;
                        foreach (string ignore in Config.noIconBlocks)
                        {
                            if (name.Contains(ignore))
                            {
                                noIcon = true;
                                break;
                            }
                        }
                        if (noIcon)
                        {
                            Save("blocks", data);
                            continue;
                        }
                        if (blocks.Count < 2)
                        {
                            Save("skins", data);
                            continue;
                        }
                        foreach (var block in blocks.ToList())
                        {
                            if (ignoreList.Contains(data)) blocks.Remove(block);
                        }
                        ignoreList.UnionWith(blocks);
                        var iconCanidates = dict[GetOriginalBlockName(name)].Where((canidate) => blocks.Contains(canidate));
                        int highestOpaqueCount = -1;
                        TextureData blockIcon = null;
                        foreach (var canidate in iconCanidates)
                        {
                            int count = GetOpaquePixelCount(canidate);
                            if (count > highestOpaqueCount)
                            {
                                blockIcon = canidate;
                                highestOpaqueCount = count;
                            }
                        }
                        if (blockIcon == null) throw new Exception("Failed to find block icon of " + data.name);
                        Save("block icons", blockIcon);

                        blocks.Remove(blockIcon);
                        foreach (var block in blocks)
                        {
                            Save("blocks", block);
                        }
                    }
                }
                else if (data.width % 16 == 0 && data.name.Contains("solar panel")) Save("blocks", data);
                else if (
                    (data.width == 1024 && data.height == 1024 && data.name.Contains("nebula")) ||
                    (data.width == 512 && data.height == 512 && data.name.Contains("stars"))) Save("background", data);
                else if (
                    (data.width % 256 == 0 && data.height % 256 == 0) ||
                    data.name.Contains("discord")) Save("ui", data);
                else Save("unknown", data);
            }
        }

        am.UnloadAll();
        SaveMap();
    }
    public static Dictionary<string, List<TextureData>> GetTextureDictionary(AssetsManager am, string assetDir)
    {
        var assetFiles = new List<AssetsFileInstance>();
        foreach (FileInfo file in new DirectoryInfo(assetDir).GetFiles())
        {
            if (file.Name.EndsWith(".resS") || file.Name.EndsWith(".resource")) continue;
            assetFiles.Add(am.LoadAssetsFile(file.FullName, true));
        }

        am.LoadClassPackage("classdata.tpk");
        am.LoadClassDatabaseFromPackage(assetFiles[0].file.typeTree.unityVersion);

        var dict = new Dictionary<string, List<TextureData>>();

        foreach (var inst in assetFiles)
        {
            foreach (var info in inst.table.GetAssetsOfType((int)AssetClassID.Texture2D))
            {
                var baseField = am.GetTypeInstance(inst, info).GetBaseField();
                var textureFile = TextureFile.ReadTextureFile(baseField);
                if (textureFile.m_Width == 0 || textureFile.m_Height == 0) continue;
                string name = textureFile.m_Name;
                if (name.StartsWith("LDR") || name.Contains("Atlas") || name.Contains("yodo") || name.Contains("ads") || name.Contains("ad-")) continue;
                if (!dict.ContainsKey(name)) dict[name] = new List<TextureData>();
                dict[name].Add(new TextureData(info, inst, am));
            }
        }
        return dict;
    }
    private static void Save(string category, TextureData data)
    {
        // Console.WriteLine($"c: {category} n: {data.name}");
        string dir = Path.Combine(outputDir, category);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        data.image.SaveAsPng(Path.Combine(dir, data.name + ".png"));
        ignoreList.Add(data);

        if (!map.ContainsKey(category)) map[category] = new Dictionary<string, string>();
        map[category][data.name] = data.id;
    }
    private static void SaveMap()
    {
        Serializer serializer = new Serializer();
        File.WriteAllText(Path.Combine(outputDir, "map.yaml"), serializer.Serialize(map));
    }
    private static HashSet<TextureData> GetRelatedBlocks(string name, Dictionary<string, List<TextureData>> dict, AssetsManager am) // Includes original block
    {
        var set = new HashSet<TextureData>();
        var alts = GetRelatedBlockNames(name);
        foreach (string alt in alts)
        {
            if (dict.ContainsKey(alt)) set.UnionWith(dict[alt]);
        }
        return set;
    }
    private static HashSet<string> GetRelatedBlockNames(string name, int rep = 3) // Includes original block name
    {
        var set = new HashSet<string> { name };
        for (int i = 0; i<rep; i++)
        {
            foreach (string alt in set.ToArray())
            {
                set.UnionWith(new string[] {
                    alt+" on",
                    alt+" off",
                    alt+" connected",
                    alt+" base",
                    alt+" head",
                    alt+" shaft",
                    "laser "+alt,
                    StringHelper.TrimEnd(alt, " on"),
                    StringHelper.TrimEnd(alt, " off"),
                    StringHelper.TrimEnd(alt, " connected"),
                    StringHelper.TrimEnd(alt, " base"),
                    StringHelper.TrimEnd(alt, " head"),
                    StringHelper.TrimEnd(alt, " shaft"),
                    StringHelper.TrimStart(alt, "laser "),
                    StringHelper.TrimStart(alt, "red "),
                    StringHelper.TrimStart(alt, "blue ")
                });
            }
        }
        foreach (string alt in set.ToArray())
        {
            if (Config.blockNameBlacklist.Contains(alt)) set.Remove(alt);
        }
        return set;
    }
    private static string GetOriginalBlockName(string name, int rep = 3)
    {
        for (int i = 0; i<rep; i++)
        {
            name = StringHelper.TrimEnd(name, " on");
            name = StringHelper.TrimEnd(name, " off");
            name = StringHelper.TrimEnd(name, " connected");
            name = StringHelper.TrimEnd(name, " base");
            name = StringHelper.TrimEnd(name, " head");
            name = StringHelper.TrimEnd(name, " shaft");
        }
        return name;
    }
    private static int GetOpaquePixelCount(TextureData data)
    {
        int amount = 0;
        var image = data.image;
        var memory = image.GetPixelMemoryGroup();
        foreach (var mem in memory)
        {
            foreach (var pixelMem in mem.ToArray())
            {
                if (pixelMem.A == 255) amount++;
            }
        }
        return amount;
    }
}
