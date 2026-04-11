# EaaS Testing Guide

## Prerequisites

- .NET 8 SDK
- Docker & Docker Compose
- (Optional) A REST client like Postman or curl

---

## Running Unit Tests

Unit tests run entirely in-memory and require no external services.

```bash
# Run all unit tests
dotnet test --filter "Category!=Integration"

# Run specific test projects
dotnet test tests/EaaS.Api.Tests/
dotnet test tests/EaaS.Infrastructure.Tests/
dotnet test tests/EaaS.Worker.Tests/

# Run with verbose output
dotnet test --verbosity normal

# Run a specific test class
dotnet test --filter "FullyQualifiedName~CreateInboundRuleHandlerTests"
```

---

## Running Integration Tests with Docker

Integration tests require the local Docker environment to be running.

### 1. Start Local Services

```bash
# Start all services with the local profile (includes Mailpit)
docker compose --profile local up -d

# Verify services are healthy
docker compose ps
```

Services available:
| Service | Port | Purpose |
|---------|------|---------|
| API | http://localhost:5000 | EaaS REST API |
| Dashboard | http://localhost:5001 | Next.js Dashboard |
| Webhook Processor | http://localhost:5002 | SNS/SES webhook handler |
| Mailpit Web UI | http://localhost:8025 | Email capture & inspection |
| Mailpit SMTP | localhost:1025 | SMTP server for dev |
| PostgreSQL | localhost:5432 | Database |
| Redis | localhost:6379 | Cache |
| RabbitMQ UI | http://localhost:15672 | Message queue management |

### 2. Run Integration Tests

```bash
dotnet test tests/EaaS.Integration.Tests/ --filter "Category=Integration"
```

---

## Testing Outbound Email with Mailpit

Mailpit captures all outbound emails in the local development environment. The `docker-compose.override.yml` configures the API and Worker to route all email through Mailpit's SMTP server on port 1025.

### Via the API

```bash
curl -X POST http://localhost:5000/api/v1/emails/send \
  -H "Content-Type: application/json" \
  -H "X-API-Key: eaas_live_devkey00000000000000000000000000000000" \
  -d '{
    "from": "sender@verified.com",
    "to": ["recipient@example.com"],
    "subject": "Test from EaaS",
    "html": "<h1>Hello</h1><p>This is a test email.</p>",
    "text": "Hello. This is a test email."
  }'
```

### Verify in Mailpit

1. Open http://localhost:8025 in your browser
2. The email should appear in the inbox within a few seconds
3. Click on it to inspect headers, HTML preview, text body, and raw source

### Mailpit API

Mailpit exposes a REST API for programmatic verification:

```bash
# List all messages
curl http://localhost:8025/api/v1/messages

# Search by subject
curl "http://localhost:8025/api/v1/search?query=subject:Test%20from%20EaaS"

# Get a specific message by ID
curl http://localhost:8025/api/v1/message/{id}

# Delete all messages (clean slate)
curl -X DELETE http://localhost:8025/api/v1/messages
```

---

## Testing Inbound Email Flow End-to-End

The inbound email pipeline follows this path:

```
AWS SES Inbound → SNS → Webhook Processor → RabbitMQ → InboundEmailConsumer → DB
```

In local development, you can simulate this flow by posting directly to the Webhook Processor's SNS endpoint.

### Step 1: Prepare a Test MIME File

Create a file called `test-inbound.eml`:

```
From: external@gmail.com
To: support@verified.com
Subject: Test Inbound Email
MIME-Version: 1.0
Content-Type: text/plain

This is a test inbound email.
```

### Step 2: Simulate the SNS Notification

The inbound flow is triggered by an SNS notification from SES. In local dev, you can bypass SNS and publish directly to RabbitMQ, or call the webhook processor endpoint.

**Option A: Direct RabbitMQ publish (via management UI)**

1. Open http://localhost:15672 (user: `eaas_app`, password from `.env`)
2. Navigate to Queues > find the `process-inbound-email` queue
3. Publish a message with this JSON body:

```json
{
  "s3BucketName": "eaas-inbound",
  "s3ObjectKey": "incoming/test-001",
  "sesMessageId": "local-test-001",
  "recipients": ["support@verified.com"],
  "spamVerdict": "PASS",
  "virusVerdict": "PASS",
  "spfVerdict": "PASS",
  "dkimVerdict": "PASS",
  "dmarcVerdict": "PASS"
}
```

**Option B: Simulate SNS via curl**

```bash
curl -X POST http://localhost:5002/webhooks/sns/inbound \
  -H "Content-Type: application/json" \
  -H "x-amz-sns-message-type: Notification" \
  -d '{
    "Type": "Notification",
    "MessageId": "test-notification-001",
    "TopicArn": "arn:aws:sns:us-east-1:123456789:ses-inbound",
    "Message": "{\"notificationType\":\"Received\",\"receipt\":{\"recipients\":[\"support@verified.com\"],\"spamVerdict\":{\"status\":\"PASS\"},\"virusVerdict\":{\"status\":\"PASS\"},\"spfVerdict\":{\"status\":\"PASS\"},\"dkimVerdict\":{\"status\":\"PASS\"},\"dmarcVerdict\":{\"status\":\"PASS\"},\"action\":{\"type\":\"S3\",\"bucketName\":\"eaas-inbound\",\"objectKey\":\"incoming/test-001\"}},\"mail\":{\"messageId\":\"local-test-001\"}}",
    "Timestamp": "2026-04-01T00:00:00.000Z"
  }'
```

### Step 3: Verify the Inbound Email

Check the database for the processed inbound email:

```bash
docker exec eaas-postgres psql -U eaas_app -d eaas -c \
  "SELECT id, from_email, subject, status FROM inbound_emails ORDER BY created_at DESC LIMIT 5;"
```

Or check via the API:

```bash
curl http://localhost:5000/api/v1/inbound/emails \
  -H "X-API-Key: eaas_live_devkey00000000000000000000000000000000"
```

---

## How to Simulate Inbound Email with Postman

1. Create a new POST request to `http://localhost:5002/webhooks/sns/inbound`
2. Set headers:
   - `Content-Type: application/json`
   - `x-amz-sns-message-type: Notification`
3. Use the JSON body from Option B above
4. Send the request
5. Check Mailpit (for any forwarded emails) or the database for the processed inbound record

---

## Test Coverage

Run tests with coverage collection:

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Generate HTML report (requires reportgenerator tool)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage/**/coverage.cobertura.xml -targetdir:coverage/report -reporttypes:Html
```

Open `coverage/report/index.html` to view the coverage report.
