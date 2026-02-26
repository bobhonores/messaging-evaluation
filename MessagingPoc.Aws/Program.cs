using AWS.Messaging;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var awsOptions = builder.Configuration.GetAWSOptions();
builder.Services.AddDefaultAWSOptions(awsOptions);

builder.Services.AddAWSMessageBus(b =>
{
    b.AddSQSPoller(builder.Configuration.GetValue<string>("MessageBus:Queue")!);
    b.AddSNSPublisher<WeatherForecast>(builder.Configuration.GetValue<string>("MessageBus:Topic"));
    b.AddMessageHandler<WeatherForecastHandler, WeatherForecast>();
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

app.MapPost("/weatherforecast", async (IMessagePublisher publisher, CancellationToken ct) =>
{
    var forecast = new WeatherForecast
    (
        DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
        Random.Shared.Next(-20, 55),
        summaries[Random.Shared.Next(summaries.Length)]
    );
    await publisher.PublishAsync(forecast, ct);
    return forecast;
});

app.Run();

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public class WeatherForecastHandler(ILogger<WeatherForecastHandler> logger) : IMessageHandler<WeatherForecast>
{
    public Task<MessageProcessStatus> HandleAsync(MessageEnvelope<WeatherForecast> messageEnvelope, CancellationToken token = default)
    {
        logger.LogInformation("Received message {id} with forecast {date} - {temperature}", 
            messageEnvelope.Id, messageEnvelope.Message.Date, messageEnvelope.Message.TemperatureC);

        return Task.FromResult(MessageProcessStatus.Success());
    }
}