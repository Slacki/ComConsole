﻿using System;

namespace ComConsole
{
    public class DataRecievedEventArgs : EventArgs
    {
        public string data 
        { 
            get; 
            private set; 
        }

        public DataRecievedEventArgs(string data)
        {
            this.data = data;
        }
    }
}
