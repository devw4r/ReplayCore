namespace SniffBrowser.Controls
{
    partial class ObjectSelectionDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ObjectSelectionDlg));
            this.availableObjectsListView = new BrightIdeasSoftware.FastObjectListView();
            this.button1 = new System.Windows.Forms.Button();
            this.PnlBottom = new System.Windows.Forms.Panel();
            this.TxtFilter = new System.Windows.Forms.TextBox();
            this.PBoxFilter = new System.Windows.Forms.PictureBox();
            this.PnlList = new System.Windows.Forms.Panel();
            this.ChkBoxByObjectType = new System.Windows.Forms.CheckBox();
            this.CBoxObjectTypes = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.availableObjectsListView)).BeginInit();
            this.PnlBottom.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PBoxFilter)).BeginInit();
            this.PnlList.SuspendLayout();
            this.SuspendLayout();
            // 
            // availableObjectsListView
            // 
            this.availableObjectsListView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.availableObjectsListView.CellEditUseWholeCell = false;
            this.availableObjectsListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.availableObjectsListView.GridLines = true;
            this.availableObjectsListView.HideSelection = false;
            this.availableObjectsListView.Location = new System.Drawing.Point(0, 20);
            this.availableObjectsListView.Name = "availableObjectsListView";
            this.availableObjectsListView.ShowGroups = false;
            this.availableObjectsListView.Size = new System.Drawing.Size(584, 243);
            this.availableObjectsListView.TabIndex = 0;
            this.availableObjectsListView.UseCompatibleStateImageBehavior = false;
            this.availableObjectsListView.View = System.Windows.Forms.View.Details;
            this.availableObjectsListView.VirtualMode = true;
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.Location = new System.Drawing.Point(523, 4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "Ok";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.Button1_Click);
            // 
            // PnlBottom
            // 
            this.PnlBottom.Controls.Add(this.button1);
            this.PnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.PnlBottom.Location = new System.Drawing.Point(0, 358);
            this.PnlBottom.Name = "PnlBottom";
            this.PnlBottom.Size = new System.Drawing.Size(610, 34);
            this.PnlBottom.TabIndex = 2;
            // 
            // TxtFilter
            // 
            this.TxtFilter.BackColor = System.Drawing.Color.AliceBlue;
            this.TxtFilter.Dock = System.Windows.Forms.DockStyle.Top;
            this.TxtFilter.Location = new System.Drawing.Point(0, 0);
            this.TxtFilter.Name = "TxtFilter";
            this.TxtFilter.Size = new System.Drawing.Size(584, 20);
            this.TxtFilter.TabIndex = 3;
            this.TxtFilter.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.TxtFilter.TextChanged += new System.EventHandler(this.TxtFilter_TextChanged);
            // 
            // PBoxFilter
            // 
            this.PBoxFilter.BackColor = System.Drawing.Color.AliceBlue;
            this.PBoxFilter.Image = global::SniffBrowser.Properties.Resources._8666693_search_icon;
            this.PBoxFilter.Location = new System.Drawing.Point(2, 2);
            this.PBoxFilter.Name = "PBoxFilter";
            this.PBoxFilter.Size = new System.Drawing.Size(16, 16);
            this.PBoxFilter.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.PBoxFilter.TabIndex = 29;
            this.PBoxFilter.TabStop = false;
            // 
            // PnlList
            // 
            this.PnlList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.PnlList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.PnlList.Controls.Add(this.availableObjectsListView);
            this.PnlList.Controls.Add(this.PBoxFilter);
            this.PnlList.Controls.Add(this.TxtFilter);
            this.PnlList.Location = new System.Drawing.Point(12, 12);
            this.PnlList.Name = "PnlList";
            this.PnlList.Size = new System.Drawing.Size(586, 265);
            this.PnlList.TabIndex = 30;
            // 
            // ChkBoxByObjectType
            // 
            this.ChkBoxByObjectType.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.ChkBoxByObjectType.AutoSize = true;
            this.ChkBoxByObjectType.Location = new System.Drawing.Point(12, 289);
            this.ChkBoxByObjectType.Name = "ChkBoxByObjectType";
            this.ChkBoxByObjectType.Size = new System.Drawing.Size(123, 17);
            this.ChkBoxByObjectType.TabIndex = 31;
            this.ChkBoxByObjectType.Text = "Only By Object Type";
            this.ChkBoxByObjectType.UseVisualStyleBackColor = true;
            this.ChkBoxByObjectType.CheckedChanged += new System.EventHandler(this.ChkBoxByObjectType_CheckedChanged);
            // 
            // CBoxObjectTypes
            // 
            this.CBoxObjectTypes.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.CBoxObjectTypes.FormattingEnabled = true;
            this.CBoxObjectTypes.Location = new System.Drawing.Point(12, 326);
            this.CBoxObjectTypes.Name = "CBoxObjectTypes";
            this.CBoxObjectTypes.Size = new System.Drawing.Size(121, 21);
            this.CBoxObjectTypes.TabIndex = 32;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.label1.Location = new System.Drawing.Point(35, 309);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(100, 13);
            this.label1.TabIndex = 33;
            this.label1.Text = "* (Source or Target)";
            // 
            // ObjectSelectionDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(610, 392);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.CBoxObjectTypes);
            this.Controls.Add(this.ChkBoxByObjectType);
            this.Controls.Add(this.PnlList);
            this.Controls.Add(this.PnlBottom);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ObjectSelectionDlg";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Object Filter";
            ((System.ComponentModel.ISupportInitialize)(this.availableObjectsListView)).EndInit();
            this.PnlBottom.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.PBoxFilter)).EndInit();
            this.PnlList.ResumeLayout(false);
            this.PnlList.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private BrightIdeasSoftware.FastObjectListView availableObjectsListView;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Panel PnlBottom;
        private System.Windows.Forms.TextBox TxtFilter;
        private System.Windows.Forms.PictureBox PBoxFilter;
        private System.Windows.Forms.Panel PnlList;
        private System.Windows.Forms.CheckBox ChkBoxByObjectType;
        private System.Windows.Forms.ComboBox CBoxObjectTypes;
        private System.Windows.Forms.Label label1;
    }
}