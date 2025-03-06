using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using CodeWalker.Utils;

namespace CodeWalker.Tools
{
    public partial class FiveMResourceForm : Form
    {
        private readonly RpfManager rpfManager;
        private readonly List<RpfEntry> selectedItems;

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
                                         !string.IsNullOrWhiteSpace(OutputDirectoryTextBox.Text) &&
                                         SelectedItemsListBox.SelectedItems.Count > 0;
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

            if (SelectedItemsListBox.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one model.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            string resourceName = ResourceNameTextBox.Text;
            string outputDir = Path.Combine(OutputDirectoryTextBox.Text, resourceName);
            string streamDir = Path.Combine(outputDir, "stream");

            // Create directories
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(streamDir);

            // Create manifest file
            CreateManifestFile(outputDir);

            // Extract selected models and their dependencies
            foreach (var item in selectedItems)
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

                    // If this is a YFT file, extract its dependencies
                    if (fileName.EndsWith(".yft", StringComparison.OrdinalIgnoreCase))
                    {
                        var yftFile = RpfFile.GetFile<YftFile>(item);
                        if (yftFile != null && yftFile.Fragment?.Drawable?.ShaderGroup?.TextureDictionary != null)
                        {
                            var textureDict = yftFile.Fragment.Drawable.ShaderGroup.TextureDictionary;
                            if (textureDict.Textures?.data_items != null)
                            {
                                foreach (var texture in textureDict.Textures.data_items)
                                {
                                    string textureName = texture.Name;
                                    var textureEntry = rpfManager.GetEntry(textureName);
                                    if (textureEntry is RpfFileEntry textureFileEntry)
                                    {
                                        string textureFileName = Path.GetFileName(textureFileEntry.Path);
                                        string textureOutputPath = Path.Combine(streamDir, textureFileName);
                                        byte[] textureData = textureFileEntry.File.ExtractFile(textureFileEntry);
                                        if (textureData != null)
                                        {
                                            File.WriteAllBytes(textureOutputPath, textureData);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            MessageBox.Show("FiveM resource created successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                writer.WriteLine("    'stream/*.yft',");
                writer.WriteLine("    'stream/*.ytd'");
                writer.WriteLine("}");
                writer.WriteLine();
                writer.WriteLine("data_file 'VEHICLE_FILE' 'stream/*.yft'");
            }
        }
    }
} 