using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EGMS.RabbitMQ
{
    public class EventPublisher : IDisposable
    {
        private IModel channel;

        private BlockingCollection<EventMessage> publish_queue = new BlockingCollection<EventMessage>();

        CancellationTokenSource tokenSource = new CancellationTokenSource();

        public Action Ready;

        private async Task createChannel(IConnection connection, CancellationToken stoppingToken)
        {
            while (channel == null && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!connection.IsOpen)
                        await Task.Delay(500, stoppingToken);

                    channel = connection.CreateModel();
                }
                catch
                {

                }
            }
        }

        private void shutdownProcedure()
        {
            if (channel != null)
            {
                try
                {
                    if (channel.IsOpen)
                        channel.Close();
                    channel.Dispose();
                }
                catch
                {

                }
            }
        }

        public void Publish(EventMessage msg)
        {
            publish_queue.Add(msg);
            Console.WriteLine("Publishing event");
        }

        public void Connect(IConnection connection, CancellationToken token)
        {
            if (connection == null)
                throw new ArgumentNullException();

            tokenSource.Token.Register(shutdownProcedure);

            token.Register(() =>
            {
                tokenSource.Cancel();
            });

            Task.Run(async () =>
            {
                await createChannel(connection, tokenSource.Token);

                while (!tokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var msg = publish_queue.Take(tokenSource.Token);
                        if (channel == null || !channel.IsOpen || !connection.IsOpen)
                        {
                            publish_queue.Add(msg);
                            await Task.Delay(500, tokenSource.Token);
                            continue;
                        }
                        try
                        {
                            var props = channel.CreateBasicProperties();
                            props.Persistent = true;

                            var json = JsonConvert.SerializeObject(msg);
                            Console.WriteLine(json);

                            var body = Encoding.UTF8.GetBytes(json);

                            channel.BasicPublish(
                                            exchange: "Events",
                                            routingKey: "event."+msg.EventName+"."+msg.SessionId+"."+msg.RequestId,
                                            body: body,
                                            basicProperties: props
                                        );
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine("Failed to publish -> " + err.ToString());
                            publish_queue.Add(msg);
                            await Task.Delay(500, tokenSource.Token);
                        }



                    }
                    catch (Exception err)
                    {
                        Console.WriteLine("General publish error -> " + err.ToString());
                    }
                }


            });
        }

        public void Dispose()
        {
            tokenSource.Cancel();
        }
    }
}
