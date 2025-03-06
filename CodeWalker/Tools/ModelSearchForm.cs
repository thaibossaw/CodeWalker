using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeWalker.GameFiles;

namespace CodeWalker.Tools
{
    public partial class ModelSearchForm : Form
    {
        private readonly RpfManager rpfManager;
        private readonly HashSet<RpfEntry> selectedModels;
        private readonly ListView searchResultsListView;
        private readonly TextBox searchBox;

        public ModelSearchForm(RpfManager rpfManager)
        {
            InitializeComponent();
            this.rpfManager = rpfManager;
            this.selectedModels = new HashSet<RpfEntry>();

            // Create search results list view
            searchResultsListView = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                Location = new System.Drawing.Point(12, 41),
                Size = new System.Drawing.Size(460, 300),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            searchResultsListView.Columns.Add("Model", 300);
            searchResultsListView.Columns.Add("RPF File", 150);
            searchResultsListView.ItemChecked += SearchResultsListView_ItemChecked;
            Controls.Add(searchResultsListView);

            // Create search box
            searchBox = new TextBox
            {
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(379, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(searchBox);

            // Create search button
            var searchButton = new Button
            {
                Text = "Search",
                Location = new System.Drawing.Point(397, 10),
                Size = new System.Drawing.Size(75, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            searchButton.Click += (s, e) => SearchModels();
            Controls.Add(searchButton);

            // Create next button
            var nextButton = new Button
            {
                Text = "Next",
                Location = new System.Drawing.Point(397, 347),
                Size = new System.Drawing.Size(75, 23),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Enabled = false
            };
            nextButton.Click += NextButton_Click;
            Controls.Add(nextButton);

            // Create selected count label
            var selectedCountLabel = new Label
            {
                Location = new System.Drawing.Point(12, 352),
                Size = new System.Drawing.Size(379, 13),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Text = "Selected: 0"
            };
            Controls.Add(selectedCountLabel);

            // Set form properties
            Text = "Search Models";
            Size = new System.Drawing.Size(500, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
        }

        private void SearchModels()
        {
            searchResultsListView.Items.Clear();
            var searchText = searchBox.Text.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                UpdateNextButton();
                return;
            }

            // Search only for .ydr files
            var results = rpfManager.FindFiles(searchText, ".ydr");

            foreach (var entry in results)
            {
                var item = new ListViewItem(entry.Name);
                item.SubItems.Add(entry.File?.Path ?? "Unknown RPF");
                item.Tag = entry;
                item.Checked = selectedModels.Contains(entry);
                searchResultsListView.Items.Add(item);
            }

            UpdateNextButton();
        }

        private void SearchResultsListView_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (e.Item.Tag is RpfEntry entry)
            {
                if (e.Item.Checked)
                {
                    // When checking a .ydr file, add it and its associated files
                    selectedModels.Add(entry);
                    AddAssociatedFiles(entry);
                }
                else
                {
                    // When unchecking, remove the .ydr file
                    selectedModels.Remove(entry);
                }

                UpdateNextButton();
            }
        }

        private void AddAssociatedFiles(RpfEntry ydrEntry)
        {
            if (!(ydrEntry is RpfFileEntry fileEntry)) return;

            var baseName = fileEntry.NameLower.Substring(0, fileEntry.NameLower.Length - 4);
            var rpfFile = fileEntry.File;

            // Look for associated files in the same RPF
            if (rpfFile.AllEntries != null)
            {
                foreach (var entry in rpfFile.AllEntries)
                {
                    if (!(entry is RpfFileEntry fe)) continue;

                    var name = fe.NameLower;
                    if (name.StartsWith(baseName))
                    {
                        // Add texture dictionary if it exists
                        if (name.EndsWith(".ytd"))
                        {
                            selectedModels.Add(entry);
                        }
                        // Add collision file if it exists
                        else if (name.EndsWith(".ybn"))
                        {
                            selectedModels.Add(entry);
                        }
                        // Add LOD files if they exist
                        else if (name.EndsWith("_lod.ydr") || 
                                name.EndsWith("_loda.ydr") || 
                                name.EndsWith("_lodb.ydr"))
                        {
                            selectedModels.Add(entry);
                        }
                    }
                }
            }
        }

        private void UpdateNextButton()
        {
            var nextButton = Controls.OfType<Button>().First(b => b.Text == "Next");
            nextButton.Enabled = selectedModels.Count > 0;

            var selectedCountLabel = Controls.OfType<Label>().First(l => l.Text.StartsWith("Selected:"));
            selectedCountLabel.Text = $"Selected: {selectedModels.Count}";
        }

        private void NextButton_Click(object sender, EventArgs e)
        {
            using (var form = new FiveMResourceForm(rpfManager, selectedModels.ToList()))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }
    }
} 