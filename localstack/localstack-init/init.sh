#!/bin/bash
set -e

# ── MessagingPoc.Rebus ────────────────────────────────────────────────────────
echo "Creating Rebus SQS queue..."
awslocal sqs create-queue --queue-name rebus-demo-queue

echo "Creating Rebus SNS topic..."
REBUS_TOPIC_ARN=$(awslocal sns create-topic --name rebus-demo-topic --query TopicArn --output text)

echo "Getting Rebus SQS queue ARN..."
REBUS_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/rebus-demo-queue \
  --attribute-names QueueArn \
  --query Attributes.QueueArn --output text)

echo "Subscribing Rebus SQS to SNS (RawMessageDelivery=true)..."
REBUS_SUB_ARN=$(awslocal sns subscribe \
  --topic-arn "$REBUS_TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$REBUS_QUEUE_ARN" \
  --query SubscriptionArn --output text)

awslocal sns set-subscription-attributes \
  --subscription-arn "$REBUS_SUB_ARN" \
  --attribute-name RawMessageDelivery \
  --attribute-value true

# ── MessagingPoc.Wolverine ────────────────────────────────────────────────────
echo "Creating Wolverine SQS queues..."
awslocal sqs create-queue --queue-name wolverine-demo-queue
awslocal sqs create-queue --queue-name wolverine-dead-letter-queue

echo "Creating Wolverine SNS topic..."
WOLVERINE_TOPIC_ARN=$(awslocal sns create-topic --name wolverine-demo-topic --query TopicArn --output text)

echo "Getting Wolverine SQS queue ARN..."
WOLVERINE_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/wolverine-demo-queue \
  --attribute-names QueueArn \
  --query Attributes.QueueArn --output text)

echo "Subscribing Wolverine SQS to SNS (RawMessageDelivery=true)..."
WOLVERINE_SUB_ARN=$(awslocal sns subscribe \
  --topic-arn "$WOLVERINE_TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$WOLVERINE_QUEUE_ARN" \
  --query SubscriptionArn --output text)

awslocal sns set-subscription-attributes \
  --subscription-arn "$WOLVERINE_SUB_ARN" \
  --attribute-name RawMessageDelivery \
  --attribute-value true

# ── MessagingPoc.Aws ──────────────────────────────────────────────────────────
echo "Creating Aws.Messaging SQS queue..."
awslocal sqs create-queue --queue-name demo-queue

echo "Creating Aws.Messaging SNS topic..."
AWS_TOPIC_ARN=$(awslocal sns create-topic --name demo-topic --query TopicArn --output text)

echo "Getting Aws.Messaging SQS queue ARN..."
AWS_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/demo-queue \
  --attribute-names QueueArn \
  --query Attributes.QueueArn --output text)

echo "Subscribing Aws.Messaging SQS to SNS..."
awslocal sns subscribe \
  --topic-arn "$AWS_TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$AWS_QUEUE_ARN" \
  --query SubscriptionArn --output text

echo "LocalStack init complete."
