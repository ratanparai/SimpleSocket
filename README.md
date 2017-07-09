# SimpleSocket
Example: 
```csharp
using System;
using SimpleSocket.Async;

namespace PlayGround
{
    class SocketServerTest : ServerServiceAsync
    {
        public SocketServerTest() : base(2524)
        {
        }

        public override void OnMessageReceive(SimpleSocketData data)
        {
            Console.WriteLine(data.Message);

            data.Message = "\n" +  data.Message.Length + " is the length of the message and the message was : " + data.Message + "\n";

            SendMessage(data);
        }

        public override void OnClientDisconnected()
        {
            Console.WriteLine("Total number of connected clients : " + ConnectedClients);
        }


    }
```
Test run the socket from console app - 
```csharp
using System;
using System.Net;

namespace PlayGround
{
    class Program
    {
        static void Main(string[] args)
        {
            SocketServerTest server = new SocketServerTest();
            Console.WriteLine("Server running");
            Console.ReadKey()
        }
}
```
    
