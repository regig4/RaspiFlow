using System.Collections;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Amqp.Framing;

namespace AggregatorService;

public record Record(double Data, long Timestamp); 

public class Worker : BackgroundService
{
    ServiceBusClient _client;

    ServiceBusProcessor _processor;

    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _httpClient;

    private const int ChannelCapacity = 10;

    private static readonly Channel<Record> _channel = Channel.CreateBounded<Record>(ChannelCapacity);

    public Worker(ILogger<Worker> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public static void SendToChannel(Record record) => _channel.Writer.TryWrite(record);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client = new ServiceBusClient(Environment.GetEnvironmentVariable("ConnectionStrings__myservicebus"));
        _processor = _client.CreateProcessor("myqueue", new ServiceBusProcessorOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });

        try
        {
            _processor.ProcessMessageAsync += MessageHandler;
            _processor.ProcessErrorAsync += ErrorHandler;

            await _processor.StartProcessingAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
                if (_channel.Reader is not { CanCount: true, Count: ChannelCapacity }) continue;
                var aggregate = new List<Record>();
                while (_channel.Reader.TryRead(out var record))
                {
                    _logger.LogInformation("Worker aggregating at: {time}, aggregated value: {record.Data}", DateTimeOffset.Now, record.Data);
                    aggregate.Add(record);
                }
                var jsonObject = JsonSerializer.Serialize(aggregate);
                _logger.LogInformation(jsonObject.ToString());
                var content = new StringContent(jsonObject.ToString(), Encoding.UTF8, "application/json");
                await _httpClient.PostAsync("/addData", content, stoppingToken);
            }

            await _processor.StopProcessingAsync(stoppingToken);
        }
        finally
        {
            await _processor.DisposeAsync();
            await _client.DisposeAsync();
        }

    }

    async Task MessageHandler(ProcessMessageEventArgs args)
    {
        _logger.LogInformation("Message handler");
        _logger.LogInformation("Message handled: {msg}, body: {b}", args.Message, args.Message.Body);
        //File.AppendAllText("./tmp23.txt", args.Message.Body.ToString());
        var record = JsonSerializer.Deserialize<Record>(args.Message.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (record != null) SendToChannel(record);
    }

    Task ErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogInformation("Error handler");
        _logger.LogError(args.Exception.ToString());
        return Task.CompletedTask;
    }
}