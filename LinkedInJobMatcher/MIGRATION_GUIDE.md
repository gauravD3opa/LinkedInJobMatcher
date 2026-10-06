# Migration Guide & Setup Instructions

This document contains the exact steps to set up and run the refactored LinkedInJobMatcher with SQLite and independent pipeline stages.

## Files Changed or Created

### New Files Created

**Data Layer (Database Context & Entities)**
- `LinkedInJobMatcher/Data/AppDbContext.cs` - EF Core database context
- `LinkedInJobMatcher/Data/AppDbContextFactory.cs` - Factory for EF Core tooling
- `LinkedInJobMatcher/Data/Post.cs` - Post entity
- `LinkedInJobMatcher/Data/Recruiter.cs` - Recruiter entity
- `LinkedInJobMatcher/Data/PostEmail.cs` - PostEmail junction entity
- `LinkedInJobMatcher/Data/PostStatus.cs` - Post status enum
- `LinkedInJobMatcher/Data/RecruiterStatus.cs` - Recruiter status enum

**Stage Services**
- `LinkedInJobMatcher/Services/FetchStage.cs` - Fetch from Apify
- `LinkedInJobMatcher/Services/AnalyzeStage.cs` - Analyze with Ollama
- `LinkedInJobMatcher/Services/SendStage.cs` - Send Gmail drafts
- `LinkedInJobMatcher/Services/ImportStage.cs` - Import from JSON
- `LinkedInJobMatcher/Services/ExportStage.cs` - Export jobs without email

**Documentation**
- `LinkedInJobMatcher/README.md` - Full project documentation
- `MIGRATION_GUIDE.md` - This file

### Modified Files

- `LinkedInJobMatcher/LinkedInJobMatcher.csproj` - Added EF Core packages
- `LinkedInJobMatcher/appsettings.Development.json` - Added connection string
- `LinkedInJobMatcher/Program.cs` - Complete rewrite with command-based architecture
- `.gitignore` - Already contains `*.db`, `*.db-shm`, `*.db-wal`

### Files to Keep (No Changes)

- All service classes: `ApifyService`, `OllamaService`, `GmailAuthService`, `GmailDraftService`
- All model classes: `LinkedInPost`, `JobAnalysis`, `Author`, `DraftLogEntry`, `JobWithoutEmail`
- Configuration files: `appsettings.json`

## Step 1: Add NuGet Packages

```bash
cd LinkedInJobMatcher
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.11
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.11
```

Or edit `LinkedInJobMatcher.csproj` manually and add:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.11" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.11" />
```

Restore packages:
```bash
dotnet restore
```

## Step 2: Update Configuration

Add connection string to `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=Data/jobmatcher.db"
  },
  // ... rest of your config
}
```

Full file should look like:
```json
{
  "exclude": [
    "**/bin",
    "**/bower_components",
    "**/jspm_packages",
    "**/node_modules",
    "**/obj",
    "**/platforms"
  ],

  "ConnectionStrings": {
    "Default": "Data Source=Data/jobmatcher.db"
  },

  "Apify": {
    "ApiToken": "apify_api_..."
  },
  "Gmail": {
    "FromAddress": "your-email@gmail.com"
  },
  "Google": {
    "ClientId": "your-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-client-secret"
  },
  "Resume": {
    "PdfPath": "Data/your-resume.pdf"
  }
}
```

## Step 3: Create and Run Initial Migration

### Option A: Using dotnet ef CLI (Recommended)

Install EF Core tools if not already installed:
```bash
dotnet tool install --global dotnet-ef
```

Create the initial migration:
```bash
cd LinkedInJobMatcher
dotnet ef migrations add InitialCreate
```

This creates `LinkedInJobMatcher/Migrations/` folder with migration files.

### Option B: Auto-Migration on First Run

The `Program.cs` now calls `await dbContext.Database.MigrateAsync();` automatically, so if migrations are applied, the database will be created on first run.

If you prefer to pre-create the migration:
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Step 4: Build and Test

```bash
dotnet build
```

If the build is successful, you're ready to run commands!

## Step 5: Run the Application

The application now uses command-line arguments. The default command is "all".

### Fetch Only
```bash
dotnet run -- fetch
```

### Analyze Only
```bash
dotnet run -- analyze
```

### Send Only (requires OAuth)
```bash
dotnet run -- send
```

### Run All Stages
```bash
dotnet run -- all
```

Or simply:
```bash
dotnet run
```

### One-Time Import (Migrate from Old System)
```bash
dotnet run -- import
```

This imports:
- All posts from `Data/apify-posts*.json` ? Posts table
- All recruiters from `Data/sent-recruiters.json` ? Recruiters table (marked as Drafted)

Run this once after first setup to migrate existing data.

### Export Jobs Without Email
```bash
dotnet run -- export-no-email
```

## Directory Structure After Setup

```
Data/
??? jobmatcher.db       # Auto-created on first run
??? jobmatcher.db-shm   # SQLite temporary file
??? jobmatcher.db-wal   # SQLite write-ahead log
??? resume.txt   # Your resume text
??? your-resume.pdf     # Your resume PDF
??? sent-recruiters.json       # (Optional) For import
??? apify-posts*.json          # (Optional) Historical fetches

