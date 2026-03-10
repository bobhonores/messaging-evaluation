using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Rebus.AwsSnsAndSqs;
using Rebus.AwsSnsAndSqs.Config;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Routing.TypeBased;
using Rebus.ServiceProvider;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var serviceUrl = cfg["AWS:ServiceURL"];

builder.Services.AddRebus(configure =>
    configure
        .Transport(t =>
            t.UseAmazonSnsAndSqs(
                amazonSqsConfig: new AmazonSQSConfig { ServiceURL = serviceUrl },
                amazonSimpleNotificationServiceConfig: new AmazonSimpleNotificationServiceConfig
                    { ServiceURL = serviceUrl },
                workerQueueAddress: cfg["MessageBus:Queue"]!,
                topicFormatter: new AttributeBasedTopicFormatter()
            ))
        .Routing(r => r.TypeBased().Map<WeatherForecast>(cfg["MessageBus:Queue"]!)));

builder.Services.AddRebusHandler<WeatherForecastHandler>();
builder.Services.AddRebusHandler<WeatherAlertHandler>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.Services.UseRebus();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast(
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast");

app.MapPost("/weatherforecast", async (IBus bus) =>
{
    var forecast = new WeatherForecast(
        DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
        Random.Shared.Next(-20, 55),
        summaries[Random.Shared.Next(summaries.Length)]);
    await bus.Publish(forecast);
    return forecast;
});

var severities = new[] { "Advisory", "Watch", "Warning", "Emergency" };
var locations  = new[] { "North", "South", "East", "West", "Central" };

app.MapPost("/weatheralert", async (IBus bus) =>
{
    var alert = new WeatherAlert(
        locations[Random.Shared.Next(locations.Length)],
        severities[Random.Shared.Next(severities.Length)],
        Random.Shared.Next(1, 100));
    await bus.Publish(alert);
    return alert;
});

app.Run();

[TopicName("rebus-demo-topic")]
public record WeatherAlert(string Location, string Severity, int WindSpeedKph);

[TopicName("rebus-demo-topic")]
public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public class WeatherAlertHandler(ILogger<WeatherAlertHandler> logger) : IHandleMessages<WeatherAlert>
{
    public Task Handle(WeatherAlert message)
    {
        logger.LogInformation("Received alert for {location} — severity: {severity}, wind: {wind} kph",
            message.Location, message.Severity, message.WindSpeedKph);
        return Task.CompletedTask;
    }
}

public class WeatherForecastHandler(ILogger<WeatherForecastHandler> logger) : IHandleMessages<WeatherForecast>
{
    public Task Handle(WeatherForecast message)
    {
        logger.LogInformation("Received message with forecast {date} - {temperature}",
            message.Date, message.TemperatureC);
        return Task.CompletedTask;
    }
}