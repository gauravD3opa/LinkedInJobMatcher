# EXACT COMMANDS TO RUN

Copy-paste these commands in order to set up and run your refactored LinkedInJobMatcher.

## Prerequisites Check

Before running commands, ensure:
- ? .NET 8 SDK installed
- ? You're in the `LinkedInJobMatcher/` directory
- ? Ollama is running locally (`ollama serve`)

---

## Setup Commands (Run Once)

### 1. Install EF Core Packages

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.11
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.11
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Build Project

```bash
dotnet build
```

Expected output: "Build successful"

### 4. Create Initial Migration

```bash
dotnet ef migrations add InitialCreate
```

This creates the `Migrations/` folder with migration files.

### 5. Update Database

```bash
dotnet ef database update
```

Or skip this - it will auto-migrate on first run.

---

## Configuration (Do This Once)

### 1. Edit Configuration File

Edit `appsettings.Development.json` and ensure it has:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=Data/jobmatcher.db"
  },
  "Apify": {
    "ApiToken": "apify_api_YOUR_TOKEN"
  },
  "Gmail": {
    "FromAddress": "your-email@gmail.com"
  },
  "Google": {
    "ClientId": "your-id.apps.googleusercontent.com",
    "ClientSecret": "your-secret"
  },
  "Resume": {
    "PdfPath": "Data/your-resume.pdf"
  }
}
```

### 2. Create Resume Files

Place these files in the `Data/` folder:

```bash
Data/resume.txt      # Text version of your resume
Data/your-resume.pdf # PDF version of your resume
```

---

## Running Commands

### Quick Test (Fetch Only)

```bash
dotnet run -- fetch
```

This:
- Creates `Data/jobmatcher.db`
- Downloads posts from Apify
- Saves archive to `Output/apify-posts_*.json`

### Analyze Posts

After fetch completes, run:

```bash
dotnet run -- analyze
```

This:
- Reads posts from database
- Analyzes with Ollama
- Extracts recruiter emails
- Creates Recruiter records

**Note**: This will run Ollama for each post. Takes 10-30 seconds per post depending on post length.

### Send Gmail Drafts

After analyze completes, run:

```bash
dotnet run -- send
```

This:
- Acquires Google OAuth token (browser opens)
- Creates Gmail drafts for pending recruiters
- Saves log to `Output/drafts-log_*.json`

### Run All Stages at Once

```bash
dotnet run -- all
```

Runs: fetch ? analyze ? send (in order)

### One-Time Import (If You Have Old Data)

```bash
dotnet run -- import
```

This:
- Imports all `Data/apify-posts*.json` files as Posts
- Imports `Data/sent-recruiters.json` as Recruiters (Status=Drafted)

Only run this once if migrating from old system.

### Export Jobs Without Email

```bash
dotnet run -- export-no-email
```

This:
- Queries Posts with Status=RelevantNoEmail
- Exports to `Output/jobs_without_email.json`

---

## Monitoring Commands

### View Database Contents

```bash
sqlite3 Data/jobmatcher.db
```

Inside sqlite3, try:

```sql
-- Count posts by status
SELECT Status, COUNT(*) FROM Posts GROUP BY Status;

-- Count recruiters by status
SELECT Status, COUNT(*) FROM Recruiters GROUP BY Status;

-- Show recent recruiters
SELECT Email, Status, ContactedAt FROM Recruiters ORDER BY ContactedAt DESC LIMIT 10;

-- Exit
.quit
```

### View Output Files

```bash
# List generated files
ls -la Output/

# View draft log
cat Output/drafts-log_*.json

