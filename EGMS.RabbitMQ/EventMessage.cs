using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace EGMS.RabbitMQ
{
    public class EventMessage
    {
        public Guid RequestId { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
        public List<MessageArgument> Arguments { get; set; } = new List<MessageArgument>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string EventName { get; set; }
        public string EventJson { get; set; }

        public T GetEvent<T>() => JsonConvert.DeserializeObject<T>(EventJson);
        public EventMessage SetEvent(object evt)
        {
            this.EventName = evt.GetType().Name;
            this.EventJson = JsonConvert.SerializeObject(evt);
            return this;
        }


    }
}
