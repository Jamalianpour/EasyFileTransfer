using EasyFileTransfer;
using System;
using System.Windows.Forms;

namespace Sample.Client
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private async void SnedButton_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            if (!int.TryParse(textBox1.Text, out int port))
            {
                MessageBox.Show("Port must be a number.", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SnedButton.Enabled = false;
            statusLabel.Text = "sending...";
            try
            {
                var client = new EftClient(textBox2.Text.Trim(), port, new EftClientOptions
                {
                    AccessToken = string.IsNullOrEmpty(tokenBox.Text) ? null : tokenBox.Text,
                });

                // Progress<T> raises its callback on the UI thread.
                var progress = new Progress<EftProgress>(p => progressBar1.Value = (int)p.Percentage);
                await client.SendFileAsync(openFileDialog1.FileName, progress);

                statusLabel.Text = "send successfully.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "failed.";
                MessageBox.Show(ex.Message, "Send failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SnedButton.Enabled = true;
            }
        }
    }
}
