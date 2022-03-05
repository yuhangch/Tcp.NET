﻿using Newtonsoft.Json;
using PHS.Networking.Enums;
using PHS.Networking.Models;
using PHS.Networking.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using Tcp.NET.Client.Events.Args;
using Tcp.NET.Client.Models;
using Tcp.NET.Core.Models;

namespace Tcp.NET.Client
{
    public class TcpNETClient : CoreNetworking<TcpConnectionClientEventArgs, TcpMessageClientEventArgs, TcpErrorClientEventArgs>,
        ITcpNETClient
    {
        protected readonly IParamsTcpClient _parameters;
        protected readonly string _oauthToken;
        protected IConnectionTcp _connection;
        protected bool _isClientRunning;

        public TcpNETClient(IParamsTcpClient parameters, string oauthToken = "")
        {
            _parameters = parameters;
            _oauthToken = oauthToken;
        }
        public virtual async Task ConnectAsync()
        {
            try
            {
                if (_parameters.IsSSL)
                {
                    await CreateSSLConnectionAsync();
                }
                else
                {
                    await CreateConnectionAsync();
                }

                _isClientRunning = true;

                if (!string.IsNullOrWhiteSpace(_oauthToken))
                {
                    await _connection.Writer.WriteLineAsync($"oauth:{_oauthToken}");
                }

                if (_connection.Client.Connected)
                {
                    FireEvent(this, new TcpConnectionClientEventArgs
                    {
                        Connection = _connection,
                        ConnectionEventType = ConnectionEventType.Connected,
                    });

                    _ = Task.Run(async () => { await StartListeningForMessagesAsync(); });
                };
            }
            catch (Exception ex)
            {
                FireEvent(this, new TcpErrorClientEventArgs
                {
                    Exception = ex,
                    Connection = _connection,
                    Message = ex.Message
                });
            }
        }
        public virtual bool Disconnect()
        {
            try
            {
                if (_connection != null)
                {
                    if (_connection != null &&
                        _connection.Writer != null)
                    {
                        _connection.Writer.Dispose();
                    }

                    if (_connection != null && 
                        _connection.Reader != null)
                    {
                        _connection.Reader.Dispose();
                    }

                    if (_connection != null &&
                        _connection.Client != null)
                    {
                        _connection.Client.Close();
                        _connection.Client.Dispose();
                    }

                    FireEvent(this, new TcpConnectionClientEventArgs
                    {
                        ConnectionEventType = ConnectionEventType.Disconnect,
                        Connection = _connection
                    });

                    _connection = null;

                    return true;
                }
            }
            catch (Exception ex)
            {
                FireEvent(this, new TcpErrorClientEventArgs
                {
                    Connection = _connection,
                    Exception = ex,
                    Message = ex.Message
                });
            }

            return false;
        }

        protected virtual Task CreateConnectionAsync()
        {
            // Establish the remote endpoint for the socket.  
            var client = new TcpClient(_parameters.Uri, _parameters.Port)
            {
                ReceiveTimeout = 60000
            };

            var reader = new StreamReader(client.GetStream());
            var writer = new StreamWriter(client.GetStream())
            {
                AutoFlush = true,
                NewLine = _parameters.EndOfLineCharacters
            };

            _connection = new ConnectionTcp
            {
                Client = client,
                Reader = reader,
                Writer = writer
            };

            return Task.CompletedTask;
        }
        protected virtual async Task CreateSSLConnectionAsync()
        {
            // Establish the remote endpoint for the socket.  
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            var client = new TcpClient(_parameters.Uri, _parameters.Port)
            {
                ReceiveTimeout = 60000,
            };

            var sslStream = new SslStream(client.GetStream());

            await sslStream.AuthenticateAsClientAsync(_parameters.Uri);

            if (sslStream.IsAuthenticated && sslStream.IsEncrypted)
            { 
                var reader = new StreamReader(sslStream);
                var writer = new StreamWriter(sslStream)
                {
                    AutoFlush = true,
                    NewLine = _parameters.EndOfLineCharacters
                };

                _connection = new ConnectionTcp
                {
                    Client = client,
                    Reader = reader,
                    Writer = writer
                };
            }
            else
            {
                throw new Exception("Could not create connection - SSL cert has validation problem.");
            }
        }
        protected virtual async Task StartListeningForMessagesAsync()
        {
            while (_connection != null && _connection.Client != null && _connection.Client.Connected)
            {
                try
                {
                    var message = await _connection.Reader.ReadLineAsync();

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        // Digest the ping first
                        if (message.Trim().ToLower() == "ping")
                        {
                            await SendToServerRawAsync("pong");
                        }
                        else
                        {
                            MessageReceived(message);

                        }
                    }
                }
                catch (Exception ex)
                {
                    FireEvent(this, new TcpErrorClientEventArgs
                    {
                        Connection = _connection,
                        Exception = ex,
                        Message = ex.Message
                    });

                    Disconnect();
                }
            }
        }
        protected virtual void MessageReceived(string message)
        {
            IPacket packet;
            try
            {
                packet = JsonConvert.DeserializeObject<Packet>(message);

                if (string.IsNullOrWhiteSpace(packet.Data))
                {
                    packet = new Packet
                    {
                        Data = message,
                        Timestamp = DateTime.UtcNow
                    };
                }
            }
            catch
            {
                packet = new Packet
                {
                    Data = message,
                    Timestamp = DateTime.UtcNow
                };
            }


            FireEvent(this, new TcpMessageClientEventArgs
            {
                MessageEventType = MessageEventType.Receive,
                Packet = packet,
                Connection = _connection
            });
        }

        public virtual async Task<bool> SendToServerAsync<T>(T packet) where T : IPacket
        {
            try
            {
                if (_connection != null &&
                    _connection.Client.Connected)
                {
                    var message = JsonConvert.SerializeObject(packet);
                    await _connection.Writer.WriteLineAsync(message);

                    FireEvent(this, new TcpMessageClientEventArgs
                    {
                        MessageEventType = MessageEventType.Sent,
                        Connection = _connection,
                        Packet = packet,
                    });
                    return true;
                }
            }
            catch (Exception ex)
            {
                FireEvent(this, new TcpErrorClientEventArgs
                {
                    Connection = _connection,
                    Exception = ex,
                    Message = ex.Message
                });

                Disconnect();
            }

            return false;
        }
        public virtual async Task<bool> SendToServerAsync(string message)
        {
            return await SendToServerAsync(new Packet
            {
                Data = message,
                Timestamp = DateTime.UtcNow
            });
        }
        public virtual async Task<bool> SendToServerRawAsync(string message)
        {
            try
            {
                if (_connection != null &&
                    _connection.Client != null &&
                    _connection.Client.Connected)
                {
                    await _connection.Writer.WriteAsync($"{message}{_parameters.EndOfLineCharacters}");

                    FireEvent(this, new TcpMessageClientEventArgs
                    {
                        MessageEventType = MessageEventType.Sent,
                        Connection = _connection,
                        Packet = new Packet
                        {
                            Data = message,
                            Timestamp = DateTime.UtcNow
                        },
                    }); 
                    return true;
                }
            }
            catch (Exception ex)
            {
                FireEvent(this, new TcpErrorClientEventArgs
                {
                    Connection = _connection,
                    Exception = ex,
                    Message = ex.Message
                });

                Disconnect();
            }

            return false;
        }

        public bool IsRunning
        {
            get
            {
                return _connection != null && _connection.Client.Connected;
            }
        }
        public IConnectionTcp Connection
        {
            get
            {
                return _connection;
            }
        }
    }
}
