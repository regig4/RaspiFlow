using System;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RaspberryAzure.PersistanceWorker
{
    public class QueueReaderFunc
    {
        private readonly ILogger<QueueReaderFunc> _logger;
        private readonly CosmosClient _client;

        public QueueReaderFunc(ILogger<QueueReaderFunc> logger, CosmosClient client)
        {
            _logger = logger;
            _client = client;
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

            Database db = await _client.CreateDatabaseIfNotExistsAsync(id: "mydb");
            
            var container = await db.CreateContainerIfNotExistsAsync(
                id: "mycontainer", 
                partitionKeyPath: "[partition-key]",  
                throughput: 400);
            
            // Complete the message
            //await messageActions.CompleteMessageAsync(message);
        }
    }
}
