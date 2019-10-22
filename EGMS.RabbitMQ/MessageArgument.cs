using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace EGMS.RabbitMQ
{
    public class MessageArgument
    {
        public string Name { get; set; }
        public string JsonValue { get; set; }
        public T GetValue<T>()
        {
            return JsonConvert.DeserializeObject<T>(JsonValue);
        }
        public void SetValue(object value)
        {
            JsonValue = JsonConvert.SerializeObject(value);
        }

        public MessageArgument()
        {

        }

        public MessageArgument(string name, object value)
        {
            this.Name = name;
            SetValue(value);
        }
    }
}
