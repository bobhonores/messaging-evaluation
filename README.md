# MessagingPoc

A side-by-side comparison of three .NET messaging libraries integrated with AWS SNS/SQS, running locally via [LocalStack](https://github.com/localstack/localstack).

| Project | Library | Port |
|---|---|---|
| `MessagingPoc.Aws` | [AWS.Messaging](https://github.com/awslabs/aws-dotnet-messaging) 1.1.1 | 5009 |
| `MessagingPoc.Wolverine` | [WolverineFx](https://wolverine.netlify.app) 5.16.2 | 5004 |
| `MessagingPoc.Rebus` | [Rebus](https://github.com/rebus-org/Rebus) 6.7.0 + [Rebus.AwsSnsAndSqs](https://github.com/rebus-org/Rebus.AwsSnsAndSqs) 6.0.9 | 5010 |

Each app exposes the same two endpoints:

- `POST /weatherforecast` — publishes a `WeatherForecast` message to SNS
- `GET /weatherforecast` — returns a list of forecasts (no messaging)

The handler in each app receives the message from SQS and logs it.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/products/docker-desktop)
- [just](https://github.com/casey/just#installation) task runner
- [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html)

---

## Quick start

```bash
# Start LocalStack and run an app (LocalStack starts automatically)
just aws        # http://localhost:5009
just wolverine  # http://localhost:5004
just rebus      # http://localhost:5010

# Publish a message
curl -X POST http://localhost:5009/weatherforecast

# Tear down LocalStack
just localstack-down
```

All `just` recipes that run an app depend on `localstack`, which:

1. Creates the `localstack` AWS named profile (`aws_access_key_id=test`)
2. Starts LocalStack via Docker Compose if not already running
3. Waits until the SQS and SNS services report healthy

---

## Available recipes

```
$ just --list

just aws             # Run MessagingPoc.Aws — port 5009
just wolverine       # Run MessagingPoc.Wolverine — port 5004
just rebus           # Run MessagingPoc.Rebus — port 5010
just localstack      # Start LocalStack (runs automatically before each app)
just localstack-down # Stop and remove the LocalStack container
just configure-aws   # Create/update the localstack AWS named profile
```

---

## Project structure

```
MessagingPoc/
├── localstack/
│   ├── docker-compose.yml          # LocalStack container (SNS + SQS on :4566)
│   └── localstack-init/
│       └── init.sh                 # Creates all queues, topics, and subscriptions
├── MessagingPoc.Aws/
├── MessagingPoc.Wolverine/
├── MessagingPoc.Rebus/
├── MessagingPoc.sln
├── justfile
└── .gitattributes                  # Enforces LF on init.sh
```

### LocalStack resources created by `init.sh`

| App | Queue | Topic | RawMessageDelivery |
|---|---|---|---|
| Aws.Messaging | `demo-queue` | `demo-topic` | false (SDK handles SNS envelope) |
| Wolverine | `wolverine-demo-queue`, `wolverine-dead-letter-queue` | `wolverine-demo-topic` | **true** |
| Rebus | `rebus-demo-queue` | `rebus-demo-topic` | **true** |

---

## Implementation notes

### AWS.Messaging (`MessagingPoc.Aws`)

Uses the official [AWS Message Processing Framework for .NET](https://github.com/awslabs/aws-dotnet-messaging). Configuration is done entirely through `AddAWSMessageBus`:

```csharp
builder.Services.AddAWSMessageBus(b =>
{
    b.AddSQSPoller(queueUrl);
    b.AddSNSPublisher<WeatherForecast>(topicArn);
    b.AddMessageHandler<WeatherForecastHandler, WeatherForecast>();
});
```

The SDK handles the SNS notification envelope natively, so `RawMessageDelivery` on the subscription is not required.

### WolverineFx (`MessagingPoc.Wolverine`)

Uses `UseAmazonSnsTransportLocally()` and `UseAmazonSqsTransportLocally()` to point at LocalStack. Wolverine does **not** auto-provision resources in local mode — all queues and topics must exist before startup, including the `wolverine-dead-letter-queue`.

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.UseAmazonSnsTransportLocally();
    opts.UseAmazonSqsTransportLocally();
    opts.PublishMessage<WeatherForecast>().ToSnsTopic(topicName);
    opts.ListenToSqsQueue(queueName);
});
```

Wolverine's SQS envelope mapper Base64-decodes the message body directly, so the SNS→SQS subscription must have `RawMessageDelivery=true`.

### Rebus (`MessagingPoc.Rebus`)

Uses `Rebus.AwsSnsAndSqs` 6.0.9, which was compiled against Rebus 6.5.5. Rebus 7.x+ added interface members that break binary compatibility, so packages are pinned:

| Package | Version |
|---|---|
| `Rebus` | 6.7.0 |
| `Rebus.ServiceProvider` | 6.4.1 |
| `Rebus.AwsSnsAndSqs` | 6.0.9 |

The `[TopicName("rebus-demo-topic")]` attribute on the message class routes publishes to the correct SNS topic. `app.Services.UseRebus()` must be called after `app.Build()` to start the SQS polling worker.

Like Wolverine, `Rebus.AwsSnsAndSqs` decodes the SQS body directly and requires `RawMessageDelivery=true` on the subscription.

---

## Running without `just`

If you prefer to run commands manually:

```bash
# 1. Configure AWS credentials for LocalStack
aws configure set aws_access_key_id test --profile localstack
aws configure set aws_secret_access_key test --profile localstack
aws configure set region us-east-1 --profile localstack

# 2. Start LocalStack
docker compose -f localstack/docker-compose.yml --project-directory localstack up -d

# 3. Run an app (Rebus requires AWS_PROFILE in the environment)
dotnet run --project MessagingPoc.Aws/MessagingPoc.Aws.csproj
dotnet run --project MessagingPoc.Wolverine/MessagingPoc.Wolverine.csproj
AWS_PROFILE=localstack dotnet run --project MessagingPoc.Rebus/MessagingPoc.Rebus.csproj
```
