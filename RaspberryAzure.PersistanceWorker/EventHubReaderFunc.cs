using System;
using System.Text;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RaspberryAzure
{
    public class EventHubReaderFunc
    {
        private readonly ILogger<EventHubReaderFunc> _logger;
        private readonly BlobServiceClient _client;

        public EventHubReaderFunc(ILogger<EventHubReaderFunc> logger, BlobServiceClient client)
        {
            _logger = logger;
            _client = client;
        }

        [Function(nameof(EventHubReaderFunc))]
        public async Task Run([EventHubTrigger("eh1", Connection = "ehConnection")] EventData[] events)
        {
            StringBuilder logs = new();
            foreach (EventData @event in events)
            {
                _logger.LogInformation("Event Body: {body}", @event.Body);
                _logger.LogInformation("Event Content-Type: {contentType}", @event.ContentType);
                logs.AppendLine(Encoding.UTF8.GetString(@event.Body.ToArray()));
                var sensorRead = @event.Properties["simulated_sensor_read"];
                logs.AppendLine(sensorRead.ToString());
            }
            //
            // DefaultAzureCredentialOptions options = new()
            // {
            //     ExcludeEnvironmentCredential = true,
            //     ExcludeManagedIdentityCredential = true
            // };
            //
            // string accountName = "sastorageaccount012"; 
            //
            // DefaultAzureCredential credential = new DefaultAzureCredential(options);
            //
            // string blobServiceEndpoint = $"https://{accountName}.blob.core.windows.net";
            // BlobServiceClient blobServiceClient = new BlobServiceClient(new Uri(blobServiceEndpoint), credential);
            
            // string containerName = "logs";
            // _logger.LogInformation("Creating container: " + containerName);
            // BlobContainerClient containerClient = 
            //     await blobServiceClient.CreateBlobContainerAsync(containerName);

            
            var containerClient = _client.GetBlobContainerClient("logs");
            await containerClient.CreateIfNotExistsAsync();

            if (containerClient != null)
            {
                _logger.LogInformation("Container created successfully.");
            }
            else
            {
                _logger.LogInformation("Failed to create the container.");
                return;
            }

            _logger.LogInformation("Creating a local file for upload to Blob storage...");
            string localPath = ".";
            string fileName = Guid.CreateVersion7() + "logs.txt";
            string localFilePath = Path.Combine(localPath, fileName);
            File.Create(localFilePath).Close();
            await File.WriteAllTextAsync(localFilePath, logs.ToString());
            _logger.LogInformation("Local file created.");
            
            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            _logger.LogInformation("Uploading to Blob storage as blob:\n\t {0}", blobClient.Uri);

            using (FileStream uploadFileStream = File.OpenRead(localFilePath))
            {
                await blobClient.UploadAsync(uploadFileStream);
                uploadFileStream.Close();
            }

            bool blobExists = await blobClient.ExistsAsync();
            if (blobExists)
            {
                _logger.LogInformation("File uploaded successfully");
            }
            else
            {
                _logger.LogInformation("File upload failed");
                return;
            }
            
            _logger.LogInformation("Listing blobs in container...");
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                _logger.LogInformation("\t" + blobItem.Name);
            }

            
            // Adds the string "DOWNLOADED" before the .txt extension so it doesn't 
            // overwrite the original file
            string downloadFilePath = localFilePath.Replace(".txt", "DOWNLOADED.txt");
            _logger.LogInformation("Downloading blob to: {0}", downloadFilePath);

            // Download the blob's contents and save it to a file
            BlobDownloadInfo download = await blobClient.DownloadAsync();

            using (FileStream downloadFileStream = File.OpenWrite(downloadFilePath))
            {
                await download.Content.CopyToAsync(downloadFileStream);
            }

            _logger.LogInformation("Blob downloaded successfully to: {0}", downloadFilePath);
        }
    }
}
