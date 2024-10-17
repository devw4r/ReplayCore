using System;
using System.Net;
using SniffBrowser.Core;
using System.Windows.Forms;
using SniffBrowser.Network;
using System.Threading;

namespace SniffBrowser
{
    public partial class FormConnectionDialog : Form
    {
        public EventHandler<TryConnectEventArgs> OnTryConnect;
        private NetworkClient NetworkClient;

        public FormConnectionDialog()
        {
            InitializeComponent();
        }

        public void AttachNetworkClient(NetworkClient networkClient)
        {
            networkClient.OnConnectFail = OnConnectFail;
            networkClient.OnConnectSuccess += OnConnectSuccess;
            NetworkClient = networkClient;
        }

        private void OnConnectSuccess(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    OnConnectSuccess(sender, e);
                }));
                return;
            }

            Text = "Connected, waiting on server data.";
        }

        private void OnConnectFail(object sender, EventArgs e)
        {
            if(InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    OnConnectFail(sender, e);
                }));
                return;
            }

            MessageBox.Show(this, (string)sender, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnConnect.Enabled = true;
            txtIpAddress.Enabled = true;
            txtPort.Enabled = true;
        }

        public void UpdateProgress(int progress)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    UpdateProgress(progress);
                }));
                return;
            }

            if (progress != progressBar.Value)
            {
                progressBar.Value = progress;
                label1.Text = $"{progress}%";
                if (progress >= 20)
                {
                    var start = progressBar.Left;
                    var end = progressBar.Left + progressBar.Width;
                    var pos = progress * end / 100;

                    pictureBox1.Left = Math.Max(26, pos - 45); 
                }
            }

            if (progress == 100)
            {
                if (DataHolder.GetExpectedTotalSniffedEvents() != DataHolder.SniffedEvents.Count)
                    DialogResult = DialogResult.Cancel;
                else
                    DialogResult = DialogResult.OK;

                Close();
            }
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if(!IPAddress.TryParse(txtIpAddress.Text, out IPAddress _))
            {
                MessageBox.Show("Invalid ip.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
                return;
            }
            
            if(!int.TryParse(txtPort.Text, out int port))
            {
                MessageBox.Show("Invalid port.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
                return;
            }

            OnTryConnect?.Invoke(this, new TryConnectEventArgs(txtIpAddress.Text, port));
            btnConnect.Enabled = false;
            txtIpAddress.Enabled = false;
            txtPort.Enabled = false;
        }

        public void BeginDownloadingData()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    BeginDownloadingData();
                }));
                return;
            }

            progressBar.Value = 0;
            pictureBox1.Visible = true;
            label1.Text = "0%";
            Text = $"Receiving {DataHolder.GetExpectedTotalSniffedEvents()} sniffed events data...";
            NetworkClient?.RequestAllEvents();
        }

        private void BtnExit_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
