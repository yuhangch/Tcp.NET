﻿using System;
using Tcp.NET.Core.Enums;
using Tcp.NET.Core.Models;

namespace Tcp.NET.Core.Events.Args
{
    public class TcpConnectionAuthEventArgs : TcpConnectionEventArgs
    {
        public ConnectionSocketDTO ConnectionSocket { get; set; }
        public Guid UserId { get; set; }
        public TcpConnectionAuthType ConnectionAuthType { get; set; }
    }
}

