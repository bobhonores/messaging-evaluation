#!/bin/bash
set -e

echo "Creating SQS queue..."
awslocal sqs create-queue --queue-name rebus-demo-queue

echo "Creating SNS topic..."
TOPIC_ARN=$(awslocal sns create-topic --name rebus-demo-topic --query TopicArn --output text)

echo "Getting SQS queue ARN..."
QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://sqs.us-east-1.localhost.localstack.cloud:4566/000000000000/rebus-demo-queue \
  --attribute-names QueueArn \
  --query Attributes.QueueArn --output text)

echo "Subscribing SQS to SNS (with RawMessageDelivery)..."
SUBSCRIPTION_ARN=$(awslocal sns subscribe \
  --topic-arn "$TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$QUEUE_ARN" \
  --query SubscriptionArn --output text)

echo "Enabling RawMessageDelivery on subscription..."
awslocal sns set-subscription-attributes \
  --subscription-arn "$SUBSCRIPTION_ARN" \
  --attribute-name RawMessageDelivery \
  --attribute-value true

echo "LocalStack init complete."
