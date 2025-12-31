# .NET 8 Microservices API

A production-ready .NET 8 Web API implementing best practices including JWT authentication, Azure Cosmos DB integration, secure password hashing, repository pattern, and comprehensive error handling.

## Features

- **JWT Authentication** - Secure token-based authentication with configurable expiration
- **Role-based Authorization** - Platform Admin, Org Admin, and User roles
- **Azure Cosmos DB** - Cloud-native NoSQL database for Users and Organizations
- **Secure Password Hashing** - PBKDF2 with HMAC-SHA256 (100,000 iterations)
- **Repository Pattern** - Clean separation of data access logic
- **Dependency Injection** - Built-in DI container with interface-based services
- **IOptions Pattern** - Strongly-typed configuration management
- **Global Exception Handling** - Centralized error handling middleware
- **Correlation ID** - Request tracking across the application
- **Structured Logging** - Serilog integration with console and file sinks
- **Swagger/OpenAPI** - Interactive API documentation
- **Organization Status Checks** - Automatic login blocking for suspended organizations

## Project Structure

```
src/Microservices/
├── Configuration/              # Configuration classes for IOptions pattern
│   ├── AppSettings.cs
│   ├── CosmosDbSettings.cs
│   ├── JwtSettings.cs
│   └── PasswordSettings.cs
├── Controllers/                # API controllers
│   ├── AuthController.cs
│   ├── OrganizationController.cs
│   ├── ProtectedController.cs
│   ├── SearchController.cs
│   └── UserController.cs
├── Core/                       # Domain layer
│   ├── DTOs/                   # Data Transfer Objects
│   │   ├── ApiResponse.cs
│   │   ├── LoginRequest.cs
│   │   ├── LoginResponse.cs
│   │   ├── OrganizationDTOs.cs
│   │   ├── SearchDTOs.cs
│   │   └── UserDTOs.cs
│   ├── Entities/               # Domain entities
│   │   ├── Organization.cs
│   │   ├── SearchableItem.cs
│   │   └── User.cs
│   ├── Exceptions/             # Custom exception types
│   │   ├── BaseException.cs
│   │   ├── BusinessException.cs
│   │   ├── ConflictException.cs
│   │   ├── ForbiddenException.cs
│   │   ├── NotFoundException.cs
│   │   ├── UnauthorizedException.cs
│   │   └── ValidationException.cs
│   └── Interfaces/             # Repository and service interfaces
│       ├── IAuthService.cs
│       ├── IJwtService.cs
│       ├── IOrganizationRepository.cs
│       ├── IOrganizationService.cs
│       ├── IPasswordService.cs
│       ├── ISearchRepository.cs
│       ├── ISearchService.cs
│       ├── IUserRepository.cs
│       └── IUserService.cs
├── Extensions/                 # Service collection extensions
│   └── ServiceCollectionExtensions.cs
├── Infrastructure/             # Infrastructure layer
│   ├── Repositories/           # Repository implementations
│   │   ├── CosmosOrganizationRepository.cs
│   │   ├── CosmosUserRepository.cs
│   │   └── SearchRepository.cs (in-memory mock)
│   └── Services/               # Service implementations
│       ├── AuthService.cs
│       ├── JwtService.cs
│       ├── OrganizationService.cs
│       ├── PasswordService.cs
│       ├── SearchService.cs
│       └── UserService.cs
├── Middleware/                 # Custom middleware
│   ├── CorrelationIdMiddleware.cs
│   ├── ExceptionHandlingMiddleware.cs
│   └── RequestLoggingMiddleware.cs
├── appsettings.json
└── Program.cs
```

## Getting Started

### Prerequisites

- .NET 8 SDK
- Azure Cosmos DB account (or emulator)
- Visual Studio 2022 / VS Code / Rider

### Azure Cosmos DB Setup

Create the following containers in your Cosmos DB database:

| Container | Partition Key |
|-----------|---------------|
| `Users` | `/orgId` |
| `Organizations` | `/id` |

### Configuration

Update `appsettings.json` with your settings:

```json
{
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong",
    "Issuer": "Microservices",
    "Audience": "MicroservicesClients",
    "ExpirationMinutes": 60
  },
  "PasswordSettings": {
    "SecretKey": "YourPasswordHashingSecretKey",
    "Iterations": 100000,
    "SaltSize": 16,
    "HashSize": 32
  },
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://your-account.documents.azure.com:443/;AccountKey=your-key;",
    "DatabaseName": "your-database",
    "UsersContainerName": "Users",
    "UsersPartitionKeyPath": "/orgId",
    "OrganizationsContainerName": "Organizations",
    "OrganizationsPartitionKeyPath": "/id"
  }
}
```

### Running the Application

```bash
cd net8-sample/src/Microservices
dotnet restore
dotnet run
```

The API will be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: http://localhost:5000 (root URL in development)

## API Endpoints

### Authentication

#### POST /api/auth/login
Authenticate a user and receive a JWT token.

**Request Body:**
```json
{
  "username": "admin@platform.com",
  "password": "Admin@123"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Login successful",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "tokenType": "Bearer",
    "expiresIn": 3600,
    "user": {
      "id": "usr_PLATFORM01",
      "name": "Platform Admin",
      "email": "admin@platform.com",
      "userType": "platform_admin",
      "role": "platform_admin",
      "orgId": "PLATFORM",
      "orgName": "Platform"
    }
  }
}
```

