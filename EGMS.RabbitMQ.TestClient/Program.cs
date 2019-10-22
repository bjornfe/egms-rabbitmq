using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace EGMS.RabbitMQ.TestClient
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
                .AddSubscription(new TestCommandExecutedHandler())
                .AddConnectedAction(rmqService =>
                {
                    Task.Run(() =>
                    {
                        var client = rmqService.GetRequestClient();

                        var cmd = new TestCommand()
                        {
                            CommandArgument = "TestArg"
                        };

                        var cmdResponse = client.Execute(cmd, 10);

                        Console.WriteLine(JsonConvert.SerializeObject(cmdResponse, Formatting.Indented));
                        

                    });

                })
                .Build();

            Console.ReadLine();
        }
    }
}
