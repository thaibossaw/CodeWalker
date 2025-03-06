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

                    // Extract embedded textures if any
                    if (ydrFile.Drawable.ShaderGroup?.TextureDictionary != null)
                    {
                        SafeExtractTextureDictionary(ydrFile.Drawable.ShaderGroup.TextureDictionary, streamDir, processedTextures);
                    }

                    // Extract shader-referenced textures
                    if (ydrFile.Drawable.ShaderGroup != null)
                    {
                        SafeExtractShaderTextures(ydrFile.Drawable.ShaderGroup, streamDir, processedTextures);
                    }

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
                                    if (archetype.Name.ToString().Equals(baseName, StringComparison.OrdinalIgnoreCase))
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
                    // First try exact name match
                    var ybnEntry = FindRpfEntry(baseName + ".ybn");
                    if (ybnEntry != null)
                    {
                        ExtractRpfEntry(ybnEntry, streamDir);
                    }
                    else
                    {
                        // If no exact match, try finding YBNs that contain the base name
                        var ybnEntries = FindAllRpfEntries(".ybn");
                        foreach (var entry in ybnEntries)
                        {
                            if (entry.Name.Contains(baseName))
                            {
                                ExtractRpfEntry(entry, streamDir);
                            }
                        }
                    }

                    // Find and extract YTD files
                    // First check for exact name match
                    var ytdEntry = FindRpfEntry(baseName + ".ytd");
                    if (ytdEntry != null)
                    {
                        ExtractRpfEntry(ytdEntry, streamDir);
                    }

                    // Then check shared texture dictionaries
                    var sharedYtds = new[] { "vehshare.ytd", "generic.ytd", "generic_textures.ytd" };
                    foreach (var sharedYtd in sharedYtds)
                    {
                        var sharedEntry = FindRpfEntry(sharedYtd);
                        if (sharedEntry != null)
                        {
                            ExtractRpfEntry(sharedEntry, streamDir);
                        }
                    }

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

                                // Check for exact matches
                                containsModel = ymapFile.AllEntities.Any(e => 
                                    e._CEntityDef.archetypeName.Hash == JenkHash.GenHash(baseName));

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

        private void ExtractYftDependencies(RpfEntry item, string streamDir, HashSet<uint> processedTextures)
        {
            try
            {
                var yftFile = RpfFile.GetFile<YftFile>(item);
                if (yftFile?.Fragment?.Drawable != null)
                {
                    // Extract embedded textures if any
                    if (yftFile.Fragment.Drawable.ShaderGroup?.TextureDictionary != null)
                    {
                        SafeExtractTextureDictionary(yftFile.Fragment.Drawable.ShaderGroup.TextureDictionary, streamDir, processedTextures);
                    }

                    // Extract shader-referenced textures
                    if (yftFile.Fragment.Drawable.ShaderGroup != null)
                    {
                        SafeExtractShaderTextures(yftFile.Fragment.Drawable.ShaderGroup, streamDir, processedTextures);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error extracting YFT dependencies for {item.Path}: {ex.Message}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SafeExtractShaderTextures(ShaderGroup shaderGroup, string outputDir, HashSet<uint> processedTextures)
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
                                var texture = FindTexture(texBase.NameHash);
                                if (texture != null)
                                {
                                    SafeExtractTexture(texture, outputDir, processedTextures);
                                }
                            }
                        }
                        catch (Exception) { /* Skip problematic parameter */ }
                    }
                }
                catch (Exception) { /* Skip problematic shader */ }
            }
        }

        private void SafeExtractTextureDictionary(TextureDictionary textureDict, string outputDir, HashSet<uint> processedTextures)
        {
            if (textureDict?.Textures?.data_items == null) return;

            foreach (var texture in textureDict.Textures.data_items)
            {
                try
                {
                    if (texture != null)
                    {
                        SafeExtractTexture(texture, outputDir, processedTextures);
                    }
                }
                catch (Exception) { /* Skip problematic texture */ }
            }
        }

        private void SafeExtractTexture(Texture texture, string outputDir, HashSet<uint> processedTextures)
        {
            if (texture == null || processedTextures.Contains(texture.NameHash)) return;

            try
            {
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

            // Search in all RPF files for the texture
            foreach (var rpf in rpfManager.AllRpfs)
            {
                foreach (var entry in rpf.AllEntries)
                {
                    if (entry is RpfFileEntry rfe && rfe.Name.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase))
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
                }
            }

            // Check shared textures
            var sharedYtd = rpfManager.GetEntry("vehshare.ytd") as RpfFileEntry;
            if (sharedYtd != null)
            {
                var sharedDict = RpfFile.GetFile<YtdFile>(sharedYtd);
                if (sharedDict?.TextureDict?.TextureNameHashes?.data_items != null)
                {
                    if (sharedDict.TextureDict.TextureNameHashes.data_items.Contains(hash))
                    {
                        path = sharedYtd.Path;
                        texturePathCache[hash] = path;
                        return path;
                    }
                }
            }

            return null;
        }

        private Texture FindTexture(uint hash)
        {
            // Try to find the texture in any available dictionary
            foreach (var rpf in rpfManager.AllRpfs)
            {
                foreach (var entry in rpf.AllEntries)
                {
                    if (entry is RpfFileEntry rfe && rfe.Name.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase))
                    {
                        var dict = RpfFile.GetFile<YtdFile>(rfe);
                        if (dict?.TextureDict != null)
                        {
                            var tex = dict.TextureDict.Lookup(hash);
                            if (tex != null) return tex;
                        }
                    }
                }
            }

            // Try to find the texture in shared dictionaries
            var sharedYtd = rpfManager.GetEntry("vehshare.ytd") as RpfFileEntry;
            if (sharedYtd != null)
            {
                var sharedDict = RpfFile.GetFile<YtdFile>(sharedYtd);
                if (sharedDict?.TextureDict != null)
                {
                    var tex = sharedDict.TextureDict.Lookup(hash);
                    if (tex != null) return tex;
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
    }
} 