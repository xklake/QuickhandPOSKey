namespace LicenseGenerator
{
    partial class License
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
            this.label1 = new System.Windows.Forms.Label();
            this.m_txtAppFactor = new System.Windows.Forms.TextBox();
            this.m_Factor = new System.Windows.Forms.Label();
            this.m_Generator = new System.Windows.Forms.Button();
            this.m_LicenseCode = new System.Windows.Forms.TextBox();
            this.lable5 = new System.Windows.Forms.Label();
            this.m_txtHardware = new System.Windows.Forms.TextBox();
            this.m_GenerateFile = new System.Windows.Forms.Button();
            this.m_HashCode = new System.Windows.Forms.TextBox();
            this.lable = new System.Windows.Forms.Label();
            this.m_ChkTrial = new System.Windows.Forms.CheckBox();
            this.m_txbDeviceId = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.m_ChkWeb = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.m_DataPickTime = new System.Windows.Forms.DateTimePicker();
            this.label4 = new System.Windows.Forms.Label();
            this.m_ChKFullAccess = new System.Windows.Forms.CheckBox();
            this.m_PaymentExpiryDate = new System.Windows.Forms.DateTimePicker();
            this.label5 = new System.Windows.Forms.Label();
            this.m_chbPaymentActiviated = new System.Windows.Forms.CheckBox();
            this.m_txtTerminalsAmount = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(64, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(83, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "Hardware Info";
            // 
            // m_txtAppFactor
            // 
            this.m_txtAppFactor.Location = new System.Drawing.Point(165, 96);
            this.m_txtAppFactor.Name = "m_txtAppFactor";
            this.m_txtAppFactor.Size = new System.Drawing.Size(241, 21);
            this.m_txtAppFactor.TabIndex = 7;
            this.m_txtAppFactor.Text = "dc.quickhandpos.com";
            // 
            // m_Factor
            // 
            this.m_Factor.AutoSize = true;
            this.m_Factor.Location = new System.Drawing.Point(64, 98);
            this.m_Factor.Name = "m_Factor";
            this.m_Factor.Size = new System.Drawing.Size(65, 12);
            this.m_Factor.TabIndex = 6;
            this.m_Factor.Text = "App Factor";
            // 
            // m_Generator
            // 
            this.m_Generator.Location = new System.Drawing.Point(0, 507);
            this.m_Generator.Name = "m_Generator";
            this.m_Generator.Size = new System.Drawing.Size(55, 10);
            this.m_Generator.TabIndex = 8;
            this.m_Generator.Text = "Generate Code";
            this.m_Generator.UseVisualStyleBackColor = true;
            this.m_Generator.Visible = false;
            this.m_Generator.Click += new System.EventHandler(this.m_Generator_Click);
            // 
            // m_LicenseCode
            // 
            this.m_LicenseCode.Location = new System.Drawing.Point(165, 130);
            this.m_LicenseCode.Name = "m_LicenseCode";
            this.m_LicenseCode.Size = new System.Drawing.Size(241, 21);
            this.m_LicenseCode.TabIndex = 10;
            // 
            // lable5
            // 
            this.lable5.AutoSize = true;
            this.lable5.Location = new System.Drawing.Point(64, 135);
            this.lable5.Name = "lable5";
            this.lable5.Size = new System.Drawing.Size(77, 12);
            this.lable5.TabIndex = 9;
            this.lable5.Text = "License Code";
            // 
            // m_txtHardware
            // 
            this.m_txtHardware.Location = new System.Drawing.Point(165, 27);
            this.m_txtHardware.Name = "m_txtHardware";
            this.m_txtHardware.Size = new System.Drawing.Size(241, 21);
            this.m_txtHardware.TabIndex = 11;
            // 
            // m_GenerateFile
            // 
            this.m_GenerateFile.Location = new System.Drawing.Point(201, 399);
            this.m_GenerateFile.Name = "m_GenerateFile";
            this.m_GenerateFile.Size = new System.Drawing.Size(133, 34);
            this.m_GenerateFile.TabIndex = 12;
            this.m_GenerateFile.Text = "Generate Key File";
            this.m_GenerateFile.UseVisualStyleBackColor = true;
            this.m_GenerateFile.Click += new System.EventHandler(this.m_GenerateFile_Click);
            // 
            // m_HashCode
            // 
            this.m_HashCode.Location = new System.Drawing.Point(165, 62);
            this.m_HashCode.Name = "m_HashCode";
            this.m_HashCode.Size = new System.Drawing.Size(241, 21);
            this.m_HashCode.TabIndex = 5;
            // 
            // lable
            // 
            this.lable.AutoSize = true;
            this.lable.Location = new System.Drawing.Point(64, 64);
            this.lable.Name = "lable";
            this.lable.Size = new System.Drawing.Size(53, 12);
            this.lable.TabIndex = 4;
            this.lable.Text = "HashCode";
            // 
            // m_ChkTrial
            // 
            this.m_ChkTrial.AutoSize = true;
            this.m_ChkTrial.Location = new System.Drawing.Point(412, 133);
            this.m_ChkTrial.Name = "m_ChkTrial";
            this.m_ChkTrial.Size = new System.Drawing.Size(54, 16);
            this.m_ChkTrial.TabIndex = 13;
            this.m_ChkTrial.Text = "Trial";
            this.m_ChkTrial.UseVisualStyleBackColor = true;
            // 
            // m_txbDeviceId
            // 
            this.m_txbDeviceId.Location = new System.Drawing.Point(165, 169);
            this.m_txbDeviceId.Name = "m_txbDeviceId";
            this.m_txbDeviceId.Size = new System.Drawing.Size(241, 21);
            this.m_txbDeviceId.TabIndex = 15;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(64, 172);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(101, 12);
            this.label2.TabIndex = 14;
            this.label2.Text = "Mobile Device ID";
            // 
            // m_ChkWeb
            // 
            this.m_ChkWeb.AutoSize = true;
            this.m_ChkWeb.Location = new System.Drawing.Point(166, 244);
            this.m_ChkWeb.Name = "m_ChkWeb";
            this.m_ChkWeb.Size = new System.Drawing.Size(15, 14);
            this.m_ChkWeb.TabIndex = 16;
            this.m_ChkWeb.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(64, 211);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(95, 12);
            this.label3.TabIndex = 17;
            this.label3.Text = "App Expiry Date";
            // 
            // m_DataPickTime
            // 
            this.m_DataPickTime.Location = new System.Drawing.Point(166, 205);
            this.m_DataPickTime.Name = "m_DataPickTime";
            this.m_DataPickTime.Size = new System.Drawing.Size(240, 21);
            this.m_DataPickTime.TabIndex = 19;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(64, 244);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(65, 12);
            this.label4.TabIndex = 20;
            this.label4.Text = "Web Access";
            // 
            // m_ChKFullAccess
            // 
            this.m_ChKFullAccess.AutoSize = true;
            this.m_ChKFullAccess.Location = new System.Drawing.Point(412, 172);
            this.m_ChKFullAccess.Name = "m_ChKFullAccess";
            this.m_ChKFullAccess.Size = new System.Drawing.Size(48, 16);
            this.m_ChKFullAccess.TabIndex = 21;
            this.m_ChKFullAccess.Text = "Full";
            this.m_ChKFullAccess.UseVisualStyleBackColor = true;
            // 
            // m_PaymentExpiryDate
            // 
            this.m_PaymentExpiryDate.Location = new System.Drawing.Point(195, 329);
            this.m_PaymentExpiryDate.Name = "m_PaymentExpiryDate";
            this.m_PaymentExpiryDate.Size = new System.Drawing.Size(211, 21);
            this.m_PaymentExpiryDate.TabIndex = 23;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(64, 335);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(125, 12);
            this.label5.TabIndex = 22;
            this.label5.Text = "Payment  Expiry Date";
            // 
            // m_chbPaymentActiviated
            // 
            this.m_chbPaymentActiviated.AutoSize = true;
            this.m_chbPaymentActiviated.Location = new System.Drawing.Point(412, 296);
            this.m_chbPaymentActiviated.Name = "m_chbPaymentActiviated";
            this.m_chbPaymentActiviated.Size = new System.Drawing.Size(66, 16);
            this.m_chbPaymentActiviated.TabIndex = 26;
            this.m_chbPaymentActiviated.Text = "Enabled";
            this.m_chbPaymentActiviated.UseVisualStyleBackColor = true;
            // 
            // m_txtTerminalsAmount
            // 
            this.m_txtTerminalsAmount.Location = new System.Drawing.Point(165, 293);
            this.m_txtTerminalsAmount.Name = "m_txtTerminalsAmount";
            this.m_txtTerminalsAmount.Size = new System.Drawing.Size(241, 21);
            this.m_txtTerminalsAmount.TabIndex = 25;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(64, 296);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(95, 12);
            this.label6.TabIndex = 24;
            this.label6.Text = "Terminal Amount";
            // 
            // License
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(528, 454);
            this.Controls.Add(this.m_chbPaymentActiviated);
            this.Controls.Add(this.m_txtTerminalsAmount);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.m_PaymentExpiryDate);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.m_ChKFullAccess);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.m_DataPickTime);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.m_ChkWeb);
            this.Controls.Add(this.m_txbDeviceId);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.m_ChkTrial);
            this.Controls.Add(this.m_GenerateFile);
            this.Controls.Add(this.m_txtHardware);
            this.Controls.Add(this.m_LicenseCode);
            this.Controls.Add(this.lable5);
            this.Controls.Add(this.m_Generator);
            this.Controls.Add(this.m_txtAppFactor);
            this.Controls.Add(this.m_Factor);
            this.Controls.Add(this.m_HashCode);
            this.Controls.Add(this.lable);
            this.Controls.Add(this.label1);
            this.Name = "License";
            this.StartPosition = System.Windows.Forms.FormStartPosition.WindowsDefaultBounds;
            this.Text = "License";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox m_txtAppFactor;
        private System.Windows.Forms.Label m_Factor;
        private System.Windows.Forms.Button m_Generator;
        private System.Windows.Forms.TextBox m_LicenseCode;
        private System.Windows.Forms.Label lable5;
        private System.Windows.Forms.TextBox m_txtHardware;
        private System.Windows.Forms.Button m_GenerateFile;
        private System.Windows.Forms.TextBox m_HashCode;
        private System.Windows.Forms.Label lable;
        private System.Windows.Forms.CheckBox m_ChkTrial;
        private System.Windows.Forms.TextBox m_txbDeviceId;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox m_ChkWeb;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.DateTimePicker m_DataPickTime;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox m_ChKFullAccess;
        private System.Windows.Forms.DateTimePicker m_PaymentExpiryDate;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox m_chbPaymentActiviated;
        private System.Windows.Forms.TextBox m_txtTerminalsAmount;
        private System.Windows.Forms.Label label6;
    }
}

