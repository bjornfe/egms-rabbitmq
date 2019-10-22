using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace EGMS.RabbitMQ
{
    public class ResponseMessage
    {
        public Guid RequestId { get; set; }
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
        public List<MessageArgument> Arguments { get; set; } = new List<MessageArgument>();
        public int ResponseCode { get; set; }
        public string Message { get; set; }
        public string ResponseJson { get; set; }
        public string ResponseType { get; set; }

        public T GetResponse<T>() => JsonConvert.DeserializeObject<T>(ResponseJson);
        public void SetResponse(object obj) 
        {
            ResponseType = obj.GetType().Name;
            ResponseJson = JsonConvert.SerializeObject(obj);
        }

        
    }
}
