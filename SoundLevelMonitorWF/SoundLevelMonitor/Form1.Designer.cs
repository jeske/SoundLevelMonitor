namespace SoundLevelMonitor
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.audioLevelsControl = new SoundLevelMonitor.AudioLevelsUIControl();
            this.SuspendLayout();
            // 
            // audioLevelsControl
            // 
            this.audioLevelsControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.audioLevelsControl.Location = new System.Drawing.Point(12, 12);
            this.audioLevelsControl.Margin = new System.Windows.Forms.Padding(0);
            this.audioLevelsControl.Name = "audioLevelsControl";
            this.audioLevelsControl.Size = new System.Drawing.Size(263, 240);
            this.audioLevelsControl.TabIndex = 0;
            this.audioLevelsControl.Text = "audioLevelsUIControl1";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Controls.Add(this.audioLevelsControl);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);

        }

        #endregion

        private AudioLevelsUIControl audioLevelsControl;
    }
}

