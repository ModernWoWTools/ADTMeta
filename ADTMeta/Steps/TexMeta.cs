using ADTMeta.Provider;
using ADTMeta.Struct;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Warcraft.NET.Files.ADT.TerrainTexture.BfA;

namespace ADTMeta.Steps
{
    public static class TexMeta
    {
        private const string META_FOLDER_TEXTURE = "Texture";
        private const string META_FOLDER_GROUND_EFFECT = "GroundEffect";

        private const string META_FILE_TEXTURE_INFO_BY_TEXTURE_FILE_ID = "TextureInfoByTextureFileID.json";
        private const string META_FILE_TEXTURE_INFO_BY_TEXTURE_FILE_PATH = "TextureInfoByTextureFilePath.json";
        private const string META_FILE_GROUND_EFFECT_BY_TEXTURE_FILE_ID = "GroundEffectByTextureFileID.json";
        private const string META_FILE_GROUND_EFFECT_BY_TEXTURE_FILE_PATH = "GroundEffectByTextureFilePath.json";

        private static ConcurrentDictionary<int, TextureInfo> _textureInfoMap = new ConcurrentDictionary<int, TextureInfo>();
        private static ConcurrentDictionary<int, List<uint>> _textureGroundEffectMap = new ConcurrentDictionary<int, List<uint>>();

