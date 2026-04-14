# Claude Configuration

## Project Context

This is the **Synerixis** project, a .NET-based application with the following structure:

### Project Type
- **Language**: C# (.NET)
- **Framework**: .NET 8+ (assuming based on structure)
- **Architecture**: Clean Architecture / OniO Architecture
  - `Synerixis.Domain` - Domain layer
  - `Synerixis.Application` - Application layer  
  - `Synerixes.Infrastructure` - Infrastructure layer
  - `Synerixes.Api` - API layer

### Known Components
- Frontend: React-based frontend in `/frontend/` (使用 HBuilder 开发，支持多端生成)
- Backend: .NET 8 Web API
- Database: PostgreSQL / SQL Server (check Migrations folder)
- Docker: Docker Compose setup with Redis, PostgreSQL, etc.

### Key Features (from README)
- User profile management with role-based access control
- File upload/download with CDN integration
- Search functionality (Elasticsearch / Meilisearch)
- Chat functionality
- Notification system
- Email templates

## Coding Standards

### C# Coding Rules
1. Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
2. Use `CamelCase` for JSON serialization (`JsonSerializerOptions` configured)
3. Prefer interface-based programming
4. Use record types for DTOs and domain entities where appropriate
5. Follow SOLID principles
6. Use dependency injection (no `new` except for primitives)

### Code Style
- Prefer `var` when type is obvious
- Use `record` for immutable DTOs
- Use `abstract class` for entities with inheritance
- Keep methods focused and small (< 50 lines)
- Use `using` statements for IDisposable resources

## Common Commands

### .NET Build
```bash
dotnet build
dotnet test
dotnet run
```

### Docker
```bash
docker-compose up -d
docker-compose down
```

### Frontend
```bash
cd frontend && npm install && npm run dev
```

## Important Files

| File | Purpose |
|------|---------|
| `Synerixis.sln` | Solution file |
| `README.md` | Project documentation |
| `PROJECT_STRUCTURE.md` | Architecture documentation |
| `Migrations/` | Database migration scripts |
| `frontend/` | React frontend code |
| `packages/` | npm packages (search, notifications, etc.) |

## Environment Variables

The application uses environment variables for configuration:

- `DATABASE_URL` - Database connection string
- `REDIS_URL` - Redis connection string
- `JWT_SECRET` - JWT signing key
- `SMTP_HOST` / `SMTP_PORT` - Email server settings
- `CDN_ENDPOINT` - CDN endpoint for file uploads

## Security Notes

1. Never commit `.env` files or secrets
2. Use `.env.example` or `.env.local` for local development
3. API keys should be stored in environment variables
4. Ensure proper authentication/authorization in API endpoints

## Notes

- Check `fix_choose_login.py` and `fix_profile_index.py` for recent fixes
- See `MVP_IMPLEMENTATION_PLAN.md` for feature roadmap
- Business plan: `业务计划书.docx`
- Installation guide: `安装说明书.docx`
