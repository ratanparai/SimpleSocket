using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace SimpleSocket.Async
{
    public class SimpleSocketData
    {
        public string Message { get; set; }
        public SocketAsyncEventArgs SimpleSocketEventArgs { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
