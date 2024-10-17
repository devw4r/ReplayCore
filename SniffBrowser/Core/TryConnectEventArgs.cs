using System;

namespace SniffBrowser.Core
{
    public class TryConnectEventArgs : EventArgs
    {
        public string ip;
        public int port;
        public TryConnectEventArgs(string ip, int port) : base() { this.ip = ip; this.port = port; }
    }
}