**Login Error Scenarios:**
| Scenario | Error Message |
|----------|---------------|
| Invalid credentials | "Invalid username or password" |
| User suspended | "User account is disabled" |
| Organization suspended | "Your organisation subscription is suspended. Please contact support." |
| Organization cancelled | "Your organisation subscription has been cancelled. Please contact support." |

### Organizations

| Method | Endpoint | Description | Access |
|--------|----------|-------------|--------|
| GET | `/api/organization` | List all organizations (paginated) | Platform Admin |
| GET | `/api/organization/{id}` | Get organization by ID | Platform Admin |
| POST | `/api/organization` | Create new organization | Platform Admin |
| PUT | `/api/organization/{id}` | Update organization | Platform Admin |
| DELETE | `/api/organization/{id}` | Soft delete organization | Platform Admin |

### Users

| Method | Endpoint | Description | Access |
|--------|----------|-------------|--------|
| GET | `/api/user?orgId={orgId}` | List users (filtered by org) | Platform Admin, Org Admin |
| GET | `/api/user/{id}` | Get user by ID | Platform Admin, Org Admin |
| POST | `/api/user?orgId={orgId}&orgName={name}` | Create user | Platform Admin, Org Admin |
| PUT | `/api/user/{id}` | Update user | Platform Admin, Org Admin |
| DELETE | `/api/user/{id}` | Soft delete user | Platform Admin, Org Admin |
| POST | `/api/user/{id}/reset-password` | Reset user password | Platform Admin, Org Admin |

### Search

| Method | Endpoint | Description | Access |
|--------|----------|-------------|--------|
| GET | `/api/search?q={query}` | Search items | Authenticated users |

### Health Check

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Application health status (public) |

## User Roles & Permissions

| Role | Permissions |
|------|-------------|
| `platform_admin` | Full access to all organizations and users |
| `org_admin` | Manage users within their organization only |
| `org_user` | Basic access (search) |

## Password Management

### Password Hashing
- Algorithm: PBKDF2 with HMAC-SHA256
- Iterations: 100,000 (configurable)
- Salt: 16 bytes random (per password)
- Hash: 32 bytes
- Format: `{iterations}.{salt_base64}.{hash_base64}`

### Auto-Generated Passwords
When creating or resetting a user password:
- Format: `{first4_email}_{first4_name}` (lowercase)
- Example: `john_john` for john.doe@example.com + John Doe

### Reset Password
- Endpoint: `POST /api/user/{id}/reset-password`
- Org admins can only reset passwords for users in their organization
- Platform admins can reset any user's password

## Data Models

### User (Cosmos DB)
```json
{
  "id": "usr_PLATFORM01",
  "userId": "usr_PLATFORM01",
  "orgId": "PLATFORM",
  "orgName": "Platform",
  "userType": "platform_admin",
  "role": "platform_admin",
  "status": "active",
  "isDeleted": false,
  "name": "Platform Admin",
  "email": "admin@platform.com",
  "auth": {
    "passwordHash": "100000.salt.hash"
  },
  "createdAt": "2025-12-30T10:00:00Z",
  "createdBy": "system",
  "modifiedAt": "2025-12-30T10:00:00Z",
  "modifiedBy": "system",
  "version": 1
}
```

### Organization (Cosmos DB)
```json
{
  "id": "org_01HABC",
  "type": "Organization",
  "orgId": "org_01HABC",
  "name": "Acme Corp",
  "status": "active",
  "isDeleted": false,
  "contact": {
    "email": "ops@acme.com",
    "phone": {
      "countryCode": "+91",
      "number": "9876543210",
      "e164": "+919876543210"
    },
    "address": {
      "line1": "12 MG Road",
      "city": "Bengaluru",
      "state": "Karnataka",
      "postalCode": "560001",
      "country": "IN"
    }
  },
  "subscription": {
    "limits": {
      "userLimit": 25
    }
  },
  "createdAt": "2025-12-24T00:00:00Z",
  "createdBy": "platform_admin_001",
  "version": 1
}
```

## Error Handling

All errors follow a consistent response format:

```json
{
  "success": false,
  "message": "Error description",
  "errorCode": "ERROR_CODE",
  "correlationId": "abc123...",
  "timestamp": "2025-01-01T00:00:00Z",
  "errors": ["Detail 1", "Detail 2"]
}
```

### Custom Exception Types

| Exception | HTTP Status | Error Code |
|-----------|-------------|------------|
| ValidationException | 400 | VALIDATION_ERROR |
| UnauthorizedException | 401 | UNAUTHORIZED |
| ForbiddenException | 403 | FORBIDDEN |
| NotFoundException | 404 | NOT_FOUND |
| ConflictException | 409 | CONFLICT |
| BusinessException | 422 | BUSINESS_ERROR |

## Correlation ID

All requests are tagged with a correlation ID for tracing:
- Provided in header: `X-Correlation-ID`
- Auto-generated if not provided
- Included in all responses
- Logged with each request

## Logging

Logs are written to:
- Console (development)
- Rolling file logs in `/logs` directory

Log format includes timestamp, log level, correlation ID, message, and exception details.

## License

MIT
