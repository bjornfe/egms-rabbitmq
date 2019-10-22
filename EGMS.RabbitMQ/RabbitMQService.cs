using EGMS.RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EGMS.RabbitMQ
{
    public class RabbitMQService
    {
        private IConnection connection;

        private RabbitMQOptions options;
        private ConnectionFactory connection_factory;
        private CancellationTokenSource stopSubscriptionsTokenSource = new CancellationTokenSource();
        private CancellationToken stoppingToken;

        public event Action Connected;
        public event Action<string> DebugText;

        RequestClient client;
        EventPublisher publisher;


        public RabbitMQService(RabbitMQOptions options)
        {
            this.options = options;
        }

        public void Run(CancellationToken stoppingToken)
        {
            this.stoppingToken = stoppingToken;
            Task.Run(async () =>
            {
                while ((!check_connection() && !stoppingToken.IsCancellationRequested))
                {
                    await Task.Delay(1000, stoppingToken);
                }

                if (stoppingToken.IsCancellationRequested)
                    return;

                stoppingToken.Register(() =>
                {

                    stopSubscriptionsTokenSource.Cancel();

                    if (connection != null)
                        connection.Close();
                });

                GetEventPublisher();
                GetRequestClient();

                Connected?.Invoke();

            },stoppingToken);

        }

        private object connectionLock = new object();

        private bool check_connection()
        {

            lock (connectionLock)
            {
                if (connection != null && connection.IsOpen)
                    return true;
                else if (connection != null && !connection.IsOpen)
                    return false;

                try
                {
                    if (connection_factory == null)
                    {
                        try
                        {
                            connection_factory = new ConnectionFactory
                            {
                                UserName = options.Username,
                                Password = options.Password,
                                HostName = options.Hostname,
                                VirtualHost = options.VirtualHost,
                                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                                Port = options.Port,

                            };

                            if (options.UseSSL)
                            {
                                connection_factory.Ssl = new SslOption()
                                {
                                    Version = System.Security.Authentication.SslProtocols.Tls12,
                                    Enabled = true,
                                    AcceptablePolicyErrors = System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors | System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch
                                };
                            }
                        }
                        catch (Exception err)
                        {
                            return false;
                        }
                    }



                    if (connection == null)
                    {
                        try
                        {
                            connection = connection_factory.CreateConnection();

                            if (connection != null)
                            {
                                connection.ConnectionShutdown += (s, e) =>
                                {
                                    DebugText?.Invoke("RabbitMQ Connection Shut Down -> " + e.ReplyText);
                                };

                                connection.RecoverySucceeded += (s, e) =>
                                {
                                    DebugText?.Invoke("RabbitMQ Connetion Successfully Recovered");
                                };

                                connection.ConnectionRecoveryError += (s, e) =>
                                {
                                    DebugText?.Invoke("RabbitMQ Connection Failed to Recover");
                                };
                            }


                            DebugText?.Invoke("RabbitMQ Connected to " + connection_factory.HostName + " on port " + connection_factory.Port);
                        }
                        catch (Exception err)
                        {
                            return false;
                        }
                    }


                }
                catch (Exception err)
                {
                    return false;
                }
            }

            return true;
        }


        public void AddSubscription(ISubscription subscription)
        {
            subscription.Subscribe(connection, stopSubscriptionsTokenSource.Token,publisher, client);
            subscription.DebugText += (txt) => DebugText?.Invoke(txt);
        }

        public RequestClient CreateRequestClient()
        {
            var client = new RequestClient();
            client.Connect(connection, stopSubscriptionsTokenSource.Token);
            return client;
        }

        public EventPublisher CreateEventPublisher()
        {
            var client = new EventPublisher();
            client.Connect(connection, stopSubscriptionsTokenSource.Token);
            return client;
        }

        public RequestClient GetRequestClient()
        {
            if (client != null)
                return client;
            
            client = new RequestClient();
            client.Connect(connection, stopSubscriptionsTokenSource.Token);
            return client;
        }

        public EventPublisher GetEventPublisher()
        {
            if (publisher != null)
                return publisher;

            publisher = new EventPublisher();
            publisher.Connect(connection, stopSubscriptionsTokenSource.Token);
            return publisher;
        }

    }
}
