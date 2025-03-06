using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using CodeWalker.Utils;

namespace CodeWalker.Tools
{
    public partial class FiveMResourceForm : Form
    {
        private readonly RpfManager rpfManager;
        private readonly List<RpfEntry> selectedItems;
        private readonly Dictionary<uint, string> texturePathCache = new Dictionary<uint, string>();

        public FiveMResourceForm(RpfManager rpfManager, List<RpfEntry> selectedItems)
        {
            InitializeComponent();
            this.rpfManager = rpfManager;
            this.selectedItems = selectedItems;
            LoadSelectedItems();
        }

        private void LoadSelectedItems()
        {
            SelectedItemsListBox.Items.Clear();
            foreach (var item in selectedItems)
            {
                SelectedItemsListBox.Items.Add(item.Path);
            }
        }

        private void ResourceNameTextBox_TextChanged(object sender, EventArgs e)
        {
            CreateResourceButton.Enabled = !string.IsNullOrWhiteSpace(ResourceNameTextBox.Text) &&
                                         !string.IsNullOrWhiteSpace(OutputDirectoryTextBox.Text);
        }

        private void OutputDirectoryButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select output directory for the FiveM resource";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    OutputDirectoryTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void CreateResourceButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ResourceNameTextBox.Text))
            {
                MessageBox.Show("Please enter a resource name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputDirectoryTextBox.Text))
            {
                MessageBox.Show("Please select an output directory.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                CreateFiveMResource();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating FiveM resource: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateFiveMResource()
        {
            try
            {
                string resourceName = ResourceNameTextBox.Text;
                string outputDir = Path.Combine(OutputDirectoryTextBox.Text, resourceName);
                string streamDir = Path.Combine(outputDir, "stream");

                // Create directories
                Directory.CreateDirectory(outputDir);
                Directory.CreateDirectory(streamDir);

                // Create manifest file
                CreateManifestFile(outputDir);

                // Track processed textures to avoid duplicates
                var processedTextures = new HashSet<uint>();

                // Extract selected models and their dependencies
                foreach (var item in selectedItems)
                {
                    try
                    {
                        if (item is RpfFileEntry fileEntry)
                        {
                            string fileName = Path.GetFileName(fileEntry.Path);
                            string outputPath = Path.Combine(streamDir, fileName);

                            // Extract the file
                            byte[] fileData = fileEntry.File.ExtractFile(fileEntry);
                            if (fileData != null)
                            {
                                File.WriteAllBytes(outputPath, fileData);
                            }

                            // Handle dependencies based on file type
                            if (fileName.EndsWith(".ydr", StringComparison.OrdinalIgnoreCase))
                            {
                                ExtractYdrDependencies(item, streamDir, processedTextures);
                            }
                            else if (fileName.EndsWith(".yft", StringComparison.OrdinalIgnoreCase))
                            {
                                ExtractYftDependencies(item, streamDir, processedTextures);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error processing file {item?.Path}: {ex.Message}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        // Continue with next file
                    }
                }

                MessageBox.Show("FiveM resource created successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating FiveM resource: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExtractYdrDependencies(RpfEntry item, string streamDir, HashSet<uint> processedTextures)
        {
            try
            {
                var ydrFile = RpfFile.GetFile<YdrFile>(item);
                if (ydrFile?.Drawable != null)
                {
                    string baseName = Path.GetFileNameWithoutExtension(item.Path).ToLowerInvariant();
                    
                    // Track all discovered texture names to search for their YTD files
                    var discoveredTextureNames = new HashSet<string>();

                    // Add the base name as a potential texture name
                    discoveredTextureNames.Add(baseName);

                    // Extract embedded textures if any
                    if (ydrFile.Drawable.ShaderGroup?.TextureDictionary != null)
                    {
                        SafeExtractTextureDictionary(ydrFile.Drawable.ShaderGroup.TextureDictionary, streamDir, processedTextures, discoveredTextureNames);
                    }

                    // Extract shader-referenced textures
                    if (ydrFile.Drawable.ShaderGroup != null)
                    {
                        SafeExtractShaderTextures(ydrFile.Drawable.ShaderGroup, streamDir, processedTextures, discoveredTextureNames);
                    }

                    // Add name variations for textures
                    AddTextureNameVariations(baseName, discoveredTextureNames);

                    // Find and extract YTD files for all discovered texture names
                    foreach (var textureName in discoveredTextureNames)
                    {
                        ExtractYtdForModel(textureName, streamDir);
                    }

                    // Extract all shared texture dictionaries
                    ExtractSharedTextureDictionaries(streamDir);

                    // Find and extract all related YTYPs (they might be in shared files)
                    var allYtyps = FindAllRpfEntries(".ytyp");
                    foreach (var ytypEntry in allYtyps)
                    {
                        try
                        {
                            var ytypFile = RpfFile.GetFile<YtypFile>(ytypEntry);
                            if (ytypFile?.AllArchetypes != null)
                            {
                                foreach (var archetype in ytypFile.AllArchetypes)
                                {
                                    string archetypeName = archetype.Name.ToString().ToLowerInvariant();
                                    if (archetypeName.Equals(baseName, StringComparison.OrdinalIgnoreCase) ||
                                        archetypeName.Contains("_" + baseName) ||
                                        baseName.Contains(archetypeName) ||
                                        // Also check if archetype name contains parts of the base name
                                        baseName.Split('_').Any(part => archetypeName.Contains(part)))
                                    {
                                        ExtractRpfEntry(ytypEntry, streamDir);
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception) { /* Skip problematic YTYP */ }
                    }

                    // Find and extract collision files (YBN)
                    // Try multiple naming patterns for collision files
                    ExtractYbnForModel(baseName, streamDir);

                    // Try variations of the model name for texture dictionaries
                    var nameVariations = new List<string>(discoveredTextureNames);
                    
                    // Extract YMAPs that reference this model
                    var allYmaps = FindAllRpfEntries(".ymap");
                    foreach (var ymapEntry in allYmaps)
                    {
                        try
                        {
                            var ymapFile = RpfFile.GetFile<YmapFile>(ymapEntry);
                            if (ymapFile?.AllEntities != null)
                            {
                                bool containsModel = false;

                                // Check for exact matches and variations
                                containsModel = ymapFile.AllEntities.Any(e => 
                                    e._CEntityDef.archetypeName.Hash == JenkHash.GenHash(baseName) ||
                                    nameVariations.Any(v => e._CEntityDef.archetypeName.Hash == JenkHash.GenHash(v)));

                                // Also check for LOD variants
                                if (!containsModel)
                                {
                                    var lodVariants = new[]
                                    {
                                        baseName + "_lod",
                                        baseName + "_loda",
                                        baseName + "_lodb",
                                        baseName + "_slod1",
                                        baseName + "_slod2",
                                        baseName + "_slod3",
                                        baseName + "_slod4"
                                    };

                                    containsModel = ymapFile.AllEntities.Any(e =>
                                        lodVariants.Any(lod => e._CEntityDef.archetypeName.Hash == JenkHash.GenHash(lod)));
                                }

                                if (containsModel)
                                {
                                    ExtractRpfEntry(ymapEntry, streamDir);
                                }
                            }
                        }
                        catch (Exception) { /* Skip problematic YMAP */ }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error extracting YDR dependencies for {item.Path}: {ex.Message}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Add texture name variations to search for
        private void AddTextureNameVariations(string baseName, HashSet<string> variations)
        {
            // Basic variations
            variations.Add(baseName + "_hi");
            variations.Add(baseName + "_veh");
            variations.Add(baseName + "_1");
            variations.Add(baseName + "_2");
            variations.Add("v_" + baseName);
            variations.Add("hei_" + baseName);
            variations.Add("prop_" + baseName);
            variations.Add(baseName + "_diff");
            variations.Add(baseName + "_n");
            variations.Add(baseName + "_spec");
            variations.Add(baseName + "_detail");
            variations.Add(baseName + "_bump");
            variations.Add(baseName + "_normal");
            variations.Add(baseName.Replace("_", ""));

            // If baseName contains "_", try both parts separately and with common prefixes/suffixes
            if (baseName.Contains("_"))
            {
                var parts = baseName.Split('_');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(parts[i]))
                    {
                        variations.Add(parts[i]);
                        variations.Add(parts[i] + "_hi");
                        variations.Add(parts[i] + "_diff");
                        variations.Add(parts[i] + "_n");
                        
                        // Also try common prefixes
                        variations.Add("v_" + parts[i]);
                        variations.Add("prop_" + parts[i]);
                        variations.Add("veh_" + parts[i]);
                    }
                }
            }
        }

        // Extract all shared texture dictionaries that might contain needed textures
        private void ExtractSharedTextureDictionaries(string streamDir)
        {
            var sharedYtds = new[] {
                "vehshare.ytd",
                "vehshare_truck.ytd",
                "generic.ytd", 
                "generic_textures.ytd",
                "vehicle_generic.ytd",
                "vehicle_decals.ytd",
                "vehicle_misc.ytd",
                "props.ytd",
                "props_textures.ytd",
                "cutscene_textures.ytd",
                "cs_global.ytd",
                "civilian_textures.ytd",
                "item_textures.ytd",
                "shared_textures.ytd",
                "common.ytd"
            };

            foreach (var sharedYtdName in sharedYtds)
            {
                var sharedEntry = FindRpfEntry(sharedYtdName);
                if (sharedEntry != null)
                {
                    ExtractRpfEntry(sharedEntry, streamDir);
                }
            }
        }

        private void ExtractYftDependencies(RpfEntry item, string streamDir, HashSet<uint> processedTextures)
        {
            try
            {
                var yftFile = RpfFile.GetFile<YftFile>(item);
                if (yftFile?.Fragment?.Drawable != null)
                {
                    // Track discovered texture names
                    var discoveredTextureNames = new HashSet<string>();
                    string baseName = Path.GetFileNameWithoutExtension(item.Path).ToLowerInvariant();
                    
                    // Add the base name as a potential texture name
                    discoveredTextureNames.Add(baseName);

                    // Extract embedded textures if any
                    if (yftFile.Fragment.Drawable.ShaderGroup?.TextureDictionary != null)
                    {
                        SafeExtractTextureDictionary(yftFile.Fragment.Drawable.ShaderGroup.TextureDictionary, streamDir, processedTextures, discoveredTextureNames);
                    }

                    // Extract shader-referenced textures
                    if (yftFile.Fragment.Drawable.ShaderGroup != null)
                    {
                        SafeExtractShaderTextures(yftFile.Fragment.Drawable.ShaderGroup, streamDir, processedTextures, discoveredTextureNames);
                    }

                    // Add name variations
                    AddTextureNameVariations(baseName, discoveredTextureNames);

                    // Find and extract YTD files for all discovered texture names
                    foreach (var textureName in discoveredTextureNames)
                    {
                        ExtractYtdForModel(textureName, streamDir);
                    }

                    // Extract shared texture dictionaries
                    ExtractSharedTextureDictionaries(streamDir);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error extracting YFT dependencies for {item.Path}: {ex.Message}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SafeExtractShaderTextures(ShaderGroup shaderGroup, string outputDir, HashSet<uint> processedTextures, HashSet<string> discoveredTextureNames = null)
        {
            if (shaderGroup?.Shaders?.data_items == null) return;

            foreach (var shader in shaderGroup.Shaders.data_items)
            {
                try
                {
                    if (shader?.ParametersList?.Parameters == null) continue;

                    foreach (var param in shader.ParametersList.Parameters)
                    {
                        try
                        {
                            if (param?.Data is TextureBase texBase)
                            {
                                // Record texture name for later searching
                                if (discoveredTextureNames != null && texBase.Name != null)
                                {
                                    string texName = texBase.Name.ToString().ToLowerInvariant();
                                    if (!string.IsNullOrWhiteSpace(texName))
                                    {
                                        discoveredTextureNames.Add(texName);
                                    }
                                }

                                var texture = FindTexture(texBase.NameHash);
                                if (texture != null)
                                {
                                    SafeExtractTexture(texture, outputDir, processedTextures, discoveredTextureNames);
                                }
                            }
                        }
                        catch (Exception) { /* Skip problematic parameter */ }
                    }
                }
                catch (Exception) { /* Skip problematic shader */ }
            }
        }

        private void SafeExtractTextureDictionary(TextureDictionary textureDict, string outputDir, HashSet<uint> processedTextures, HashSet<string> discoveredTextureNames = null)
        {
            if (textureDict?.Textures?.data_items == null) return;

            foreach (var texture in textureDict.Textures.data_items)
            {
                try
                {
                    if (texture != null)
                    {
                        // Record texture name for later searching
                        if (discoveredTextureNames != null && texture.Name != null)
                        {
                            string texName = texture.Name.ToString().ToLowerInvariant();
                            if (!string.IsNullOrWhiteSpace(texName))
                            {
                                discoveredTextureNames.Add(texName);
                            }
                        }

                        SafeExtractTexture(texture, outputDir, processedTextures, discoveredTextureNames);
                    }
                }
                catch (Exception) { /* Skip problematic texture */ }
            }
        }

        private void SafeExtractTexture(Texture texture, string outputDir, HashSet<uint> processedTextures, HashSet<string> discoveredTextureNames = null)
        {
            if (texture == null || processedTextures.Contains(texture.NameHash)) return;

            try
            {
                // Record texture name for later searching
                if (discoveredTextureNames != null && texture.Name != null)
                {
                    string texName = texture.Name.ToString().ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(texName))
                    {
                        discoveredTextureNames.Add(texName);
                    }
                }

                string texturePath = GetTexturePath(texture.NameHash);
                if (string.IsNullOrEmpty(texturePath)) return;

                var textureEntry = rpfManager.GetEntry(texturePath);
                if (textureEntry is RpfFileEntry textureFileEntry)
                {
                    string textureFileName = Path.GetFileName(textureFileEntry.Path);
                    if (string.IsNullOrEmpty(textureFileName)) return;

                    string textureOutputPath = Path.Combine(outputDir, textureFileName);
                    
                    // Only extract if not already extracted
                    if (!File.Exists(textureOutputPath))
                    {
                        byte[] textureData = textureFileEntry.File.ExtractFile(textureFileEntry);
                        if (textureData != null && textureData.Length > 0)
                        {
                            File.WriteAllBytes(textureOutputPath, textureData);
                            processedTextures.Add(texture.NameHash);
                        }
                    }

                    // Also extract the YTD that contains this texture
                    string ytdPath = Path.GetDirectoryName(textureFileEntry.Path) + "\\" + 
                                     Path.GetFileNameWithoutExtension(textureFileEntry.Path) + ".ytd";
                    var ytdEntry = rpfManager.GetEntry(ytdPath) as RpfFileEntry;
                    if (ytdEntry != null)
                    {
                        ExtractRpfEntry(ytdEntry, outputDir);
                    }
                }
            }
            catch (Exception) { /* Skip problematic texture */ }
        }

        private void CreateManifestFile(string outputDir)
        {
            string manifestPath = Path.Combine(outputDir, "fxmanifest.lua");
            using (var writer = new StreamWriter(manifestPath))
            {
                writer.WriteLine("fx_version 'cerulean'");
                writer.WriteLine("game 'gta5'");
                writer.WriteLine();
                writer.WriteLine($"name '{ResourceNameTextBox.Text}'");
                writer.WriteLine("description 'Created with CodeWalker'");
                writer.WriteLine();
                writer.WriteLine("files {");
                writer.WriteLine("    'stream/*.ydr',");
                writer.WriteLine("    'stream/*.yft',");
                writer.WriteLine("    'stream/*.ytd',");
                writer.WriteLine("    'stream/*.ybn',");
                writer.WriteLine("    'stream/*.ytyp',");
                writer.WriteLine("    'stream/*.ymap',");
                writer.WriteLine("    'stream/*_lod.ydr',");
                writer.WriteLine("    'stream/*_loda.ydr',");
                writer.WriteLine("    'stream/*_lodb.ydr',");
                writer.WriteLine("    'stream/*.ydd'");
                writer.WriteLine("}");
                writer.WriteLine();

                // Add data_file directives based on file types present
                var fileTypes = selectedItems.Select(item => Path.GetExtension(item.Path).ToLowerInvariant())
                                          .Distinct()
                                          .ToList();

                // Always include YTYP since it's critical for model registration
                writer.WriteLine("data_file 'DLC_ITYP_REQUEST' 'stream/*.ytyp'");

                if (fileTypes.Contains(".yft"))
                {
                    writer.WriteLine("data_file 'VEHICLE_FILE' 'stream/*.yft'");
                }

                if (fileTypes.Contains(".ymap"))
                {
                    writer.WriteLine("data_file 'DLC_YMAP_REQUEST' 'stream/*.ymap'");
                }

                if (fileTypes.Contains(".ydd"))
                {
                    writer.WriteLine("data_file 'DLC_ITYP_REQUEST' 'stream/*.ydd'");
                }

                // Add client script to handle dynamic loading if needed
                bool needsDynamicLoading = fileTypes.Any(ext => ext == ".yft" || ext == ".ymap");
                if (needsDynamicLoading)
                {
                    writer.WriteLine();
                    writer.WriteLine("client_script 'client.lua'");
                    
                    // Create client.lua
                    string clientLuaPath = Path.Combine(outputDir, "client.lua");
                    using (var luaWriter = new StreamWriter(clientLuaPath))
                    {
                        luaWriter.WriteLine("-- Automatically load all resource assets");
                        luaWriter.WriteLine("Citizen.CreateThread(function()");
                        luaWriter.WriteLine("    local resourceName = GetCurrentResourceName()");
                        luaWriter.WriteLine();
                        luaWriter.WriteLine("    -- Load all YTYPs");
                        luaWriter.WriteLine("    local ytypFiles = { 'stream/*.ytyp' }");
                        luaWriter.WriteLine("    for _, ytypPattern in ipairs(ytypFiles) do");
                        luaWriter.WriteLine("        local files = GetStreamingFileForType(ytypPattern)");
                        luaWriter.WriteLine("        for _, file in ipairs(files) do");
                        luaWriter.WriteLine("            local success = RequestIpl(file)");
                        luaWriter.WriteLine("            if not success then");
                        luaWriter.WriteLine("                print('^1Failed to load YTYP: ' .. file .. '^7')");
                        luaWriter.WriteLine("            end");
                        luaWriter.WriteLine("        end");
                        luaWriter.WriteLine("    end");
                        luaWriter.WriteLine();
                        luaWriter.WriteLine("    -- Load all YMAPs");
                        luaWriter.WriteLine("    local ymapFiles = { 'stream/*.ymap' }");
                        luaWriter.WriteLine("    for _, ymapPattern in ipairs(ymapFiles) do");
                        luaWriter.WriteLine("        local files = GetStreamingFileForType(ymapPattern)");
                        luaWriter.WriteLine("        for _, file in ipairs(files) do");
                        luaWriter.WriteLine("            local success = RequestIpl(file)");
                        luaWriter.WriteLine("            if not success then");
                        luaWriter.WriteLine("                print('^1Failed to load YMAP: ' .. file .. '^7')");
                        luaWriter.WriteLine("            end");
                        luaWriter.WriteLine("        end");
                        luaWriter.WriteLine("    end");
                        luaWriter.WriteLine("end)");
                    }
                }
            }
        }

        private string GetTexturePath(uint hash)
        {
            if (texturePathCache.TryGetValue(hash, out string path))
            {
                return path;
            }

            foreach (var rpf in rpfManager.AllRpfs)
            {
                foreach (var entry in rpf.AllEntries)
                {
                    if (entry is RpfFileEntry rfe && rfe.Name.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var dict = RpfFile.GetFile<YtdFile>(rfe);
                            if (dict?.TextureDict?.TextureNameHashes?.data_items != null)
                            {
                                if (dict.TextureDict.TextureNameHashes.data_items.Contains(hash))
                                {
                                    path = rfe.Path;
                                    texturePathCache[hash] = path;
                                    return path;
                                }
                            }
                        }
                        catch (Exception) { /* Skip problematic YTD */ }
                    }
                }
            }

            return null;
        }

        private Texture FindTexture(uint hash)
        {
            foreach (var rpf in rpfManager.AllRpfs)
            {
                foreach (var entry in rpf.AllEntries)
                {
                    if (entry is RpfFileEntry rfe && rfe.Name.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var dict = RpfFile.GetFile<YtdFile>(rfe);
                            if (dict?.TextureDict != null)
                            {
                                var tex = dict.TextureDict.Lookup(hash);
                                if (tex != null) return tex;
                            }
                        }
                        catch (Exception) { /* Skip problematic YTD */ }
                    }
                }
            }

            return null;
        }

        private RpfFileEntry FindRpfEntry(string searchName, bool exactMatch = true)
        {
            foreach (var rpf in rpfManager.AllRpfs)
            {
                foreach (var entry in rpf.AllEntries)
                {
                    if (entry is RpfFileEntry rfe)
                    {
                        if (exactMatch)
                        {
                            if (rfe.Name.Equals(searchName, StringComparison.OrdinalIgnoreCase))
                                return rfe;
                        }
                        else
                        {
                            if (rfe.Name.Contains(searchName))
                                return rfe;
                        }
                    }
                }
            }
            return null;
        }

        private List<RpfFileEntry> FindAllRpfEntries(string extension)
        {
            var results = new List<RpfFileEntry>();
            foreach (var rpf in rpfManager.AllRpfs)
            {
                foreach (var entry in rpf.AllEntries)
                {
                    if (entry is RpfFileEntry rfe && rfe.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(rfe);
                    }
                }
            }
            return results;
        }

        private void ExtractRpfEntry(RpfFileEntry entry, string outputDir)
        {
            if (entry == null) return;

            string fileName = Path.GetFileName(entry.Path);
            string outputPath = Path.Combine(outputDir, fileName);

            if (!File.Exists(outputPath))
            {
                byte[] data = entry.File.ExtractFile(entry);
                if (data != null && data.Length > 0)
                {
                    File.WriteAllBytes(outputPath, data);
                }
            }
        }

        // Improve the YTD extraction to search more thoroughly
        private void ExtractYtdForModel(string baseName, string streamDir)
        {
            if (string.IsNullOrWhiteSpace(baseName)) return;
            
            // First check for exact match with YTD extension
            var ytdEntry = FindRpfEntry(baseName + ".ytd");
            if (ytdEntry != null)
            {
                ExtractRpfEntry(ytdEntry, streamDir);
            }

            // Sometimes YTDs are in parent folders with the same name
            var ytdParentEntry = FindRpfEntryWithParentName(baseName);
            if (ytdParentEntry != null)
            {
                ExtractRpfEntry(ytdParentEntry, streamDir);
            }

            // Try texture variations
            var variations = new[] {
                baseName + "_diff.ytd",
                baseName + "_1.ytd",
                baseName + "_2.ytd",
                baseName + "_hi.ytd",
                baseName + "_lod.ytd",
                baseName + "_n.ytd",
                baseName + "_spec.ytd"
            };

            foreach (var variation in variations)
            {
                var entry = FindRpfEntry(variation);
                if (entry != null)
                {
                    ExtractRpfEntry(entry, streamDir);
                }
            }

            // Check prefixed variants
            var prefixedVariants = new[] {
                "v_" + baseName + ".ytd",
                "veh_" + baseName + ".ytd",
                "prop_" + baseName + ".ytd",
                "hei_" + baseName + ".ytd"
            };

            foreach (var variant in prefixedVariants)
            {
                var entry = FindRpfEntry(variant);
                if (entry != null)
                {
                    ExtractRpfEntry(entry, streamDir);
                }
            }

            // Check texture dictionaries that might contain this texture
            var allYtds = FindAllRpfEntries(".ytd");
            foreach (var entry in allYtds)
            {
                try
                {
                    var ytdFile = RpfFile.GetFile<YtdFile>(entry);
                    if (ytdFile?.TextureDict?.Textures?.data_items != null)
                    {
                        bool containsTexture = false;
                        foreach (var texture in ytdFile.TextureDict.Textures.data_items)
                        {
                            string texName = texture?.Name?.ToString()?.ToLowerInvariant() ?? "";
                            if (!string.IsNullOrEmpty(texName) && 
                                (texName.Contains(baseName) || baseName.Contains(texName) || 
                                 LevenshteinDistance(texName, baseName) <= 3))  // Allow for minor differences
                            {
                                containsTexture = true;
                                break;
                            }
                        }

                        if (containsTexture)
                        {
                            ExtractRpfEntry(entry, streamDir);
                        }
                    }
                }
                catch (Exception) { /* Skip problematic YTD */ }
            }
        }

        // Find RpfEntry where the parent folder name matches the search term
        private RpfFileEntry FindRpfEntryWithParentName(string folderName)
        {
            foreach (var rpf in rpfManager.AllRpfs)
            {
                foreach (var entry in rpf.AllEntries)
                {
                    if (entry is RpfFileEntry rfe && rfe.Name.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] pathParts = rfe.Path.Split('\\', '/');
                        if (pathParts.Length >= 2)
                        {
                            string parentDir = pathParts[pathParts.Length - 2].ToLowerInvariant();
                            if (parentDir.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                            {
                                return rfe;
                            }
                        }
                    }
                }
            }
            return null;
        }

        // Calculate Levenshtein distance between two strings to find close matches
        private int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.IsNullOrEmpty(t) ? 0 : t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            int[] v0 = new int[t.Length + 1];
            int[] v1 = new int[t.Length + 1];

            for (int i = 0; i < v0.Length; i++)
            {
                v0[i] = i;
            }

            for (int i = 0; i < s.Length; i++)
            {
                v1[0] = i + 1;

                for (int j = 0; j < t.Length; j++)
                {
                    int cost = (s[i] == t[j]) ? 0 : 1;
                    v1[j + 1] = Math.Min(Math.Min(v1[j] + 1, v0[j + 1] + 1), v0[j] + cost);
                }

                for (int j = 0; j < v0.Length; j++)
                {
                    v0[j] = v1[j];
                }
            }

            return v1[t.Length];
        }

        // Helper method to extract YBN files with various naming patterns
        private void ExtractYbnForModel(string baseName, string streamDir)
        {
            // Try exact name match first
            var ybnEntry = FindRpfEntry(baseName + ".ybn");
            if (ybnEntry != null)
            {
                ExtractRpfEntry(ybnEntry, streamDir);
                return;
            }

            // Try hi/lo variants
            var hiEntry = FindRpfEntry(baseName + "_hi.ybn");
            if (hiEntry != null)
            {
                ExtractRpfEntry(hiEntry, streamDir);
            }

            var loEntry = FindRpfEntry(baseName + "_lod.ybn");
            if (loEntry != null)
            {
                ExtractRpfEntry(loEntry, streamDir);
            }

            // Try with different prefix/suffix patterns
            var patterns = new[] {
                "hi_" + baseName + ".ybn",
                "lo_" + baseName + ".ybn",
                baseName.Replace("_", "") + ".ybn"
            };

            foreach (var pattern in patterns)
            {
                var entry = FindRpfEntry(pattern);
                if (entry != null)
                {
                    ExtractRpfEntry(entry, streamDir);
                }
            }

            // Try substring matches if nothing found yet
            var ybnEntries = FindAllRpfEntries(".ybn");
            foreach (var entry in ybnEntries)
            {
                if (entry.Name.Contains(baseName) || baseName.Contains(entry.Name.Replace(".ybn", "")))
                {
                    ExtractRpfEntry(entry, streamDir);
                }
            }
        }
    }
} 