# View jobs without email
cat Output/jobs_without_email.json
```

### Check Database File Size

```bash
ls -lh Data/jobmatcher.db
```

---

## Troubleshooting Commands

### Clean and Rebuild

```bash
dotnet clean
dotnet build
```

### Reset Database (Caution!)

```bash
rm Data/jobmatcher.db
rm Data/jobmatcher.db-wal
rm Data/jobmatcher.db-shm
dotnet ef database update  # Recreates empty database
```

### Check Ollama Connection

```bash
curl http://localhost:11434/api/tags
```

### View Ollama Models

```bash
ollama list
```

### Force Pull Ollama Model

```bash
ollama pull qwen3.5:4b
```

---

## Migration Commands

### List Migrations

```bash
dotnet ef migrations list
```

### Add New Migration

```bash
dotnet ef migrations add DescriptionHere
```

### Revert Migration

```bash
dotnet ef database update PreviousMigrationName
```

### Remove Last Migration (Unapplied Only)

```bash
dotnet ef migrations remove
```

---

## Backup Commands

### Backup Database

```bash
cp Data/jobmatcher.db Data/jobmatcher.db.backup
```

### Restore Database

```bash
cp Data/jobmatcher.db.backup Data/jobmatcher.db
```

### Export Database to SQL

```bash
sqlite3 Data/jobmatcher.db ".dump" > Data/jobmatcher.sql
```

---

## Scheduling (Optional)

### Windows - Run Nightly

Create a batch file `run-job-matcher.bat`:

```batch
@echo off
cd C:\path\to\LinkedInJobMatcher
dotnet run -- all
```

Add to Task Scheduler to run nightly.

### Linux/Mac - Cron Job

Edit crontab:

```bash
crontab -e
```

Add line to run daily at 2 AM:

```bash
0 2 * * * cd /path/to/LinkedInJobMatcher && dotnet run -- all
```

---

## Step-by-Step Setup (Copy-Paste)

```bash
# 1. Navigate to project
cd LinkedInJobMatcher

# 2. Install packages
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.11
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.11

# 3. Restore and build
dotnet restore
dotnet build

# 4. Create migration
dotnet ef migrations add InitialCreate

# 5. Update database
dotnet ef database update

# 6. [Now edit appsettings.Development.json with your credentials]
# 7. [Now create Data/resume.txt and Data/your-resume.pdf]

# 8. Test fetch
dotnet run -- fetch

# 9. Test analyze
dotnet run -- analyze

# 10. Test send (requires Google OAuth)
dotnet run -- send

# Done! Future runs:
dotnet run -- all
```

---

## Common Issues & Fixes

### Issue: "dotnet: command not found"
**Fix**: Install .NET 8 SDK from https://dotnet.microsoft.com

### Issue: "Connection string 'Default' is missing"
**Fix**: Add ConnectionStrings section to appsettings.Development.json

### Issue: "Ollama connection refused"
**Fix**: Run `ollama serve` in another terminal

### Issue: "Database is locked"
**Fix**: 
```bash
rm Data/jobmatcher.db-wal
rm Data/jobmatcher.db-shm
```

### Issue: "Type or namespace name 'AppDbContext' could not be found"
**Fix**: Ensure all Data/*.cs files are created

---

## Verification Checklist

Run these to verify everything works:

```bash
# 1. Build succeeds
dotnet build
# Expected: "Build successful"

# 2. Database exists
ls -l Data/jobmatcher.db
# Expected: File exists

# 3. Tables created
sqlite3 Data/jobmatcher.db ".tables"
# Expected: PostEmails Posts Recruiters

# 4. Migrations present
dotnet ef migrations list
# Expected: InitialCreate listed
```

---

## Next Steps

1. ? Run setup commands above
2. ? Edit configuration
3. ? Add resume files
4. ? Run `dotnet run -- fetch`
5. ? Run `dotnet run -- analyze`
6. ? Run `dotnet run -- send`
7. ? Check Gmail for drafts

---

## Documentation

For more details, see:
- **QUICKSTART.md** - 5-minute overview
- **README.md** - Complete guide
- **MIGRATION_GUIDE.md** - Detailed setup
- **REFERENCE.md** - Command reference
- **DATABASE_REFERENCE.md** - SQL queries

---

**That's all the commands you need!**

Copy, paste, and run. ??
