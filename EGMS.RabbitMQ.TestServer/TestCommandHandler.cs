using System;
using System.Collections.Generic;
using System.Text;

namespace EGMS.RabbitMQ.TestServer
{
    public class TestCommandHandler : RequestSubscription<TestCommand>
    {
        protected override ResponseMessage Handle(TestCommand obj, RequestMessage msg)
        {
            Console.WriteLine("Received Command with argument: " + obj.CommandArgument);

            Events.Publish(msg.CreateEventMessage(new TestCommandExecuted()
            {
                EventInformation = "TestCommand with argument:  " + obj.CommandArgument + " Executed"
            }));


            return msg.MessageOk("Received OK");
        }
    }
}
