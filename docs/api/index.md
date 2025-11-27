# API Reference

Complete API documentation for Aspire-Full distributed application.

## Base URLs

| Environment | URL |
|-------------|-----|
| Development | `http://localhost:5047` |
| Dashboard | `http://localhost:18888` |
| OTLP | `http://localhost:18889` |

---

## Items API

Standard CRUD operations for items.

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/items` | List all items |
| GET | `/api/items/{id}` | Get item by ID |
| POST | `/api/items` | Create new item |
| PUT | `/api/items/{id}` | Update item |
| DELETE | `/api/items/{id}` | Delete item |

### Item Model

```json
{
  "id": 0,
  "name": "string",
  "description": "string",
  "createdAt": "2024-01-01T00:00:00Z",
  "updatedAt": "2024-01-01T00:00:00Z",
  "createdByUserId": 1
}
```

---

## Users API

Standard user management with upsert/downsert (soft-delete) pattern.

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users` | List active users |
| GET | `/api/users/{id}` | Get user by ID |
| GET | `/api/users/by-email/{email}` | Get user by email |
| POST | `/api/users` | **Upsert**: Create or reactivate user |
| PUT | `/api/users/{id}` | Update user details |
| DELETE | `/api/users/{id}` | **Downsert**: Soft delete user |
| POST | `/api/users/{id}/login` | Record login timestamp |

### User Model

```json
{
  "id": 0,
  "email": "user@example.com",
  "displayName": "string",
  "role": "User",
  "isActive": true,
  "createdAt": "2024-01-01T00:00:00Z",
  "updatedAt": "2024-01-01T00:00:00Z",
  "lastLoginAt": "2024-01-01T00:00:00Z",
  "deletedAt": null
}
```

### User Roles

| Role | Value | Description |
|------|-------|-------------|
| User | 0 | Standard user |
| Admin | 1 | Administrator |

### Upsert Behavior

When POSTing to `/api/users`:
- If email doesn't exist → Creates new user (201)
- If email exists but inactive → Reactivates user (200)
- If email exists and active → Returns conflict (409)

### Downsert Behavior

When DELETEing `/api/users/{id}`:
- Sets `IsActive = false`
- Sets `DeletedAt` timestamp
- User excluded from standard queries via global filter
- Can be reactivated via upsert or admin endpoint

---

## Admin API

Administrative operations including bulk actions and statistics.

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/users` | List **all** users (including deleted) |
| GET | `/api/admin/users/{id}` | Get any user by ID |
| POST | `/api/admin/users/{id}/promote` | Promote user to Admin |
| POST | `/api/admin/users/{id}/demote` | Demote admin to User |
| POST | `/api/admin/users/{id}/reactivate` | Reactivate soft-deleted user |
| DELETE | `/api/admin/users/{id}/permanent` | **Permanent** delete |
| POST | `/api/admin/users/bulk-deactivate` | Bulk soft-delete users |
| GET | `/api/admin/stats` | Get admin statistics |

### Admin Stats Response

```json
{
  "totalUsers": 100,
  "activeUsers": 95,
  "deletedUsers": 5,
  "adminCount": 3,
  "totalItems": 250
}
```

### Bulk Deactivate Request

```json
{
  "userIds": [1, 2, 3, 4, 5]
}
```

---

## Health Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check |
| GET | `/alive` | Liveness probe |

---

## Database Schema

### Entity Relationships

```
┌──────────────────────────────────────────────────────────────┐
│                         Users                                 │
├──────────────────────────────────────────────────────────────┤
│ Id (PK)          │ int           │ Auto-increment             │
│ Email            │ string(256)   │ Unique, Required           │
│ DisplayName      │ string(100)   │ Required                   │
│ Role             │ enum          │ User=0, Admin=1            │
│ IsActive         │ bool          │ Soft-delete flag           │
│ CreatedAt        │ DateTime      │ UTC timestamp              │
│ UpdatedAt        │ DateTime      │ UTC timestamp              │
│ DeletedAt        │ DateTime?     │ Soft-delete timestamp      │
│ LastLoginAt      │ DateTime?     │ Last login timestamp       │
└──────────────────────────────────────────────────────────────┘
                              │
                              │ 1:N (SetNull on delete)
                              ▼
┌──────────────────────────────────────────────────────────────┐
│                         Items                                 │
├──────────────────────────────────────────────────────────────┤
│ Id (PK)          │ int           │ Auto-increment             │
│ Name             │ string(100)   │ Required                   │
│ Description      │ string(500)   │ Optional                   │
│ CreatedAt        │ DateTime      │ UTC timestamp              │
│ UpdatedAt        │ DateTime      │ UTC timestamp              │
│ CreatedByUserId  │ int?          │ FK → Users.Id (nullable)   │
└──────────────────────────────────────────────────────────────┘
```

### Query Filters

- **Users**: Global filter `u => u.IsActive` excludes soft-deleted users
- **Admin**: Uses `IgnoreQueryFilters()` to access all users

### Indexes

| Table | Column(s) | Type |
|-------|-----------|------|
| Users | Email | Unique |

---

## Error Responses

| Status | Description |
|--------|-------------|
| 200 | Success |
| 201 | Created |
| 204 | No Content (successful delete) |
| 400 | Bad Request |
| 404 | Not Found |
| 409 | Conflict (duplicate) |
| 500 | Internal Server Error |

### Error Format

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "errors": {
    "Email": ["The Email field is required."]
  }
}
```

---

## Testing

### HTTP File

Use [Aspire-Full.Api.http](../../Aspire-Full.Api/Aspire-Full.Api.http) for VS Code REST Client testing.

### Unit Tests

```bash
dotnet test Aspire-Full.Tests.Unit
```

### E2E Tests

```bash
dotnet test Aspire-Full.Tests.E2E
```
