using System;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RaspberryAzure
{
    public class EventHubReaderFunc
    {
        private readonly ILogger<EventHubReaderFunc> _logger;

        public EventHubReaderFunc(ILogger<EventHubReaderFunc> logger)
        {
            _logger = logger;
        }

        [Function(nameof(EventHubReaderFunc))]
        public void Run([EventHubTrigger("eh1", Connection = "ehConnection")] EventData[] events)
        {
            foreach (EventData @event in events)
            {
                _logger.LogInformation("Event Body: {body}", @event.Body);
                _logger.LogInformation("Event Content-Type: {contentType}", @event.ContentType);
            }
        }
    }
}
