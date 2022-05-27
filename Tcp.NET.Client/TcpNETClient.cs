﻿using Newtonsoft.Json;
using PHS.Networking.Enums;
using PHS.Networking.Models;
using PHS.Networking.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tcp.NET.Client.Events.Args;
using Tcp.NET.Client.Handlers;
using Tcp.NET.Client.Models;
using Tcp.NET.Core.Models;

namespace Tcp.NET.Client
{
    public class TcpNETClient :
        TcpNETClientBase<
            TcpConnectionClientEventArgs,
            TcpMessageClientEventArgs,
            TcpErrorClientEventArgs,
            ParamsTcpClient,
            TcpClientHandler,
            ConnectionTcp>,
        ITcpNETClient
    {
        public TcpNETClient(ParamsTcpClient parameters, string token = "") : base(parameters, token)
        {
        }

        protected override void OnConnectionEvent(object sender, TcpConnectionClientEventArgs args)
        {
            FireEvent(this, args);
        }
        protected override void OnMessageEvent(object sender, TcpMessageClientEventArgs args)
        {
            FireEvent(this, args);
        }
        protected override void OnErrorEvent(object sender, TcpErrorClientEventArgs args)
        {
            FireEvent(this, args);
        }

        protected override TcpClientHandler CreateTcpClientHandler()
        {
            return new TcpClientHandler(_parameters, _token);
        }
    }
}