        public static void Generate()
        {
            Console.WriteLine("[INFO] Generating texture meta");
            Load();

            Parallel.ForEach(ListFile.NameMap.Where(l => l.Value.StartsWith("world/maps/kultiras/kultiras") && l.Value.EndsWith("_tex0.adt")), entry =>
            {
                if (!CASC.Instance.FileExists(entry.Key))
                    return;

                try
                {
                    if (AppSettings.Instance.Verbose)
                        Console.WriteLine($"[DEBUG] Open {entry.Value}");

                    using (var fileStream = CASC.Instance.OpenFile(entry.Key))
                    using (var memoryStream = new MemoryStream())
                    {
                        fileStream.CopyTo(memoryStream);
                        fileStream.Close();
                        memoryStream.Position = 0;

                        TerrainTexture terrainTexture = new TerrainTexture(memoryStream.ToArray());
                        CollectTextureParameters(terrainTexture);
                        CollectGroundEffects(terrainTexture);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to process {entry.Value} " + ex.Message);
                    return;
                }
            });

            RemoveGroundEffectDuplicates();
            AddDefaultsFromListfile();
            Save();
        }

        private static void CollectTextureParameters(TerrainTexture terrainTexture)
        {
            if (terrainTexture.TextureParameters == null || terrainTexture.TextureHeightIds == null)
                return;

            for (int i = 0; i < terrainTexture.TextureParameters.TextureFlagEntries.Count; i++)
            {
                var mtxp = terrainTexture.TextureParameters.TextureFlagEntries[i];
                if (mtxp.HeightScale == 0 && mtxp.HeightOffset == 1)
                    continue;

                if (terrainTexture.TextureHeightIds.Textures[i] == 0)
                    continue;

                if (_textureInfoMap.TryGetValue((int)terrainTexture.TextureHeightIds.Textures[i], out var existingInfo))
                {

                    if (existingInfo.Scale != mtxp.TextureScale || existingInfo.HeightScale != mtxp.HeightScale || existingInfo.HeightOffset != mtxp.HeightOffset)
                    {
                        // Check if the old values were defaults, if so don't bother 
                        if (existingInfo.Scale == 1 && existingInfo.HeightScale == 6 && existingInfo.HeightOffset == 1)
                            continue;

                        if (AppSettings.Instance.Verbose)
                        {
                            Console.WriteLine("[DEBUG] Texture " + terrainTexture.TextureHeightIds.Textures[i] + " has conflicting info");
                            Console.WriteLine("\t Existing: " + existingInfo.Scale + " " + existingInfo.HeightScale + " " + existingInfo.HeightOffset);
                            Console.WriteLine("\t New: " + mtxp.TextureScale + " " + mtxp.HeightScale + " " + mtxp.HeightOffset);
                        }
                    }
                }

                _textureInfoMap[(int)terrainTexture.TextureHeightIds.Textures[i]] = new TextureInfo
                {
                    Scale = mtxp.TextureScale,
                    HeightScale = mtxp.HeightScale,
                    HeightOffset = mtxp.HeightOffset
                };
            }
        }

        private static void CollectGroundEffects(TerrainTexture terrainTexture)
        {
            if (terrainTexture.TextureDiffuseIds == null || terrainTexture.Chunks == null)
                return;

            foreach (var chunk in terrainTexture.Chunks)
            {
                if (chunk.TextureLayers == null)
                    continue;

                foreach (var layer in chunk.TextureLayers.Layers)
                {
                    if (layer.EffectID == 0)
                        continue;

                    var textureId = (int)terrainTexture.TextureDiffuseIds.Textures[(int)layer.TextureID];
                    if (textureId <= 0)
                        continue;

                    if (!_textureGroundEffectMap.ContainsKey(textureId))
                        _textureGroundEffectMap[textureId] = new List<uint>();

                    if (!_textureGroundEffectMap[textureId].Contains(layer.EffectID))
                        _textureGroundEffectMap[textureId].Add(layer.EffectID);
                }
            }
        }

        private static void AddDefaultsFromListfile()
        {
            if (AppSettings.Instance.Verbose)
                Console.WriteLine("[DEBUG] Adding tilesets from listfile starting with tileset and ending in _h.blp with default values (can be wrong)");

            foreach (var file in ListFile.NameMap.Where(x => x.Value.EndsWith("_h.blp") && x.Value.StartsWith("tileset")))
            {
                TextureInfo textureInfo = new TextureInfo
                {
                    Scale = 1,
                    HeightScale = 6,
                    HeightOffset = 1
                };

                if (!_textureInfoMap.ContainsKey(file.Key))
                {
                    if (AppSettings.Instance.Verbose)
                        Console.WriteLine("[DEBUG] Adding " + file.Value + " from listfile");

                    _textureInfoMap[file.Key] = textureInfo;
                }
            }
        }

        // Code from Marlamin's MetaGen: https://github.com/Marlamin/MapUpconverter/blob/main/MetaGen/Scanners/ADT.cs#L140
        private static void RemoveGroundEffectDuplicates()
        {
            var dbcd = new DBCD.DBCD(new CASCDBCProvider(), new DBCD.Providers.GithubDBDProvider());

            var groundEffectDB = dbcd.Load("GroundEffectTexture");
            var groundEffectMap = new Dictionary<int, int>();
            foreach (var groundEffectRow in groundEffectDB.Values)
            {
                var rowString = "";
                foreach (var field in groundEffectDB.AvailableColumns)
                {
                    var value = groundEffectRow[field];
                    switch (value.GetType().ToString())
                    {
                        case "System.SByte[]":
                            var sbyteArray = (sbyte[])value;
                            for (var i = 0; i < sbyteArray.Length; i++)
                            {
                                rowString += sbyteArray[i].ToString() + "_";
                            }
                            break;
                        case "System.UInt16[]":
                            var uintArray = (ushort[])value;
                            for (var i = 0; i < uintArray.Length; i++)
                            {
                                rowString += uintArray[i].ToString() + "_";
                            }
                            break;
                        case "System.Byte":
                            rowString += ((byte)value).ToString() + "_";
                            break;
                        case "System.Int32":
                            rowString += ((int)value).ToString() + "_";
                            break;
                        case "System.UInt32":
                            rowString += ((uint)value).ToString() + "_";
                            break;
                        default:
                            Console.WriteLine("Unknown type: " + value.GetType());
                            break;
                    }
                }

                groundEffectMap.Add((int)groundEffectRow["ID"], rowString.GetHashCode());
            }

            foreach (var texture in _textureGroundEffectMap)
            {
                var groundEffects = texture.Value;
                var newGEs = new List<uint>();
                var usedHashes = new List<int>();
                foreach (var groundEffect in groundEffects)
                {
                    if (groundEffectMap.TryGetValue((int)groundEffect, out var hash))
                    {
                        if (!usedHashes.Contains(hash))
                        {
                            newGEs.Add(groundEffect);
                            usedHashes.Add(hash);
                        }
                    }
                }

                _textureGroundEffectMap[texture.Key] = newGEs;
            }

            _textureGroundEffectMap = new(_textureGroundEffectMap.Where(x => x.Value.Count > 0).ToDictionary());
        }
        
        private static void Load()
        {
            try
            {
                string textureMetaFile = Path.Combine(AppSettings.Instance.MetaFolder, META_FOLDER_TEXTURE, META_FILE_TEXTURE_INFO_BY_TEXTURE_FILE_ID);
                if (File.Exists(textureMetaFile))
                    _textureInfoMap = new ConcurrentDictionary<int, TextureInfo>(JsonConvert.DeserializeObject<Dictionary<int, TextureInfo>>(File.ReadAllText(textureMetaFile)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load texture meta: {ex.Message}");
            }

            try
            {
                string groundEffectMetaFile = Path.Combine(AppSettings.Instance.MetaFolder, META_FOLDER_GROUND_EFFECT, META_FILE_GROUND_EFFECT_BY_TEXTURE_FILE_ID);
                if (File.Exists(groundEffectMetaFile))
                    _textureGroundEffectMap = new ConcurrentDictionary<int, List<uint>>(JsonConvert.DeserializeObject<Dictionary<int, List<uint>>>(File.ReadAllText(groundEffectMetaFile)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load ground effect meta: {ex.Message}");
            }
        }

        private static void Save()
        {
            var textureMetaPath = Path.Combine(AppSettings.Instance.MetaFolder, META_FOLDER_TEXTURE);
            if (!Directory.Exists(textureMetaPath))
                Directory.CreateDirectory(textureMetaPath);

            SaveTextureInfoByTextureFileID(Path.Combine(textureMetaPath, META_FILE_TEXTURE_INFO_BY_TEXTURE_FILE_ID));
            SaveTextureInfoByTextureFilePath(Path.Combine(textureMetaPath, META_FILE_TEXTURE_INFO_BY_TEXTURE_FILE_PATH));

            var groundEffectMetaPath = Path.Combine(AppSettings.Instance.MetaFolder, META_FOLDER_GROUND_EFFECT);
            if (!Directory.Exists(groundEffectMetaPath))
                Directory.CreateDirectory(groundEffectMetaPath);

            SaveGroundEffecByTextureFileID(Path.Combine(groundEffectMetaPath, META_FILE_GROUND_EFFECT_BY_TEXTURE_FILE_ID));
            SaveGroundEffectByTextureFilePath(Path.Combine(groundEffectMetaPath, META_FILE_GROUND_EFFECT_BY_TEXTURE_FILE_PATH));
        }

        private static void SaveTextureInfoByTextureFileID(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(_textureInfoMap.OrderBy(x => x.Key).ToDictionary(x => x.Key.ToString(), x => x.Value), Formatting.Indented));
        }

        public static void SaveTextureInfoByTextureFilePath(string path)
        {
            var textureInfoByFilePath = new Dictionary<string, TextureInfo>();
            foreach (var entry in _textureInfoMap)
            {
                if (ListFile.NameMap.TryGetValue(entry.Key, out var filename))
                    textureInfoByFilePath[filename.Replace("_h.blp", ".blp")] = entry.Value;
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(textureInfoByFilePath.OrderBy(x => x.Key).ToDictionary(), Formatting.Indented));
        }

        public static void SaveGroundEffecByTextureFileID(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(_textureGroundEffectMap.OrderBy(x => x.Key).ToDictionary(x => x.Key.ToString(), x => x.Value), Formatting.Indented));
        }

        public static void SaveGroundEffectByTextureFilePath(string path)
        {
            var groundEffectIDsByFilePath = new Dictionary<string, List<uint>>();
            foreach (var entry in _textureGroundEffectMap)
            {
                if (ListFile.NameMap.TryGetValue(entry.Key, out var filename))
                    groundEffectIDsByFilePath[filename.Replace("_h.blp", ".blp").Replace("_s.blp", ".blp")] = entry.Value;
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(groundEffectIDsByFilePath.OrderBy(x => x.Key).ToDictionary(), Formatting.Indented));
        }
    }
}
