using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EGMS.RabbitMQ
{
    public class RabbitMQServiceBuilder
    {
        private RabbitMQOptions options = new RabbitMQOptions();
        private RabbitMQService rmqService;
        private CancellationTokenSource tokenSource;
        private CancellationToken stoppingToken;
        private bool useExternalToken;

        private List<ISubscription> subscriptions = new List<ISubscription>();
        private List<Action<RabbitMQService>> connectedActions = new List<Action<RabbitMQService>>();
        public RabbitMQServiceBuilder()
        {
            rmqService = new RabbitMQService(options);
            tokenSource = new CancellationTokenSource();
            stoppingToken = tokenSource.Token;

            rmqService.Connected += () =>
            {
                foreach(var s in subscriptions)
                {
                    rmqService.AddSubscription(s);
                }

                foreach (var a in connectedActions)
                    a?.Invoke(rmqService);
            };
        }

        public RabbitMQServiceBuilder SetDebugWriter(Action<string> act)
        {
            rmqService.DebugText += (txt) => act?.Invoke(txt);
            return this;
        }

        public RabbitMQServiceBuilder SetOptions(Action<RabbitMQOptions> opt)
        {
            opt?.Invoke(options);
            return this;
        }

        public RabbitMQServiceBuilder UseCancellationToken(CancellationToken token)
        {
            this.stoppingToken = token;
            this.tokenSource.Dispose();
            return this;
        }

        public RabbitMQServiceBuilder AddSubscription(ISubscription sub)
        {
            subscriptions.Add(sub);
            return this;
        }

        public RabbitMQServiceBuilder AddConnectedAction(Action<RabbitMQService> action)
        {
            connectedActions.Add(action);
            return this;
        }
       

        public RabbitMQService Build()
        {
            rmqService.Run(stoppingToken);
            return rmqService;
        }




    }
}
