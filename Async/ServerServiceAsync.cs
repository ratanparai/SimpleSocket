using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SimpleSocket.Async
{
    public abstract class ServerServiceAsync
    {
        private readonly int _port;
        private Socket listener;

        // the maximum number of connections the sample is designed to handle simultaneously 
        private int numberOfMaxConnections;
        // Total number of connected sockets
        private int numberOfConnectedSockets;

        //A Semaphore has two parameters, the initial number of available slots
        // and the maximum number of slots. We'll make them the same.
        //This Semaphore is used to keep from going over max connection #.
        //(It is not about controlling threading really here.)
        private Semaphore maxNumberOfConnectedSocket;

        // Pool of reusable SocketAsyncEventArgs objects for write, read and accept socket operations
        private SocketAsyncEventArgsPool readWritePool;


        // TESTING BUFFER ADDITION 
        private BufferManager bufferManager;
        const int opsToPreAlloc = 2;    // read, write (don't alloc buffer space for accepts)

        public ServerServiceAsync(int Port = 11000, int maxConnection = 3000)
        {
            _port = Port;
            numberOfMaxConnections = maxConnection;

            // Now run the server
            Run();

        }
       
        // Call method to run the server properly
        private void Run()
        {
            Init();
            Start();
        }

        /// <summary>
        /// Initialize read and write SocketEventArgs pool
        /// </summary>
        private void Init()
        {
            maxNumberOfConnectedSocket = new Semaphore(numberOfMaxConnections, numberOfMaxConnections);
            readWritePool = new SocketAsyncEventArgsPool(numberOfMaxConnections);
            bufferManager = new BufferManager(1024 * numberOfMaxConnections * opsToPreAlloc, 1024);

            bufferManager.InitBuffer();
            // preallocate pool of Reusable SocketAsyncEventArgs objects 
            SocketAsyncEventArgs readWriteEventArgs;
            for (int i = 1; i <= numberOfMaxConnections; i++)
            {
                readWriteEventArgs = new SocketAsyncEventArgs();
                readWriteEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Complete);
                readWriteEventArgs.UserToken = new AsyncUserToken();

                // TESTING
                bufferManager.SetBuffer(readWriteEventArgs);

                readWritePool.Push(readWriteEventArgs);
            }
        }

        

        /// <summary>
        /// Start the Socket server process
        /// </summary>
        private void Start()
        {
            IPAddress address = IPAddress.Any;
            EndPoint localEndPoint = new IPEndPoint(address, _port);

            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(localEndPoint);
            listener.Listen(100);

            StartAccept(null);

            // Console.WriteLine("Server is running. Press any key to exit...");
            // Console.ReadKey();

        }

        private void StartAccept(SocketAsyncEventArgs acceptEventArgs)
        {
            if (acceptEventArgs == null)
            {
                acceptEventArgs = new SocketAsyncEventArgs();
                acceptEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(acceptEventArgs_completed);
            }
            else
            {
                // socket must be cleared since the context object is being reused
                acceptEventArgs.AcceptSocket = null;
            }

            // if maximum number of simultanius connection exceeds then wait for one or more connection to 
            // end before proceeds
            maxNumberOfConnectedSocket.WaitOne();

            // Returns true if the I / O operation is pending.The SocketAsyncEventArgs.Completed event on the e 
            // parameter will be raised upon completion of the operation.
            //
            // Returns false if the I/O operation completed synchronously. The SocketAsyncEventArgs.Completed 
            // event on the e parameter will not be raised and the e object passed as a parameter may be examined 
            // immediately after the method call returns to retrieve the result of the operation.
            bool willRaiseEvent = listener.AcceptAsync(acceptEventArgs);
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArgs);
            }
        }

        private void acceptEventArgs_completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            // Provides atomic operations for variables that are shared by multiple threads.
            // Thread safe increment of number of connected socket
            Interlocked.Increment(ref numberOfConnectedSockets);

            // debug print
            Console.WriteLine("Client connection accepted. Total number of client " + numberOfConnectedSockets);

            // get the Socket of accepted client connection and put it in the ReadAsyncEventArgs object
            // UserToken
            SocketAsyncEventArgs readAsyncEventArgs = readWritePool.Pop();
            ((AsyncUserToken)readAsyncEventArgs.UserToken).Socket = e.AcceptSocket;

            // Returns true if the I / O operation is pending.The SocketAsyncEventArgs.Completed event on the e 
            // parameter will be raised upon completion of the operation.
            //
            // Returns false if the I/O operation completed synchronously. The SocketAsyncEventArgs.Completed 
            // event on the e parameter will not be raised and the e object passed as a parameter may be examined 
            // immediately after the method call returns to retrieve the result of the operation.

            // check if acceptsocket is ok
            if(e.AcceptSocket != null)
            {
                Console.WriteLine("AcceptSocket is not null : " + e.SocketError.ToString());
            }

            bool willRaiseEvent = e.AcceptSocket.ReceiveAsync(readAsyncEventArgs);
            if(!willRaiseEvent)
            {
                ProcessReceive(readAsyncEventArgs);
            }

            // Start the next accept operation reusing the previous SocketAsyncEventArgs
            StartAccept(e);
        }

        /// <summary>
        /// This method is called when connected socket receive or send any data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IO_Complete(object sender, SocketAsyncEventArgs e)
        {
            Console.WriteLine("IO_Complete is called");
            // Determine which of the operation is completed and call associate handler
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;

                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;

                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        private void ProcessSend(SocketAsyncEventArgs e)
        {
            if(e.SocketError == SocketError.Success)
            {
                // done echoing data back to the client
                AsyncUserToken token = (AsyncUserToken)e.UserToken;
                // read the next block of data send from the client
                bool willRaiseEvent = token.Socket.ReceiveAsync(e);
                if (!willRaiseEvent)
                {
                    ProcessReceive(e);
                }
            } else
            {
                CloseClientSocket(e);
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            Console.WriteLine("Process Receive is called");
            AsyncUserToken token = (AsyncUserToken)e.UserToken;
            // check if the remote host closed the connection
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                // Log
                Console.WriteLine("The server have read total of {0} bytes of data", e.BytesTransferred);


                // Do data processing here
                string message = Encoding.ASCII.GetString(e.Buffer, e.Offset, e.BytesTransferred);
                OnReceive(e);

                // token.Socket.SendAsync(e);

                // start receiving again
                // token.Socket.ReceiveAsync(e);
            

            } else
            {
                // close client socket
                Console.WriteLine("Socket connection is clossing...");
                CloseClientSocket(e);
            }
        }



        private void OnReceive(SocketAsyncEventArgs e)
        {
            string message = Encoding.ASCII.GetString(e.Buffer, e.Offset, e.BytesTransferred);
            SimpleSocketData data = new SimpleSocketData();
            data.Message = message;
            data.SimpleSocketEventArgs = e;
            // Console.WriteLine(message);
            OnMessageReceive(data);
        }

        public abstract void OnMessageReceive(SimpleSocketData data);

        public void SendMessage(SimpleSocketData data)
        {
            SocketAsyncEventArgs eventArgs = data.SimpleSocketEventArgs;
            string message = data.Message;

            if(null != message && message.Trim() != "")
            {
                // send the message 
                byte[] buffer = Encoding.ASCII.GetBytes(message);

                eventArgs.SetBuffer(buffer, 0, message.Length);

                AsyncUserToken token = eventArgs.UserToken as AsyncUserToken;
                bool willRaiseEvent = token.Socket.SendAsync(eventArgs);
                if(!willRaiseEvent)
                {
                    ProcessSend(eventArgs);
                }
            }

        }

        /// <summary>
        /// Close Socket connection with one of the connected Client socket 
        /// </summary>
        /// <param name="e">SocketAsyncEvnetArgs object which have the socket of the client that 
        /// we want to close connection</param>
        public void CloseClientSocket(SocketAsyncEventArgs e)
        {
            AsyncUserToken token = e.UserToken as AsyncUserToken;

            // close the socket associate with the client
            try
            {
                token.Socket.Shutdown(SocketShutdown.Send);
            }
            catch (Exception) { }

            // Cant seem to find socket close so chekcing if shutdown close the socket connection or not
            PrintIfSocketIsClosedOrNot(token.Socket);

            //
        }

        private void PrintIfSocketIsClosedOrNot(Socket socket)
        {
            if (socket.Connected)
            {
                Console.WriteLine("Socket is still connected");
            } else
            {
                Console.WriteLine("Socket is closed sucessfully. Enjoy Freedom. :)");
            }
        }
    }
}