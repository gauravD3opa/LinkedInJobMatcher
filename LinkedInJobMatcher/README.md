# LinkedIn Job Matcher

A .NET 8 console application that automates job hunting by fetching LinkedIn job posts via Apify, analyzing them with Ollama AI, and creating Gmail drafts for matching recruiters.

## Features

- **Multi-Stage Pipeline**: Independent commands for fetching, analyzing, and sending
- **State Persistence**: SQLite database tracks progress at each stage
- **Resumable Workflow**: Stop and resume at any point without duplicating work
- **AI-Powered Matching**: Uses Ollama to match jobs against your resume
- **Gmail Integration**: Automatically creates Gmail drafts with your resume attached
- **Email Extraction**: Extracts recruiter emails from job posts using AI + regex
- **Duplicate Prevention**: Tracks contacted recruiters to avoid re-sending

## Architecture

### Database Entities

- **Post**: LinkedIn job posts with status tracking and analysis results
- **Recruiter**: Recruiter contacts with draft creation status
- **PostEmail**: Junction table linking posts to recruiters (many-to-many relationship)

### Pipeline Stages

1. **Fetch**: Downloads posts from Apify API
2. **Analyze**: Uses Ollama to evaluate job relevance and extract recruiter emails
3. **Send**: Creates Gmail drafts for relevant recruiter contacts

Each stage is independent and can be run separately or in sequence.

## Prerequisites

### Required Services

- **Ollama** running locally on `http://localhost:11434` with the `qwen3.5:4b` model
- **Google OAuth** credentials (Gmail API access)
- **Apify** API token (LinkedIn post scraper)

### Setup Steps

1. **Install Ollama**
   ```bash
   # Visit https://ollama.ai and install
   ollama pull qwen3.5:4b
   ollama serve  # Run in background
   ```

