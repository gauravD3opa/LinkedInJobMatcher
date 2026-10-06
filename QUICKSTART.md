# Quick Start Guide

Get up and running with LinkedInJobMatcher in 5 minutes.

## Prerequisites

Make sure you have:
- ? .NET 8 SDK installed
- ? Ollama running locally with qwen3.5:4b model
- ? Google OAuth credentials for Gmail API
- ? Apify API token

## Step 1: Configure (2 minutes)

Edit `appsettings.Development.json`:

```json
{
  "Apify": {
    "ApiToken": "apify_api_YOUR_TOKEN_HERE"
  },
  "Gmail": {
    "FromAddress": "your-email@gmail.com"
  },
  "Google": {
    "ClientId": "your-id.apps.googleusercontent.com",
    "ClientSecret": "your-secret"
  }
}
```

Place your files in `Data/`:
- `resume.txt` - Your resume text
- `your-resume.pdf` - Your resume PDF

## Step 2: Create Database (1 minute)

```bash
cd LinkedInJobMatcher
dotnet build
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Or skip this and let it auto-migrate:
```bash
dotnet run -- fetch  # Auto-creates database
```

## Step 3: Run a Test Fetch (1 minute)

```bash
dotnet run -- fetch
```

This downloads LinkedIn posts from Apify and saves them to the database.

**Output**: 
- `Data/jobmatcher.db` - Created ?
- `Output/apify-posts_{timestamp}.json` - Archive ?

## Step 4: Analyze Posts (Optional, depends on post count)

```bash
dotnet run -- analyze
```

This analyzes posts with Ollama and extracts recruiter emails.

**Output**:
- Posts marked as Relevant/NotRelevant/Failed
- Recruiters created with Status=Pending

## Step 5: Send Drafts (Requires Gmail OAuth)

```bash
dotnet run -- send
```

Creates Gmail drafts for pending recruiters.

**Output**:
- `Output/drafts-log_{timestamp}.json` - Sent/failed status
- Gmail drafts created (check Gmail drafts folder)

## Run All at Once

```bash
dotnet run -- all
```

Runs: fetch ? analyze ? send (in order)

## Common Commands

```bash
# View all available commands
dotnet run

# Fetch from Apify
dotnet run -- fetch

# Analyze with Ollama
dotnet run -- analyze

# Send Gmail drafts
dotnet run -- send

# Do everything
dotnet run -- all

# Migrate old data from JSON
dotnet run -- import

# Export jobs without email
dotnet run -- export-no-email
```

## Checking Progress

### View database contents:
```bash
sqlite3 Data/jobmatcher.db
sqlite> SELECT Status, COUNT(*) FROM Posts GROUP BY Status;
sqlite> SELECT Status, COUNT(*) FROM Recruiters GROUP BY Status;
```

### Check output folder:
```bash
ls -la Output/
# Should contain:
# - apify-posts_*.json (archive)
# - drafts-log_*.json (results)
# - jobs_without_email.json (optional)
```

## Resuming Interrupted Runs

If a stage is interrupted:
```bash
# Same command continues where it left off
dotnet run -- analyze
```

All progress is saved in the database.

## Troubleshooting

### "Connection string 'Default' is missing"
? Check `appsettings.Development.json` has `ConnectionStrings` section

### "Ollama connection failed"
? Run `ollama serve` in another terminal

### "Google OAuth failed"
? Check credentials in `appsettings.Development.json`

### "No pending recruiters"
? Run `dotnet run -- analyze` first to extract emails

## Full Documentation

- **README.md** - Complete guide
- **MIGRATION_GUIDE.md** - Detailed setup instructions
- **IMPLEMENTATION_SUMMARY.md** - Technical details

## Next Steps

1. ? Configure credentials
2. ? Place resume files
3. ? Run `dotnet run -- fetch`
4. ? Check `Data/jobmatcher.db`
5. ? Run `dotnet run -- analyze`
6. ? Run `dotnet run -- send`

---

**That's it!** Your job matcher is now running independently at each stage. ??
