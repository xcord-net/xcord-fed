# Xcord Integration Tests

This project contains integration tests for the Xcord federation instance backend using real PostgreSQL 17 and Redis 7 containers via Testcontainers.

## Prerequisites

- Docker (for Testcontainers)
- .NET 9 SDK

## Running Tests

```bash
# Run all integration tests
dotnet test

# Run specific category
dotnet test --filter "Category=Integration"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

## Test Structure

- **Fixtures/** - Testcontainers lifecycle management
  - `PostgresFixture` - PostgreSQL 17 container
  - `RedisFixture` - Redis 7 container
  - `IntegrationFixture` - Combined fixture for all integration tests

- **EntityConfigurationTests** - Entity Framework configuration and relationships
- **SnowflakePersistenceTests** - Snowflake ID generation and persistence
- **RedisOperationsTests** - Redis operations (strings, expiration, sorted sets)
- **ConversationModelTests** - Conversation model and message relationships

## Key Features

- Ephemeral containers (fresh state per test collection)
- Real PostgreSQL and Redis instances (no mocking)
- xUnit collection fixtures for shared container lifecycle
- FluentAssertions for readable test assertions

## Notes

- Tests use the `[Collection("Integration")]` attribute to share container fixtures
- Each test class gets a fresh database via `CreateFreshDbContext()`
- Email fields use byte arrays (encrypted via pgcrypto in production)
- All entities use Snowflake IDs (64-bit integers)
