using MessagePack;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EGMS.RabbitMQ
{
    public abstract class RequestSubscription<T> : ISubscription
    {
       
        public string QueueName => GetType().Name;
        public bool Durable { get; protected set; } = true;

        public bool Exclusive { get; protected set; } = false;

        public bool AutoDelete { get; protected set; } = false;

        public bool AutoAck { get; protected set; } = false;

        public string RoutingKey { get; protected set; } = typeof(T).Name + ".*";

        public string Exchange { get; protected set; } = "Requests";

        public bool AutoRequeue { get; protected set; } = false;

        protected EventPublisher Events;
        protected RequestClient Requests;





        private IModel channel;

        public event Action<string> DebugText;

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

        protected virtual void handleReceivedMessage(object sender, BasicDeliverEventArgs e)
        {
            try
            {

                var msgObj = JsonConvert.DeserializeObject<RequestMessage>(Encoding.UTF8.GetString(e.Body));
                if (msgObj == null)
                {
                    DebugText?.Invoke(this.GetType().Name+": Failed to parse received message to RequestMessage");
                    return;
                }

                object result = null;

                try
                {
                    if (!msgObj.RequestName.Equals(typeof(T).Name))
                    {
                        DebugText?.Invoke(this.GetType().Name + ": Invalid Request type. Expected: " + typeof(T).Name + " Received: " + msgObj.RequestName);
                        if (!AutoAck)
                            channel.BasicNack(e.DeliveryTag, false, AutoRequeue);
                    }

                    var obj = msgObj.GetRequest<T>();
                    result = Handle(obj, msgObj);
                }
                catch (Exception err)
                {
                    DebugText?.Invoke(this.GetType().Name + ": Failed to Handle Request -> " + err.ToString());
                    DebugText?.Invoke(JsonConvert.SerializeObject(msgObj, Formatting.Indented));

                    if (!AutoAck)
                        channel.BasicNack(e.DeliveryTag, false, AutoRequeue);

                    return;
                }

                if (result == null)
                {
                    if (!AutoAck)
                        channel.BasicNack(e.DeliveryTag, false, AutoRequeue);

                    DebugText?.Invoke(this.GetType().Name + ": Handler Returned null");
                    return;
                }

                if (!AutoAck)
                    channel.BasicAck(e.DeliveryTag, false);

                if (e.BasicProperties == null || e.BasicProperties.ReplyTo == null)
                    return;

                var responseProps = channel.CreateBasicProperties();
                responseProps.CorrelationId = e.BasicProperties.CorrelationId;
                responseProps.Persistent = true;

                channel.BasicPublish(
                    exchange: "Response",
                    routingKey: typeof(T).Name+"."+e.BasicProperties.ReplyTo,
                    body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result)),
                    basicProperties: responseProps
                );

            }
            catch (Exception err)
            {
                DebugText.Invoke("RPCSubscription Callback Failed to handle received message -> " + err.ToString());
            }
        }

        public void Unsubscribe()
        {
            try
            {
                shutdownProcedure();
            }
            catch
            {

            }
        }

        public void Subscribe(IConnection connection, CancellationToken stoppingToken, EventPublisher eventService, RequestClient requestService)
        {
            if (connection == null)
                throw new ArgumentNullException();

            this.Events = eventService;
            this.Requests = requestService;

            Task.Run(async () =>
            {
                await createChannel(connection, stoppingToken);

                stoppingToken.Register(shutdownProcedure);



                var queueName = channel.QueueDeclare(QueueName, Durable, Exclusive, AutoDelete);

                var messageConsumer = new EventingBasicConsumer(channel);

                messageConsumer.Received += handleReceivedMessage;


                channel.BasicConsume(
                    queue: queueName,
                    autoAck: AutoAck,
                    consumer: messageConsumer
                    );

                channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                if (Exchange.Equals("") || RoutingKey.Equals(""))
                    return;

                channel.QueueBind(
                    queue: queueName,
                    routingKey: RoutingKey,
                    exchange: Exchange,
                    arguments: null
                    );
            });
        }

        protected abstract ResponseMessage Handle(T obj, RequestMessage msg);
    }
}
