namespace SampleApp
{
    partial class frmMain
    {
        /// <summary>
        /// Variable nécessaire au concepteur.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Nettoyage des ressources utilisées.
        /// </summary>
        /// <param name="disposing">true si les ressources managées doivent être supprimées ; sinon, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Code généré par le Concepteur Windows Form

        /// <summary>
        /// Méthode requise pour la prise en charge du concepteur - ne modifiez pas
        /// le contenu de cette méthode avec l'éditeur de code.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
            this.toolbarBackground = new System.Windows.Forms.Panel();
            this.forwardButton = new System.Windows.Forms.PictureBox();
            this.backButton = new System.Windows.Forms.PictureBox();
            this.urlTextBox = new System.Windows.Forms.TextBox();
            this.urlBorder = new System.Windows.Forms.Panel();
            this.urlBackground = new System.Windows.Forms.Panel();
            this.webBrowser = new System.Windows.Forms.WebBrowser();
            this.toolbarBackground.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.forwardButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.backButton)).BeginInit();
            this.urlBorder.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolbarBackground
            // 
            this.toolbarBackground.Controls.Add(this.forwardButton);
            this.toolbarBackground.Controls.Add(this.backButton);
            this.toolbarBackground.Controls.Add(this.urlTextBox);
            this.toolbarBackground.Controls.Add(this.urlBorder);
            this.toolbarBackground.Dock = System.Windows.Forms.DockStyle.Top;
            this.toolbarBackground.Location = new System.Drawing.Point(0, 0);
            this.toolbarBackground.Name = "toolbarBackground";
            this.toolbarBackground.Size = new System.Drawing.Size(800, 36);
            this.toolbarBackground.TabIndex = 3;
            // 
            // forwardButton
            // 
            this.forwardButton.BackColor = System.Drawing.Color.Transparent;
            this.forwardButton.Image = global::SampleApp.Properties.Resources.ForwardActive;
            this.forwardButton.Location = new System.Drawing.Point(37, 5);
            this.forwardButton.Margin = new System.Windows.Forms.Padding(4, 4, 3, 3);
            this.forwardButton.Name = "forwardButton";
            this.forwardButton.Size = new System.Drawing.Size(27, 27);
            this.forwardButton.TabIndex = 3;
            this.forwardButton.TabStop = false;
            // 
            // backButton
            // 
            this.backButton.BackColor = System.Drawing.Color.Transparent;
            this.backButton.Image = global::SampleApp.Properties.Resources.BackActive;
            this.backButton.Location = new System.Drawing.Point(6, 5);
            this.backButton.Margin = new System.Windows.Forms.Padding(4, 4, 3, 3);
            this.backButton.Name = "backButton";
            this.backButton.Size = new System.Drawing.Size(27, 27);
            this.backButton.TabIndex = 2;
            this.backButton.TabStop = false;
            // 
            // urlTextBox
            // 
            this.urlTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.urlTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.urlTextBox.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.urlTextBox.Location = new System.Drawing.Point(79, 8);
            this.urlTextBox.Margin = new System.Windows.Forms.Padding(9);
            this.urlTextBox.Name = "urlTextBox";
            this.urlTextBox.Size = new System.Drawing.Size(705, 19);
            this.urlTextBox.TabIndex = 0;
            this.urlTextBox.Text = "about:blank";
            this.urlTextBox.WordWrap = false;
            // 
            // urlBorder
            // 
            this.urlBorder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.urlBorder.BackColor = System.Drawing.Color.Silver;
            this.urlBorder.Controls.Add(this.urlBackground);
            this.urlBorder.ForeColor = System.Drawing.Color.Silver;
            this.urlBorder.Location = new System.Drawing.Point(69, 5);
            this.urlBorder.Name = "urlBorder";
            this.urlBorder.Size = new System.Drawing.Size(727, 26);
            this.urlBorder.TabIndex = 1;
            // 
            // urlBackground
            // 
            this.urlBackground.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.urlBackground.BackColor = System.Drawing.Color.White;
            this.urlBackground.ForeColor = System.Drawing.Color.Silver;
            this.urlBackground.Location = new System.Drawing.Point(1, 1);
            this.urlBackground.Name = "urlBackground";
            this.urlBackground.Size = new System.Drawing.Size(725, 24);
            this.urlBackground.TabIndex = 2;
            // 
            // webBrowser
            // 
            this.webBrowser.Dock = System.Windows.Forms.DockStyle.Fill;
            this.webBrowser.Location = new System.Drawing.Point(0, 36);
            this.webBrowser.MinimumSize = new System.Drawing.Size(20, 20);
            this.webBrowser.Name = "webBrowser";
            this.webBrowser.ScriptErrorsSuppressed = true;
            this.webBrowser.Size = new System.Drawing.Size(800, 414);
            this.webBrowser.TabIndex = 7;
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.webBrowser);
            this.Controls.Add(this.toolbarBackground);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmMain";
            this.Text = "Sample App";
            this.toolbarBackground.ResumeLayout(false);
            this.toolbarBackground.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.forwardButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.backButton)).EndInit();
            this.urlBorder.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel toolbarBackground;
        private System.Windows.Forms.PictureBox forwardButton;
        private System.Windows.Forms.PictureBox backButton;
        private System.Windows.Forms.TextBox urlTextBox;
        private System.Windows.Forms.Panel urlBorder;
        private System.Windows.Forms.Panel urlBackground;
        private System.Windows.Forms.WebBrowser webBrowser;
    }
}