Output/
??? apify-posts_2025-01-15_14-30-45.json    # Fetch archive
??? drafts-log_2025-01-15_14-35-22.json     # Draft log
??? jobs_without_email.json         # Export

Migrations/
??? 20250115000000_InitialCreate.cs        # First migration
??? 20250115000000_InitialCreate.Designer.cs
```

## Database Migrations

### View Pending Migrations
```bash
dotnet ef migrations list
```

### Add a New Migration
After modifying `AppDbContext`, create a new migration:
```bash
dotnet ef migrations add DescriptionOfChanges
```

### Apply Migrations
```bash
dotnet ef database update
```

### Revert to Previous Migration
```bash
dotnet ef database update PreviousMigrationName
```

### Remove Latest Migration (Unapplied)
```bash
dotnet ef migrations remove
```

## Troubleshooting

### Issue: "Connection string 'Default' is missing"
**Solution**: Make sure `appsettings.Development.json` has the ConnectionStrings section.

### Issue: "Type or namespace name 'AppDbContext' could not be found"
**Solution**: Make sure you've created all files in the `LinkedInJobMatcher/Data/` folder.

### Issue: "Cannot open database file"
**Solution**: Ensure `Data/` folder exists and the app has write permissions.

### Issue: Migrations not found
**Solution**: Run `dotnet ef migrations add InitialCreate` to create the Migrations folder.

### Issue: Database is locked
**Solution**: Another instance may be running. Close all instances and try again. The `*.db-wal` file indicates an active session.

## Rollback to Old Code

If you need to revert, the old `JobProcessor.cs` is still available for reference:

```csharp
// Old usage (no longer used, but kept for reference)
var processor = new JobProcessor(ollamaService, gmailDraftService);
await processor.ProcessAsync(
    posts,
    resumeTextPath,
    accessToken,
    fromAddress,
    draftLogPath,
    resumePdfPath,
    commonSubject,
    commonBody,
    sentStore);
```

However, the new stage-based approach is recommended.

## Key Differences from Old Code

| Aspect | Old Code | New Code |
|--------|----------|----------|
| **Execution** | Single monolithic run | Independent commands |
| **State Persistence** | JSON file (`sent-recruiters.json`) | SQLite database |
| **Resume Capability** | Limited (file-based) | Full (database checkpoints) |
| **Post Re-analysis** | Every run | Only unprocessed posts |
| **Email Extraction** | After posts loaded | Immediately after analysis |
| **Gmail Draft Log** | Single JSON file | Timestamped JSON file |
| **Error Handling** | Limited retry | Retry count tracking |
| **Duplicate Prevention** | In-memory HashSet | Database unique index + HashSet |

## Verifying the Setup

### Test 1: Database Creation
```bash
dotnet run -- fetch
```
Should create `Data/jobmatcher.db` and show migration output.

### Test 2: Database Contents
```bash
# Using sqlite3 CLI (if installed)
sqlite3 Data/jobmatcher.db
sqlite> SELECT COUNT(*) FROM Posts;
sqlite> SELECT COUNT(*) FROM Recruiters;
sqlite> .tables
```

### Test 3: Help Message
```bash
dotnet run -- help
# or
dotnet run
# (no valid command)
```

Should show available commands.

## Performance Considerations

- **Database**: SQLite is fast enough for this workload (< 1000 posts, < 200 recruiters)
- **Indexing**: `Post.Status` is indexed for quick Unprocessed lookups
- **Batch Operations**: Each stage saves after processing to enable resumption

For future scaling:
- Consider PostgreSQL or SQL Server if posts > 10,000
- Add logging database for audit trail
- Implement parallel Ollama processing

## Monitoring

Check database state:
```sql
-- Count posts by status
SELECT Status, COUNT(*) FROM Posts GROUP BY Status;

-- Count recruiters by status
SELECT Status, COUNT(*) FROM Recruiters GROUP BY Status;

-- Find problematic posts
SELECT PostId, Status, RetryCount, Reason FROM Posts WHERE Status = 'Failed';

-- List contacted recruiters
SELECT Email, Status, ContactedAt FROM Recruiters WHERE Status = 'Drafted' ORDER BY ContactedAt DESC;
```

## Next Steps

1. ? Add packages and update configuration
2. ? Create initial migration
3. ? Run `dotnet run -- import` to migrate old data (if applicable)
4. ? Run `dotnet run -- all` for a full pipeline execution
5. Monitor `Data/jobmatcher.db` and `Output/` folder for results
6. Adjust Ollama prompt in `OllamaService.cs` if needed
7. Consider setting up a scheduled task (Windows Task Scheduler / cron) to run periodically

## Commands Reference

```bash
# Setup
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet ef migrations add InitialCreate

# Run stages
dotnet run -- fetch    # Fetch from Apify
dotnet run -- analyze         # Analyze with Ollama
dotnet run -- send     # Create Gmail drafts
dotnet run -- all      # Run all stages
dotnet run -- import   # Import from JSON
dotnet run -- export-no-email # Export jobs without email

# Database
dotnet ef database update
dotnet ef migrations list
dotnet ef migrations add MyMigration
dotnet ef database update --verbose
```

---

**Setup Complete!** You now have a fully refactored job matcher with SQLite persistence and independent pipeline stages.