2. **Google OAuth Setup**
   - Create a project in [Google Cloud Console](https://console.cloud.google.com)
   - Create OAuth 2.0 credentials (Desktop application)
   - Download credentials and add to `appsettings.Development.json`:
     ```json
     "Google": {
       "ClientId": "your-client-id.apps.googleusercontent.com",
       "ClientSecret": "your-client-secret"
     }
     ```

3. **Apify API Token**
   - Sign up at [Apify](https://apify.com)
   - Get your API token and add to `appsettings.Development.json`:
     ```json
     "Apify": {
       "ApiToken": "apify_api_..."
     }
     ```

## Configuration

Edit `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=Data/jobmatcher.db"
  },
  "Apify": {
    "ApiToken": "your-apify-token"
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

## File Structure

```
LinkedInJobMatcher/
??? Data/            # Database and data files
?   ??? jobmatcher.db      # SQLite database (auto-created)
?   ??? resume.txt  # Resume text for Ollama analysis
?   ??? your-resume.pdf    # Resume PDF for email attachment
?   ??? sent-recruiters.json      # Existing sent recruiters (for import)
?   ??? apify-posts*.json         # Historical Apify fetch archives
??? Output/          # Generated outputs
?   ??? apify-posts_{timestamp}.json
?   ??? drafts-log_{timestamp}.json
?   ??? jobs_without_email.json
??? Services/
?   ??? FetchStage.cs      # Apify fetch orchestration
?   ??? AnalyzeStage.cs    # Ollama analysis orchestration
?   ??? SendStage.cs       # Gmail draft creation orchestration
?   ??? ImportStage.cs     # One-time data import
?   ??? ExportStage.cs     # Export jobs without emails
?   ??? ApifyService.cs    # Apify API client
?   ??? OllamaService.cs   # Ollama AI client
?   ??? GmailAuthService.cs       # Google OAuth handler
?   ??? GmailDraftService.cs      # Gmail API client
?   ??? JobProcessor.cs           # Legacy processor (kept for reference)
?   ??? LoggingHandler.cs         # HTTP logging
??? Data/
?   ??? AppDbContext.cs    # EF Core database context
?   ??? Post.cs     # Post entity
?   ??? Recruiter.cs       # Recruiter entity
?   ??? PostEmail.cs       # PostEmail junction entity
?   ??? PostStatus.cs      # Post status enum
?   ??? RecruiterStatus.cs        # Recruiter status enum
??? Models/
?   ??? LinkedInPost.cs           # Apify response model
?   ??? JobAnalysis.cs     # Ollama response model
?   ??? DraftLogEntry.cs          # Draft log entry model
?   ??? ...other models
??? Program.cs       # Command-line entry point
??? LinkedInJobMatcher.csproj      # Project file
??? appsettings.Development.json   # Configuration
```

## Usage

### Creating the Database

The database is automatically created and migrated on first run:

```bash
dotnet run -- fetch
```

### Commands

#### Fetch Posts from Apify
```bash
dotnet run -- fetch
```
- Downloads posts from Apify and saves to database
- Archives raw JSON to `Output/apify-posts_{timestamp}.json`
- Skips posts already in database

#### Analyze Posts with Ollama
```bash
dotnet run -- analyze
```
- Analyzes unprocessed posts against your resume
- Extracts recruiter emails using AI + regex
- Creates recruiter records and PostEmail links
- Saves after each post (resumable)

#### Send Gmail Drafts
```bash
dotnet run -- send
```
- Creates Gmail drafts for pending recruiters
- Sets status to "Drafting" before draft creation
- Sets status to "Drafted" on success (idempotent)
- Warns about recruiters left in "Drafting" state
- Writes draft log to `Output/drafts-log_{timestamp}.json`

#### Run All Stages
```bash
dotnet run -- all
```
Runs: fetch ? analyze ? send (in order)

#### One-Time Import
```bash
dotnet run -- import
```
Imports existing data from JSON files:
- `Data/apify-posts*.json` ? Posts table
- `Data/sent-recruiters.json` ? Recruiters table (Status = Drafted)

Use this to migrate from the old system.

#### Export Jobs Without Email
```bash
dotnet run -- export-no-email
```
Exports relevant posts with no email found to `Output/jobs_without_email.json`

## Database Schema

### Post Table
- `PostId` (PK, string)
- `LinkedinUrl` (string)
- `AuthorName` (string)
- `Content` (text)
- `FetchedAt` (datetime)
- `Status` (string: Unprocessed | NotRelevant | RelevantWithEmail | RelevantNoEmail | Failed)
- `Reason` (string)
- `RetryCount` (int)
- `AnalyzedAt` (datetime)
- Index: `Post.Status`

### Recruiter Table
- `Id` (PK, int)
- `Email` (string, unique index)
- `Status` (string: Pending | Drafting | Drafted | Failed)
- `DraftId` (string)
- `ContactedAt` (datetime)
- `Error` (string)

### PostEmail Table (Junction)
- `PostId` (FK)
- `RecruiterId` (FK)
- PK: (PostId, RecruiterId)

## Status Workflow

### Post Status
```
Unprocessed ? NotRelevant
           ? RelevantWithEmail ? (linked to Recruiters)
    ? RelevantNoEmail
    ? Failed ? Unprocessed (retry, RetryCount < 3)
```

### Recruiter Status
```
Pending ? Drafting ? Drafted
       ? Failed
```

## Key Features

### Resumable Pipeline
- Each stage saves state after each item
- Crashes or interruptions are safe
- Resume by running the same command again

### Duplicate Prevention
- `seenThisRun` HashSet during analyze phase
- Database unique index on `Recruiter.Email`
- Check for "Drafting" state on send (manual Gmail check required)

### Email Normalization
- Emails are stored lowercase and trimmed
- AI emails + regex emails are deduplicated
- Invalid emails are filtered

### Retry Logic
- Failed posts can be retried (RetryCount < 3)
- Manual `update Post set Status='Unprocessed' where PostId='...'` to reset

## Troubleshooting

### Recruiters Stuck in "Drafting" State
If the send stage crashes mid-run, recruiters may be left in "Drafting" state.

**Manual Fix**:
1. Check Gmail to see which drafts were actually created
2. Query the database:
   ```sql
   SELECT * FROM Recruiters WHERE Status = 'Drafting';
   ```
3. Update their status based on whether the draft exists:
   ```sql
   -- If draft exists
   UPDATE Recruiters SET Status = 'Drafted' WHERE Id = 1;
   
   -- If draft doesn't exist
   UPDATE Recruiters SET Status = 'Pending' WHERE Id = 1;
   ```

### Ollama Connection Issues
- Ensure Ollama is running: `ollama serve`
- Check it's accessible: `curl http://localhost:11434/api/tags`
- Verify model is installed: `ollama list`

### Google OAuth Issues
- Ensure redirect URI matches: `http://localhost:1179/callback`
- Check OAuth app is authorized in Google Cloud Console
- Clear browser cache if getting "redirect_uri_mismatch"

### Apify Issues
- Verify API token is valid
- Check Apify account has API credits
- Monitor Apify Actor runs in your dashboard

## Development

### Building
```bash
dotnet build
```

### Running Tests
Currently no unit tests. Consider adding:
- OllamaService analysis tests
- EmailRegex validation tests
- Database entity tests

### Adding Migrations
If you modify `AppDbContext`:
```bash
dotnet ef migrations add YourMigrationName
dotnet ef database update
```

## Performance Notes

- **Ollama Analysis**: ~10-30 seconds per post (depends on model and post length)
- **Gmail Draft Creation**: ~2-5 seconds per draft
- **Database Queries**: Very fast (SQLite is sufficient for this workload)

For 200 posts:
- Fetch: ~30 seconds
- Analyze: ~1-2 hours (can be parallelized in future)
- Send: ~5-30 minutes depending on recruiter count

## Future Enhancements

- [ ] Parallel Ollama processing
- [ ] Web UI for monitoring
- [ ] Email templates per company
- [ ] Follow-up email scheduling
- [ ] Job description caching
- [ ] Analytics dashboard
- [ ] Slack notifications

## License

This project is for personal use.

## Support

For issues or questions:
1. Check the Troubleshooting section
2. Review the database state
3. Check logs in Output/

---

**Built with**: .NET 8 | EF Core | SQLite | Ollama | Gmail API | Apify
