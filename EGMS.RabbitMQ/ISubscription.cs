using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EGMS.RabbitMQ
{
    public interface ISubscription
    {
        

        event Action<string> DebugText;

        void Subscribe(IConnection connection, CancellationToken stoppingToken, EventPublisher eventPublisher, RequestClient client);
        void Unsubscribe();
    }
}
