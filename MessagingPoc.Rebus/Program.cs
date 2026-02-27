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

app.Run();

[TopicName("rebus-demo-topic")]
public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
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