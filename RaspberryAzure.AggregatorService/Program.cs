using AggregatorService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<HttpClient>(sp => 
    new HttpClient() { BaseAddress = new Uri(Environment.GetEnvironmentVariable("services__agent__http__0")) });

var host = builder.Build();
host.Run();
