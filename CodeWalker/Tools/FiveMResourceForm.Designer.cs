namespace CodeWalker.Tools
{
    partial class FiveMResourceForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FiveMResourceForm));
            this.SelectedItemsListBox = new System.Windows.Forms.ListBox();
            this.ResourceNameLabel = new System.Windows.Forms.Label();
            this.ResourceNameTextBox = new System.Windows.Forms.TextBox();
            this.OutputDirectoryLabel = new System.Windows.Forms.Label();
            this.OutputDirectoryTextBox = new System.Windows.Forms.TextBox();
            this.OutputDirectoryButton = new System.Windows.Forms.Button();
            this.CreateResourceButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // SelectedItemsListBox
            // 
            this.SelectedItemsListBox.FormattingEnabled = true;
            this.SelectedItemsListBox.Location = new System.Drawing.Point(12, 12);
            this.SelectedItemsListBox.Name = "SelectedItemsListBox";
            this.SelectedItemsListBox.SelectionMode = System.Windows.Forms.SelectionMode.MultiSimple;
            this.SelectedItemsListBox.Size = new System.Drawing.Size(460, 160);
            this.SelectedItemsListBox.TabIndex = 0;
            // 
            // ResourceNameLabel
            // 
            this.ResourceNameLabel.AutoSize = true;
            this.ResourceNameLabel.Location = new System.Drawing.Point(12, 185);
            this.ResourceNameLabel.Name = "ResourceNameLabel";
            this.ResourceNameLabel.Size = new System.Drawing.Size(87, 13);
            this.ResourceNameLabel.TabIndex = 1;
            this.ResourceNameLabel.Text = "Resource Name:";
            // 
            // ResourceNameTextBox
            // 
            this.ResourceNameTextBox.Location = new System.Drawing.Point(12, 201);
            this.ResourceNameTextBox.Name = "ResourceNameTextBox";
            this.ResourceNameTextBox.Size = new System.Drawing.Size(460, 20);
            this.ResourceNameTextBox.TabIndex = 2;
            this.ResourceNameTextBox.TextChanged += new System.EventHandler(this.ResourceNameTextBox_TextChanged);
            // 
            // OutputDirectoryLabel
            // 
            this.OutputDirectoryLabel.AutoSize = true;
            this.OutputDirectoryLabel.Location = new System.Drawing.Point(12, 224);
            this.OutputDirectoryLabel.Name = "OutputDirectoryLabel";
            this.OutputDirectoryLabel.Size = new System.Drawing.Size(89, 13);
            this.OutputDirectoryLabel.TabIndex = 3;
            this.OutputDirectoryLabel.Text = "Output Directory:";
            // 
            // OutputDirectoryTextBox
            // 
            this.OutputDirectoryTextBox.Location = new System.Drawing.Point(12, 240);
            this.OutputDirectoryTextBox.Name = "OutputDirectoryTextBox";
            this.OutputDirectoryTextBox.Size = new System.Drawing.Size(379, 20);
            this.OutputDirectoryTextBox.TabIndex = 4;
            this.OutputDirectoryTextBox.TextChanged += new System.EventHandler(this.ResourceNameTextBox_TextChanged);
            // 
            // OutputDirectoryButton
            // 
            this.OutputDirectoryButton.Location = new System.Drawing.Point(397, 238);
            this.OutputDirectoryButton.Name = "OutputDirectoryButton";
            this.OutputDirectoryButton.Size = new System.Drawing.Size(75, 23);
            this.OutputDirectoryButton.TabIndex = 5;
            this.OutputDirectoryButton.Text = "Browse...";
            this.OutputDirectoryButton.UseVisualStyleBackColor = true;
            this.OutputDirectoryButton.Click += new System.EventHandler(this.OutputDirectoryButton_Click);
            // 
            // CreateResourceButton
            // 
            this.CreateResourceButton.Enabled = false;
            this.CreateResourceButton.Location = new System.Drawing.Point(12, 266);
            this.CreateResourceButton.Name = "CreateResourceButton";
            this.CreateResourceButton.Size = new System.Drawing.Size(460, 30);
            this.CreateResourceButton.TabIndex = 6;
            this.CreateResourceButton.Text = "Create FiveM Resource";
            this.CreateResourceButton.UseVisualStyleBackColor = true;
            this.CreateResourceButton.Click += new System.EventHandler(this.CreateResourceButton_Click);
            // 
            // FiveMResourceForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 308);
            this.Controls.Add(this.CreateResourceButton);
            this.Controls.Add(this.OutputDirectoryButton);
            this.Controls.Add(this.OutputDirectoryTextBox);
            this.Controls.Add(this.OutputDirectoryLabel);
            this.Controls.Add(this.ResourceNameTextBox);
            this.Controls.Add(this.ResourceNameLabel);
            this.Controls.Add(this.SelectedItemsListBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FiveMResourceForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Create FiveM Resource";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ListBox SelectedItemsListBox;
        private System.Windows.Forms.Label ResourceNameLabel;
        private System.Windows.Forms.TextBox ResourceNameTextBox;
        private System.Windows.Forms.Label OutputDirectoryLabel;
        private System.Windows.Forms.TextBox OutputDirectoryTextBox;
        private System.Windows.Forms.Button OutputDirectoryButton;
        private System.Windows.Forms.Button CreateResourceButton;
    }
} 