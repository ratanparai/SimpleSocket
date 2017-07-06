using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace SimpleSocket.Async
{
    // Represents a collection of reusable SocketAsyncEventArgs objects.  
    class SocketAsyncEventArgsPool
    {
        Stack<SocketAsyncEventArgs> pool;

        // Initializes the object pool to the specified size
        //
        // The "capacity" parameter is the maximum number of 
        // SocketAsyncEventArgs objects the pool can hold
        public SocketAsyncEventArgsPool(int capacity)
        {
            pool = new Stack<SocketAsyncEventArgs>(capacity);
        }

        public void Push(SocketAsyncEventArgs item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null");
            }

            lock (pool)
            {
                pool.Push(item);
            }

        }

        public SocketAsyncEventArgs Pop()
        {
            lock (pool)
            {
                return pool.Pop();
            }
        }

        public int Count
        {
            get { return pool.Count;  }
        }
    }
}
