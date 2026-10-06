# Deployment & Setup Checklist

Use this checklist to ensure everything is properly configured before running the application.

## Pre-Setup Checklist

- [ ] .NET 8 SDK installed (`dotnet --version`)
- [ ] Ollama installed and running (`ollama serve`)
- [ ] Ollama model installed (`ollama list` shows qwen3.5:4b)
- [ ] Google OAuth credentials obtained
- [ ] Apify API token obtained
- [ ] Resume files ready (resume.txt and PDF)

## Configuration Checklist

### 1. Dependencies
- [ ] Run: `dotnet add package Microsoft.EntityFrameworkCore.Sqlite`
- [ ] Run: `dotnet add package Microsoft.EntityFrameworkCore.Design`
- [ ] Run: `dotnet build` (successful)

### 2. Environment Configuration
- [ ] Edit `appsettings.Development.json`
- [ ] Add Apify API token:
  ```json
  "Apify": {
    "ApiToken": "apify_api_..."
  }
  ```
- [ ] Add Google OAuth credentials:
  ```json
  "Google": {
    "ClientId": "...",
    "ClientSecret": "..."
  }
  ```
- [ ] Add Gmail address:
  ```json
  "Gmail": {
    "FromAddress": "your-email@gmail.com"
  }
  ```
- [ ] Add connection string (should already be there):
  ```json
  "ConnectionStrings": {
    "Default": "Data Source=Data/jobmatcher.db"
  }
  ```

### 3. Resume Files
- [ ] Create `Data/resume.txt` (text version of resume)
- [ ] Create `Data/your-resume.pdf` (PDF version of resume)
- [ ] Or update `appsettings.Development.json` with custom PDF path:
  ```json
  "Resume": {
    "PdfPath": "Path/to/your/resume.pdf"
  }
  ```

### 4. Directories
- [ ] Ensure `Data/` folder exists (auto-created but can pre-create)
- [ ] Ensure `Output/` folder exists (auto-created but can pre-create)
- [ ] Ensure `Migrations/` folder exists (will be created by EF)

## Database Setup Checklist

- [ ] Run: `dotnet ef migrations add InitialCreate`
- [ ] Run: `dotnet ef database update`
- [ ] Or let it auto-migrate: run `dotnet run -- fetch` once
- [ ] Verify: Check `Data/jobmatcher.db` exists

## Service Verification Checklist

### Ollama
- [ ] Run `ollama serve` in background/terminal
- [ ] Verify: `curl http://localhost:11434/api/tags`
- [ ] Check: Model `qwen3.5:4b` is listed
- [ ] If not installed: `ollama pull qwen3.5:4b`

