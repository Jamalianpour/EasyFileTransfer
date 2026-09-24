using EasyFileTransfer;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace Sample.Server
{
    public partial class Form1 : Form
    {
        private EftServer server;

        public Form1()
        {
            InitializeComponent();
            label3.Text = GetLocalIPAddress();
            saveTo.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }

            return "unknown";
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            if (server != null)
            {
                await server.StopAsync();
                server = null;
                SetRunning(false);
                return;
            }

            try
            {
                server = new EftServer(new EftServerOptions
                {
                    SaveDirectory = saveTo.Text,
                    Port = Convert.ToInt32(Port.Text),
                    AccessToken = string.IsNullOrEmpty(tokenBox.Text) ? null : tokenBox.Text,
                });
                server.FileReceived += (s, args) => BeginInvoke(new Action(() => lastFile.Text = "received: " + Path.GetFileName(args.FilePath)));
                server.TransferFailed += (s, args) => BeginInvoke(new Action(() => lastFile.Text = "failed: " + args.Exception.Message));
                server.Start();
                SetRunning(true);
            }
            catch (Exception ex)
            {
                server = null;
                MessageBox.Show(ex.Message);
            }
        }

        private void SetRunning(bool running)
        {
            status.ForeColor = running ? Color.Green : Color.Red;
            status.Text = running ? "Online" : "Off";
            StartButton.Text = running ? "Stop Server" : "Start Server";
            saveTo.Enabled = Port.Enabled = tokenBox.Enabled = button1.Enabled = !running;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                saveTo.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            server?.Dispose();
        }
    }
}
