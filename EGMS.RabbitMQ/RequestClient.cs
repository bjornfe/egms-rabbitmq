using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using EGMS.RabbitMQ;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EGMS.RabbitMQ
{
    public class RequestClient : IDisposable
    {
        private class RpcResult
        {
            public DateTime ValidTo;
            public string CorrelationId;
            public Action<ResponseMessage> Success;
            public Action Timeout;
        }

        private class PublishMessage
        {
            public string CorrelationId;
            public RequestMessage Message;
        }

        private IModel channel;

        private BlockingCollection<PublishMessage> publish_queue = new BlockingCollection<PublishMessage>();

        private ConcurrentDictionary<string, RpcResult> rpc_queue = new ConcurrentDictionary<string, RpcResult>();

        CancellationTokenSource tokenSource = new CancellationTokenSource();

        public Guid RequestClientId = Guid.NewGuid();

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

        public void Connect(IConnection connection, CancellationToken token)
        {
            if (connection == null)
                throw new ArgumentNullException();

            token.Register(() =>
            {
                tokenSource.Cancel();
            });

            Task.Run(async () =>
            {
                while (!tokenSource.Token.IsCancellationRequested)
                {
                    while (rpc_queue.Count() > 0 && rpc_queue.Values.Any(v => DateTime.Now > v.ValidTo))
                    {
                        var entry = rpc_queue.Where(v => DateTime.Now > v.Value.ValidTo).First().Value;
                        entry.Timeout?.Invoke();
                        rpc_queue.TryRemove(entry.CorrelationId, out var val);
                    }

                    await Task.Delay(500, tokenSource.Token);
                }

            }, tokenSource.Token);

            Task.Run(async () =>
            {
                await createChannel(connection, tokenSource.Token);

                tokenSource.Token.Register(shutdownProcedure);


                var queueName = channel.QueueDeclare("", false, true, true);

                var messageConsumer = new EventingBasicConsumer(channel);

                messageConsumer.Received += (s,e)=> 
                {

                    try
                    {
                        if (e.BasicProperties == null || e.BasicProperties.CorrelationId == null)
                            return;

                        var msg = JsonConvert.DeserializeObject<ResponseMessage>(Encoding.UTF8.GetString(e.Body));
                        if (msg != null && rpc_queue.TryRemove(e.BasicProperties.CorrelationId, out var resp))
                        {
                            resp.Success?.Invoke(msg);
                        }

                    }
                    catch(Exception err)
                    {
                        Console.WriteLine("Response message deparse error -> " + err.ToString());
                    }
                   
                };


                channel.BasicConsume(
                    queue: queueName,
                    autoAck: true,
                    consumer: messageConsumer
                    );


                channel.QueueBind(
                    queue: queueName,
                    routingKey: "*."+ RequestClientId.ToString(),
                    exchange: "Response",
                    arguments: null
                    );

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
                            props.CorrelationId = msg.CorrelationId;
                            props.ReplyTo = RequestClientId.ToString();
                            props.Expiration = msg.Message.ValidSeconds.ToString();

                            var json = JsonConvert.SerializeObject(msg.Message);
                            Console.WriteLine(json);

                            var body = Encoding.UTF8.GetBytes(json);

                            channel.BasicPublish(
                                            exchange: "Requests",
                                            routingKey: msg.Message.RequestName+"."+msg.Message.RequestId,
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

        public ResponseMessage Execute(object request, int TimeoutSeconds)
        {
            var msg = new RequestMessage(request, TimeoutSeconds);
            return Execute(msg);
        }

        public ResponseMessage Execute(RequestMessage request)
        {
            try
            {
                var correlationId = Guid.NewGuid().ToString();

                BlockingCollection<ResponseMessage> waitngQueue = new BlockingCollection<ResponseMessage>();
                var msg = new PublishMessage()
                {
                    CorrelationId = correlationId,
                    Message = request
                };

                var result = new RpcResult()
                {
                    CorrelationId = correlationId,
                    Success = (resp) =>
                    {
                        waitngQueue.Add(resp);
                    },
                    Timeout = () =>
                    {
                        waitngQueue.Add(new ResponseMessage()
                        {
                            ResponseCode = 408,
                            Message = "Request Timed out ( t > " + request.ValidSeconds + " s )"
                        });
                    },
                    ValidTo = DateTime.Now.AddSeconds(request.ValidSeconds)
                };

                rpc_queue.TryAdd(correlationId, result);
                publish_queue.Add(msg);

                var response = waitngQueue.Take(tokenSource.Token);
                return response;

            }
            catch (Exception err)
            {
                return new ResponseMessage()
                {
                    ResponseCode = 500,
                    Message = "An error occured while executing request -> " + err.ToString()
                };
            }
        }

        public async Task<ResponseMessage> ExecuteAsync(RequestMessage request)
        {
            
            return await Task.Run(() =>
            {
                return Execute(request);

            }, tokenSource.Token);
               
        }

        public async Task<ResponseMessage> ExecuteAsync(object Request, int Timeout)
        {

            return await Task.Run(() =>
            {
                return Execute(Request, Timeout);

            }, tokenSource.Token);

        }

        public void Dispose()
        {
            tokenSource.Cancel();
           
        }
    }
}
