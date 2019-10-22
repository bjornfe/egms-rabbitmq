using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EGMS.RabbitMQ
{
    public abstract class EventSubscription<T> : ISubscription
    {
        public string QueueName => GetType().Name;
        public bool Durable { get; protected set; } = true;

        public bool Exclusive { get; protected set; } = false;

        public bool AutoDelete { get; protected set; } = false;

        public bool AutoAck { get; protected set; } = false;

        public string RoutingKey { get; protected set; } = null;

        public string Exchange { get; protected set; } = "Events";

        public bool AutoRequeue { get; protected set; } = false;

        protected EventPublisher Events;
        protected RequestClient Requests;

        protected virtual U Deserialize<U>(byte[] data)
        {
            return JsonConvert.DeserializeObject<U>(Encoding.UTF8.GetString(data));
        }

        protected virtual byte[] Serialize(object obj)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));
        }


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

                var msgObj = Deserialize<EventMessage>(e.Body);
                if (msgObj == null)
                {
                    DebugText?.Invoke(this.GetType().Name + ": Failed to parse received message to RequestMessage");
                    return;
                }

                try
                {
                    if (!msgObj.EventName.Equals(typeof(T).Name))
                    {
                        DebugText?.Invoke(this.GetType().Name + ": Invalid Event type. Expected: " + typeof(T).Name + " Received: " + msgObj.EventName);
                        if (!AutoAck)
                            channel.BasicNack(e.DeliveryTag, false, AutoRequeue);
                    }

                    var obj = msgObj.GetEvent<T>();
                    Handle(obj, msgObj);
                }
                catch (Exception err)
                {
                    DebugText?.Invoke(this.GetType().Name + ": Failed to Handle Event -> " + err.ToString());

                    if (!AutoAck)
                        channel.BasicNack(e.DeliveryTag, false, AutoRequeue);
                }

                if (!AutoAck)
                    channel.BasicAck(e.DeliveryTag, false);

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

                channel.QueueBind(
                    queue: queueName,
                    routingKey: RoutingKey == null ? "event." + typeof(T).Name + ".*.*" : RoutingKey,
                    exchange: "Events",
                    arguments: null
                    );
            });
        }

        protected abstract void Handle(T obj, EventMessage msg);
    }
}
