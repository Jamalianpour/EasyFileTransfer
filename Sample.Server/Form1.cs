using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using EasyFileTransfer;
using System.IO;

namespace Sample.Server
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            label3.Text = GetLocalIPAddress();
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
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            try
            {
                EftServer server = new EftServer(saveTo.Text, Convert.ToInt32(Port.Text));
                System.Threading.Thread obj_thread = new System.Threading.Thread(server.StartServer);
                obj_thread.Start();
                status.ForeColor = Color.Green;
                status.Text = "Online";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                saveTo.Text = folderBrowserDialog1.SelectedPath + @"\";
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void Form1_Leave(object sender, EventArgs e)
        {
            this.Dispose();
            Application.Exit();
        }
    }
}