### Google OAuth
- [ ] Visit [Google Cloud Console](https://console.cloud.google.com)
- [ ] Create project
- [ ] Enable Gmail API
- [ ] Create OAuth 2.0 credential (Desktop app)
- [ ] Add redirect URI: `http://localhost:1179/callback`
- [ ] Download JSON credentials
- [ ] Copy ClientId and ClientSecret to `appsettings.Development.json`

### Apify
- [ ] Sign up at [Apify](https://apify.com)
- [ ] Get API token from account settings
- [ ] Copy token to `appsettings.Development.json`
- [ ] Verify: Account has API credits

## Testing Checklist

### Build Test
- [ ] Run: `dotnet build`
- [ ] No errors (warnings OK)

### Fetch Test
- [ ] Run: `dotnet run -- fetch`
- [ ] Check: `Data/jobmatcher.db` created
- [ ] Check: `Output/apify-posts_*.json` created
- [ ] Check: Console shows "X new posts added"

### Analyze Test
- [ ] Run: `dotnet run -- analyze`
- [ ] Ollama should start processing
- [ ] Check: Console shows "Analyzing X posts"
- [ ] Check: Post statuses updated in database

### Send Test (Optional)
- [ ] Run: `dotnet run -- send`
- [ ] Browser should open for Google OAuth
- [ ] Check: `Output/drafts-log_*.json` created
- [ ] Check: Gmail drafts folder has new drafts

## Post-Setup Checklist

### Data Archiving
- [ ] Backup: `cp Data/jobmatcher.db Data/jobmatcher.db.backup`
- [ ] Archive: Move any old Apify JSON files to `Data/` for import

### Import Old Data (If Applicable)
- [ ] Place `sent-recruiters.json` in `Data/` folder
- [ ] Place `apify-posts*.json` files in `Data/` folder
- [ ] Run: `dotnet run -- import`
- [ ] Verify: Existing recruiters and posts imported

### Scheduling (Optional)
- [ ] Consider setting up recurring task:
  - Windows: Task Scheduler (run `dotnet run -- all`)
  - Linux/Mac: Cron (add to crontab)
- [ ] Set frequency (daily/weekly)
- [ ] Test: Verify it runs automatically

### Monitoring Setup (Optional)
- [ ] Create monitoring script:
  ```bash
  sqlite3 Data/jobmatcher.db "SELECT Status, COUNT(*) FROM Posts GROUP BY Status;"
  ```
- [ ] Set up alerts if needed

## Troubleshooting Checklist

### Build Fails
- [ ] Verify .NET 8: `dotnet --version`
- [ ] Clean: `dotnet clean && dotnet build`
- [ ] Restore: `dotnet restore`
- [ ] Check: All files created in correct locations

### Database Issues
- [ ] Delete and recreate: `rm Data/jobmatcher.db*`
- [ ] Run: `dotnet ef database update`
- [ ] Verify: `sqlite3 Data/jobmatcher.db ".tables"`

### Ollama Connection Fails
- [ ] Check: `ollama serve` is running
- [ ] Verify: `curl http://localhost:11434/api/tags`
- [ ] Model: `ollama list | grep qwen3.5:4b`
- [ ] Reinstall: `ollama pull qwen3.5:4b`

### Google OAuth Fails
- [ ] Verify credentials in `appsettings.Development.json`
- [ ] Check: Redirect URI matches (`http://localhost:1179/callback`)
- [ ] Test: Can access Google OAuth consent screen directly
- [ ] Clear: Browser cache/cookies

### Apify Issues
- [ ] Check: API token is valid
- [ ] Verify: Account has API credits
- [ ] Test: API directly via curl
- [ ] Monitor: Apify dashboard

### Database Locked
- [ ] Close all instances
- [ ] Delete temporary files: `rm Data/jobmatcher.db-wal Data/jobmatcher.db-shm`
- [ ] Restart application

## Running Checklist

### Before Running
- [ ] All checklist items above completed
- [ ] `appsettings.Development.json` configured
- [ ] `resume.txt` and PDF in place
- [ ] Services running (Ollama, etc.)
- [ ] Network access available

### Running Commands
- [ ] First run: `dotnet run -- fetch`
- [ ] Then: `dotnet run -- analyze`
- [ ] Finally: `dotnet run -- send`
- [ ] Or all at once: `dotnet run -- all`

### After Running
- [ ] Check console output for errors
- [ ] Verify output files created
- [ ] Check Gmail drafts folder
- [ ] Query database for results
- [ ] Review logs in Output folder

## Maintenance Checklist

### Regular (After Each Run)
- [ ] Check for new posts in database
- [ ] Monitor disk space for database
- [ ] Review draft log for errors
- [ ] Check Gmail for drafts

### Weekly
- [ ] Backup database: `cp Data/jobmatcher.db Data/jobmatcher.db.weekly`
- [ ] Review failed posts: Check database for Status='Failed'
- [ ] Check recruiter contacts: View Drafted recruiters

### Monthly
- [ ] Archive old output files
- [ ] Run database optimization: `sqlite3 Data/jobmatcher.db "VACUUM;"`
- [ ] Review Ollama performance
- [ ] Check Apify API usage

## Rollback Procedure

If something goes wrong:

1. **Stop** all running instances
2. **Restore** database:
   ```bash
   cp Data/jobmatcher.db.backup Data/jobmatcher.db
   ```
3. **Clean** temporary files:
   ```bash
   rm Data/jobmatcher.db-wal Data/jobmatcher.db-shm
   ```
4. **Verify** database:
   ```bash
   sqlite3 Data/jobmatcher.db ".tables"
   ```
5. **Re-test** stages

## Sign-Off

- [ ] All checklist items verified
- [ ] Services confirmed running
- [ ] Database created and tested
- [ ] First fetch successful
- [ ] Ready for production use

---

## Quick Reference Commands

```bash
# Setup
dotnet build
dotnet ef migrations add InitialCreate
dotnet ef database update

# Run
dotnet run -- fetch
dotnet run -- analyze
dotnet run -- send
dotnet run -- all

# Monitor
sqlite3 Data/jobmatcher.db "SELECT Status, COUNT(*) FROM Posts GROUP BY Status;"

# Backup
cp Data/jobmatcher.db Data/jobmatcher.db.backup

# Clean
rm Data/jobmatcher.db-wal Data/jobmatcher.db-shm
```

---

**Date Completed**: _______________  
**Verified By**: _______________  
**Notes**: _______________

