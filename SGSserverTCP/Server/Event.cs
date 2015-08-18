using System;
using System.Collections.Generic;
using System.Text;

namespace Server
{
    public class Event
    {
        public delegate void SendMessage(string message);
        public event SendMessage SendIt;
        
        public void RaiseEvent(string message)
        {
            if (SendIt == null)
                return;
            else
                SendIt(message);
        }
    }
}
