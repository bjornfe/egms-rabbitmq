using System;

namespace EGMS.RabbitMQ.TestServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new RabbitMQServiceBuilder()
                .SetDebugWriter(msg => Console.WriteLine(msg))
                .SetOptions(opt =>
                {
                    opt.Hostname = "<servername>";
                    opt.Username = "<username>";
                    opt.Password = "<password>";
                    opt.VirtualHost = "/";
                    opt.UseSSL = true;
                    opt.Port = 5671;

                })
                .AddSubscription(new TestCommandHandler())
                .Build();

            Console.ReadLine();
        }
    }
}
