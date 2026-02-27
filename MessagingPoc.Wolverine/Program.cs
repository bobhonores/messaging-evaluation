using Scalar.AspNetCore;
using Wolverine;
using Wolverine.AmazonSqs;
using Wolverine.AmazonSns;

var builder = WebApplication.CreateBuilder(args);

// Configure AWS options from appsettings.json
var awsOptions = builder.Configuration.GetAWSOptions();
builder.Services.AddDefaultAWSOptions(awsOptions);

var queueName = builder.Configuration.GetValue<string>("MessageBus:Queue")!;
var topicName = builder.Configuration.GetValue<string>("MessageBus:Topic")!;

// Add Wolverine with AWS SNS/SQS configuration for LocalStack
builder.Host.UseWolverine(opts =>
{
    // Use LocalStack helper methods (resources created externally)
    opts.UseAmazonSnsTransportLocally();
    opts.UseAmazonSqsTransportLocally();

    // Publish WeatherForecast messages to SNS topic
    opts.PublishMessage<WeatherForecast>().ToSnsTopic(topicName);

    // Listen to the SQS queue for incoming messages
    opts.ListenToSqsQueue(queueName);
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast");

app.MapPost("/weatherforecast", async (IMessageBus messageBus) =>
{
    var forecast = new WeatherForecast
    (
        DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
        Random.Shared.Next(-20, 55),
        summaries[Random.Shared.Next(summaries.Length)]
    );
    await messageBus.PublishAsync(forecast);
    return forecast;
});

app.Run();

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public class WeatherForecastHandler(ILogger<WeatherForecastHandler> logger)
{
    public void Handle(WeatherForecast message)
    {
        logger.LogInformation("Received message with forecast {date} - {temperature}",
            message.Date, message.TemperatureC);
    }
}