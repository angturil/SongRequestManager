using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ChatCore.Utilities;

namespace SongRequestManager
{
    public class UdpListener
    {
        private readonly UdpClient _client = new UdpClient();
        private readonly IPEndPoint _listenAddress = new IPEndPoint(IPAddress.Any, 10360);

        public UdpListener()
        {
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _client.Client.ExclusiveAddressUse = false;
            _client.Client.Bind(_listenAddress);

            _client.BeginReceive(ReceiveData, _client);

            Plugin.Log("UDP Listener started.");
        }

        private void ReceiveData(IAsyncResult ar)
        {
            var client = (UdpClient)ar.AsyncState;
            var receivedIpEndPoint = new IPEndPoint(IPAddress.Any, 10360);
            var receivedBytes = client.EndReceive(ar, ref receivedIpEndPoint);

            // get data sent
            var data = Encoding.UTF8.GetString(receivedBytes);

            var msg = JSONNode.Parse(data); 

            // handle the udp message
            switch (msg["command"].Value)
            {
                case "mtt":
                    RequestBot.Instance.MoveRequestPositionInQueue(null, msg["value"].Value, true);
                    break;
                case "addmtt":
                    RequestBot.automtt.Add(msg["value"].Value.ToLower());
                    //RequestBot.Instance.MoveRequestPositionInQueue(null, msg["value"].Value, true);
                    break;
            }

            // receive a new
            client.BeginReceive(ReceiveData, ar.AsyncState);
        }

        public void Shutdown()
        {
            _client.Close();

            Plugin.Log("UDP Listener shutdown.");
        }
    }
}
