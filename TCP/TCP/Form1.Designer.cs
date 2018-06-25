namespace TCP
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.bt_start_server = new System.Windows.Forms.Button();
            this.saveTo = new System.Windows.Forms.TextBox();
            this.Port = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.status = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // bt_start_server
            // 
            this.bt_start_server.Location = new System.Drawing.Point(122, 12);
            this.bt_start_server.Name = "bt_start_server";
            this.bt_start_server.Size = new System.Drawing.Size(127, 47);
            this.bt_start_server.TabIndex = 0;
            this.bt_start_server.Text = "شروع سرویس";
            this.bt_start_server.UseVisualStyleBackColor = true;
            this.bt_start_server.Click += new System.EventHandler(this.bt_start_server_Click);
            // 
            // saveTo
            // 
            this.saveTo.Location = new System.Drawing.Point(18, 65);
            this.saveTo.Name = "saveTo";
            this.saveTo.Size = new System.Drawing.Size(231, 23);
            this.saveTo.TabIndex = 1;
            this.saveTo.Text = "D:\\c#\\TCP_test\\";
            // 
            // Port
            // 
            this.Port.Location = new System.Drawing.Point(122, 94);
            this.Port.Name = "Port";
            this.Port.Size = new System.Drawing.Size(127, 23);
            this.Port.TabIndex = 2;
            this.Port.Text = "1300";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.status);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.saveTo);
            this.groupBox1.Controls.Add(this.Port);
            this.groupBox1.Controls.Add(this.bt_start_server);
            this.groupBox1.Location = new System.Drawing.Point(5, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(345, 124);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            // 
            // status
            // 
            this.status.AutoSize = true;
            this.status.ForeColor = System.Drawing.Color.Red;
            this.status.Location = new System.Drawing.Point(289, 12);
            this.status.Name = "status";
            this.status.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.status.Size = new System.Drawing.Size(51, 17);
            this.status.TabIndex = 5;
            this.status.Text = "خاموش";
            this.status.Click += new System.EventHandler(this.label3_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(255, 97);
            this.label2.Name = "label2";
            this.label2.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.label2.Size = new System.Drawing.Size(43, 17);
            this.label2.TabIndex = 4;
            this.label2.Text = "پورت :";
            this.label2.Click += new System.EventHandler(this.label2_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(255, 68);
            this.label1.Name = "label1";
            this.label1.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.label1.Size = new System.Drawing.Size(82, 17);
            this.label1.TabIndex = 3;
            this.label1.Text = "محل ذخیره :";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(137, 127);
            this.label3.Name = "label3";
            this.label3.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.label3.Size = new System.Drawing.Size(80, 17);
            this.label3.TabIndex = 6;
            this.label3.Text = "آدرس شما :";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(354, 152);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.groupBox1);
            this.Font = new System.Drawing.Font("Tahoma", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form1";
            this.Text = "server ( گیرنده )";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button bt_start_server;
        private System.Windows.Forms.TextBox saveTo;
        private System.Windows.Forms.TextBox Port;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label status;
        private System.Windows.Forms.Label label3;
    }
}

