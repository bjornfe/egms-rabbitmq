using EGMS.RabbitMQ;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace EGMS.RabbitMQ
{
    public class RequestMessage
    {
        public Guid RequestId { get; set; }
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
        public List<MessageArgument> Arguments { get; set; } = new List<MessageArgument>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int ValidSeconds { get; set; }
        public string RequestName { get; set; }
        public string RequestJson { get; set; }

        public T GetRequest<T>() => JsonConvert.DeserializeObject<T>(RequestJson);

        public RequestMessage()
        {

        }

        public RequestMessage(object Request, int ValidSeconds)
        {
            SetRequest(Request);
            this.ValidSeconds = ValidSeconds;
        }


        public ResponseMessage Unauthorized(string message = "") => new ResponseMessage()
        {
            Arguments = Arguments,
            RequestId = RequestId,
            SessionId = SessionId,
            OrganizationId = OrganizationId,
            UserId = UserId,
            ResponseCode = 401,
            Message = message
        };
        public ResponseMessage BadRequest(string message = "") => new ResponseMessage()
        {
            Arguments = Arguments,
            RequestId = RequestId,
            SessionId = SessionId,
            OrganizationId = OrganizationId,
            UserId = UserId,
            ResponseCode = 404,
            Message = message
        };
        public ResponseMessage Error(string message = "") => new ResponseMessage()
        {
            Arguments = Arguments,
            RequestId = RequestId,
            SessionId = SessionId,
            OrganizationId = OrganizationId,
            UserId = UserId,
            ResponseCode = 500,
            Message = message
        };

        public ResponseMessage MessageOk(string message = "", object response = null)
        {
            var msg = new ResponseMessage()
            {
                Arguments = Arguments,
                RequestId = RequestId,
                SessionId = SessionId,
                OrganizationId = OrganizationId,
                UserId = UserId,
                ResponseCode = 200,
                Message = message
            };

            if (response != null)
                msg.SetResponse(response);

            return msg;
        }
       

        public ResponseMessage ObjectOk(object response)
        {
            var msg = new ResponseMessage()
            {
                Arguments = Arguments,
                RequestId = RequestId,
                SessionId = SessionId,
                OrganizationId = OrganizationId,
                UserId = UserId,
                ResponseCode = 200,
                Message = "OK"
            };
            msg.SetResponse(response);
            return msg;
        }
        
        public ResponseMessage NoChange(string message = "") => new ResponseMessage()
        {
            Arguments = Arguments,
            RequestId = RequestId,
            SessionId = SessionId,
            OrganizationId = OrganizationId,
            UserId = UserId,
            ResponseCode = 304,
            Message = message
        };
        public ResponseMessage Timeout(string message = "") => new ResponseMessage()
        {
            Arguments = Arguments,
            RequestId = RequestId,
            SessionId = SessionId,
            OrganizationId = OrganizationId,
            UserId = UserId,
            ResponseCode = 408,
            Message = message
        };

        public EventMessage CreateEventMessage(object evt)
        {
            var msg = new EventMessage();
            msg.Arguments = Arguments;
            msg.RequestId = RequestId;
            msg.SessionId = SessionId;
            msg.OrganizationId = OrganizationId;
            msg.UserId = UserId;
            msg.EventName = evt.GetType().Name;
            msg.EventJson = JsonConvert.SerializeObject(evt);
            return msg;
        }

        public RequestMessage CreateRequestMessage(object request)
        {
            return new RequestMessage()
            {
                RequestId = RequestId,
                Arguments = Arguments,
                SessionId = SessionId,
                OrganizationId = OrganizationId,
                UserId = UserId,
                RequestName = request.GetType().Name,
                RequestJson = JsonConvert.SerializeObject(request)
            };
        }

        public RequestMessage SetRequest(object request)
        {
            RequestName = request.GetType().Name;
            RequestJson = JsonConvert.SerializeObject(request);
            return this;
        }
    }
}
