using System;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RaspberryAzure.PersistanceWorker
{
    public class QueueReaderFunc
    {
        private readonly ILogger<QueueReaderFunc> _logger;

        public QueueReaderFunc(ILogger<QueueReaderFunc> logger)
        {
            _logger = logger;
        }

        [Function(nameof(QueueReaderFunc))]
        public async Task Run(
            [ServiceBusTrigger("myqueue", Connection = "messaging", AutoCompleteMessages = false)]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogInformation("Message ID: {id}", message.MessageId);
            _logger.LogInformation("Message Body: {body}", message.Body);
            _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);
            // Complete the message
            //await messageActions.CompleteMessageAsync(message);
        }
    }
}
