using System;
using System.Collections.Generic;
using System.Text;

namespace EGMS.RabbitMQ.TestClient
{
    public class TestCommandExecutedHandler : EventSubscription<TestCommandExecuted>
    {
        protected override void Handle(TestCommandExecuted obj, EventMessage msg)
        {
            Console.WriteLine("Received Event: " + obj.EventInformation);
        }
    }
}
