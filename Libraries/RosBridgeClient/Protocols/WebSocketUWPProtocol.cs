/*
© David Whitney, 2018
Author: David Whitney (david_whitney@brown.edu)

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
<http://www.apache.org/licenses/LICENSE-2.0>.
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace RosSharp.RosBridgeClient.Protocols
{
    public class WebSocketUWPProtocol : IProtocol
    {
        private MessageWebSocket WebSocket;
        private readonly Uri uri;
        private DataWriter MessageWriter;
        private bool isAlive = false;

        public event EventHandler OnReceive;
        public event EventHandler OnConnected;
        public event EventHandler OnClosed;

        public WebSocketUWPProtocol(string uriString)
        {
            WebSocket = new MessageWebSocket();
            WebSocket.MessageReceived += Receive;
            WebSocket.Closed += Close;
            uri = TryGetUri(uriString);
        }

        public void Connect()
        {
            WebSocket.ConnectAsync(uri).Completed = (source, status) =>
                {
                    if (status == AsyncStatus.Completed)
                    {
                        MessageWriter = new DataWriter(WebSocket.OutputStream);
                        isAlive = true;
                        OnConnected(null, EventArgs.Empty);
                    }
                };
        }

        public void Close()
        {
            if (isAlive)
            {
                WebSocket.Close(1000, "Normal Closure");
                isAlive = false;
                OnClosed(null, EventArgs.Empty);
            }
        }

        private void Close(IWebSocket Socket, WebSocketClosedEventArgs EventArgs)
        {
            Close();
        }

        public bool IsAlive()
        {
            return isAlive;
        }

        public async void Send(byte[] message)
        {
            if (isAlive && MessageWriter != null)
            {
                try
                {
                    MessageWriter.WriteBytes(message);
                    await MessageWriter.StoreAsync();
                }
                catch
                {
                    return;
                }
            }
        }

        private void Receive(MessageWebSocket FromSocket, MessageWebSocketMessageReceivedEventArgs InputMessage)
        {
            try
            {
                using (var reader = InputMessage.GetDataReader())
                {
                    var messageLength = InputMessage.GetDataReader().UnconsumedBufferLength;
                    byte[] receivedMessage = new byte[messageLength];
                    reader.UnicodeEncoding = UnicodeEncoding.Utf8;
                    reader.ReadBytes(receivedMessage);
                    OnReceive.Invoke(this, new MessageEventArgs(receivedMessage));
                }
            }
            catch
            {
                return;
            }
        }

        static System.Uri TryGetUri(string uriString)
        {
            Uri webSocketUri;
            if (!Uri.TryCreate(uriString.Trim(), UriKind.Absolute, out webSocketUri))
                throw new System.Exception("Error: Invalid URI");

            // Fragments are not allowed in WebSocket URIs.
            if (!String.IsNullOrEmpty(webSocketUri.Fragment))
                throw new System.Exception("Error: URI fragments not supported in WebSocket URIs.");

            // Uri.SchemeName returns the canonicalized scheme name so we can use case-sensitive, ordinal string
            // comparison.
            if ((webSocketUri.Scheme != "ws") && (webSocketUri.Scheme != "wss"))
                throw new System.Exception("Error: WebSockets only support ws:// and wss:// schemes.");

            return webSocketUri;
        }
    }
}
