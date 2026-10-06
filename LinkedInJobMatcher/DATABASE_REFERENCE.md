# Database Reference & Queries

This document contains useful SQL queries for monitoring and debugging the LinkedIn Job Matcher database.

## Database File

```
Data/jobmatcher.db
```

Access with:
```bash
sqlite3 Data/jobmatcher.db
```

## Schema Overview

### Posts Table
```sql
CREATE TABLE Posts (
    PostId TEXT PRIMARY KEY,
    LinkedinUrl TEXT,
    AuthorName TEXT,
    Content TEXT,
    FetchedAt DATETIME,
    Status TEXT,  -- Unprocessed, NotRelevant, RelevantWithEmail, RelevantNoEmail, Failed
    Reason TEXT,
    RetryCount INTEGER,
    AnalyzedAt DATETIME
);

CREATE INDEX ix_Posts_Status ON Posts(Status);
```

### Recruiters Table
```sql
CREATE TABLE Recruiters (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Email TEXT UNIQUE,
    Status TEXT,  -- Pending, Drafting, Drafted, Failed
    DraftId TEXT,
    ContactedAt DATETIME,
    Error TEXT
);

CREATE UNIQUE INDEX ix_Recruiters_Email ON Recruiters(Email);
```

### PostEmails Table
```sql
CREATE TABLE PostEmails (
    PostId TEXT NOT NULL,
    RecruiterId INTEGER NOT NULL,
    PRIMARY KEY (PostId, RecruiterId),
    FOREIGN KEY (PostId) REFERENCES Posts(PostId),
    FOREIGN KEY (RecruiterId) REFERENCES Recruiters(Id)
);
```

## Monitoring Queries

### Dashboard: Pipeline Progress
```sql
-- Count posts by status
SELECT 
    Status,
    COUNT(*) as Count
FROM Posts
GROUP BY Status
ORDER BY Status;
```

Output example:
```
Status        | Count
NotRelevant          | 45
Failed        | 5
RelevantNoEmail      | 8
RelevantWithEmail    | 35
Unprocessed          | 107
```

### Dashboard: Recruiter Status
```sql
SELECT 
    Status,
    COUNT(*) as Count
FROM Recruiters
GROUP BY Status
ORDER BY Status;
```

Output example:
```
Status      | Count
Drafted     | 32
Failed      | 3
Pending     | 0
```

### Dashboard: Jobs Matching
```sql
-- Show job posting success rate
SELECT
    'Total Posts' as Metric,
    COUNT(*) as Value
FROM Posts
UNION ALL
SELECT 'Relevant With Email', COUNT(*) FROM Posts WHERE Status = 'RelevantWithEmail'
UNION ALL
SELECT 'Relevant No Email', COUNT(*) FROM Posts WHERE Status = 'RelevantNoEmail'
UNION ALL
SELECT 'Not Relevant', COUNT(*) FROM Posts WHERE Status = 'NotRelevant'
UNION ALL
SELECT 'Failed', COUNT(*) FROM Posts WHERE Status = 'Failed'
UNION ALL
SELECT 'Success Rate (%)', 
    ROUND(CAST(COUNT(CASE WHEN Status IN ('RelevantWithEmail', 'RelevantNoEmail') THEN 1 END) AS FLOAT) 
          / NULLIF(COUNT(*), 0) * 100, 2)
FROM Posts;
```

## Investigation Queries

### Find Failed Posts
```sql
SELECT PostId, Reason, RetryCount, AnalyzedAt
FROM Posts
WHERE Status = 'Failed'
ORDER BY AnalyzedAt DESC;
```

### Find Posts Stuck in Retry
```sql
SELECT PostId, RetryCount, Reason, Content
FROM Posts
WHERE Status = 'Failed' AND RetryCount >= 2
ORDER BY AnalyzedAt DESC;
```

### Find Recruiters with Errors
```sql
SELECT Id, Email, Status, Error, ContactedAt
FROM Recruiters
WHERE Status = 'Failed'
ORDER BY ContactedAt DESC;
```

### Find Recruiters Stuck in Drafting
```sql
-- Indicates a crashed run
SELECT Id, Email, DraftId, ContactedAt
FROM Recruiters
WHERE Status = 'Drafting'
ORDER BY ContactedAt DESC;
```

### Find Recently Contacted Recruiters
```sql
SELECT Id, Email, Status, DraftId, ContactedAt
FROM Recruiters
WHERE Status IN ('Drafted', 'Failed')
ORDER BY ContactedAt DESC
LIMIT 20;
```

## Analysis Queries

### Posts by Author
```sql
SELECT AuthorName, COUNT(*) as PostCount
FROM Posts
GROUP BY AuthorName
ORDER BY PostCount DESC
LIMIT 20;
```

### Average Posts per Recruiter
```sql
SELECT 
    COUNT(*) as Total,
    AVG(PostCount) as Average
FROM (
    SELECT RecruiterId, COUNT(*) as PostCount
    FROM PostEmails
    GROUP BY RecruiterId
);
```

### Recruiters with Most Posts
```sql
SELECT 
    r.Email,
    r.Status,
    COUNT(pe.PostId) as PostCount
FROM Recruiters r
LEFT JOIN PostEmails pe ON r.Id = pe.RecruiterId
GROUP BY r.Id
ORDER BY PostCount DESC
LIMIT 20;
```

### Posts with No Recruiters
```sql
SELECT PostId, LinkedinUrl, AuthorName, Status
FROM Posts
WHERE PostId NOT IN (SELECT DISTINCT PostId FROM PostEmails)
ORDER BY FetchedAt DESC;
```

### Processing Time Analysis
```sql
SELECT 
    COUNT(*) as TotalProcessed,
    CAST(AVG(CAST((julianday(AnalyzedAt) - julianday(FetchedAt)) * 24 * 60 AS FLOAT)) AS INT) as AvgMinutes
FROM Posts
WHERE AnalyzedAt IS NOT NULL;
```

## Data Modification Queries

### Reset a Failed Post for Retry
```sql
UPDATE Posts
SET Status = 'Unprocessed', RetryCount = 0, AnalyzedAt = NULL
WHERE PostId = 'your-post-id';
```

### Manually Mark Recruiter as Drafted
```sql
UPDATE Recruiters
SET Status = 'Drafted', ContactedAt = datetime('now')
WHERE Email = 'recruiter@example.com';
```

### Fix Recruiter Stuck in Drafting
```sql
-- After manually checking Gmail and confirming draft exists
UPDATE Recruiters
SET Status = 'Drafted'
WHERE Status = 'Drafting' AND Email = 'recruiter@example.com';

-- Or if draft doesn't exist, reset to pending
UPDATE Recruiters
SET Status = 'Pending'
WHERE Status = 'Drafting' AND Email = 'recruiter@example.com';
```

### Delete a Post and Its Links
```sql
DELETE FROM PostEmails WHERE PostId = 'your-post-id';
DELETE FROM Posts WHERE PostId = 'your-post-id';
```

### Clear All Pending Recruiters (CAUTION!)
```sql
DELETE FROM PostEmails 
WHERE RecruiterId IN (SELECT Id FROM Recruiters WHERE Status = 'Pending');

DELETE FROM Recruiters 
WHERE Status = 'Pending';
```

## Backup & Restore

### Backup Database
```bash
cp Data/jobmatcher.db Data/jobmatcher.db.backup
```

### Restore from Backup
```bash
cp Data/jobmatcher.db.backup Data/jobmatcher.db
```

### Export to CSV
```bash
sqlite3 Data/jobmatcher.db ".mode csv" ".output posts.csv" "SELECT * FROM Posts;"
sqlite3 Data/jobmatcher.db ".mode csv" ".output recruiters.csv" "SELECT * FROM Recruiters;"
```

### Import from CSV
```bash
sqlite3 Data/jobmatcher.db ".mode csv" ".import posts.csv Posts"
```

## Performance Queries

### Database Size
```bash
ls -lh Data/jobmatcher.db
```

### Largest Posts (by Content Length)
```sql
SELECT PostId, LENGTH(Content) as ContentLength, AuthorName
FROM Posts
ORDER BY ContentLength DESC
LIMIT 10;
```

### Slow Analysis (if timestamps are tracked)
```sql
SELECT PostId, 
       CAST((julianday(AnalyzedAt) - julianday(FetchedAt)) * 24 * 60 * 60 AS INT) as SecondsToAnalyze
FROM Posts
WHERE AnalyzedAt IS NOT NULL
ORDER BY SecondsToAnalyze DESC
LIMIT 10;
```

## Maintenance

### Optimize Database
```bash
sqlite3 Data/jobmatcher.db "VACUUM;"
```

### Analyze Query Performance
```bash
sqlite3 Data/jobmatcher.db "ANALYZE;"
```

### Check Database Integrity
```bash
sqlite3 Data/jobmatcher.db "PRAGMA integrity_check;"
```

### View Query Execution Plan
```bash
sqlite3 Data/jobmatcher.db "EXPLAIN QUERY PLAN SELECT * FROM Posts WHERE Status = 'Unprocessed';"
```

## Common Issues & Fixes

### Issue: Database is Locked
**Cause**: Another instance is running or file handle not closed

```bash
# Force unlock
rm Data/jobmatcher.db-wal
rm Data/jobmatcher.db-shm
```

### Issue: Duplicate Recruiters
**Prevention**: Email is unique indexed, so duplicates shouldn't occur.

```sql
-- Check for case sensitivity issues
SELECT Email, COUNT(*) FROM Recruiters GROUP BY Email HAVING COUNT(*) > 1;
```

### Issue: Orphaned PostEmails
**Check for orphans**:
```sql
SELECT pe.* FROM PostEmails pe
WHERE pe.PostId NOT IN (SELECT PostId FROM Posts)
   OR pe.RecruiterId NOT IN (SELECT Id FROM Recruiters);
```

**Delete orphans**:
```sql
DELETE FROM PostEmails
WHERE PostId NOT IN (SELECT PostId FROM Posts)
   OR RecruiterId NOT IN (SELECT Id FROM Recruiters);
```

## Migration Queries

### Count Data Before Import
```sql
SELECT 
    (SELECT COUNT(*) FROM Posts) as PostCount,
    (SELECT COUNT(*) FROM Recruiters) as RecruiterCount,
    (SELECT COUNT(*) FROM PostEmails) as PostEmailCount;
```

### Export for Backup Before Major Changes
```bash
sqlite3 Data/jobmatcher.db ".dump" > Data/jobmatcher.sql
```

## Tips & Tricks

### SQLite CLI Commands
```bash
# Connect
sqlite3 Data/jobmatcher.db

# Show tables
.tables

# Show schema for table
.schema Posts

# Show all queries
.mode line
SELECT * FROM Posts LIMIT 1;

# Export to JSON (SQLite 3.38+)
.mode json
SELECT * FROM Posts;

# Exit
.quit
```

### Format Output for Readability
```bash
sqlite3 -header -column Data/jobmatcher.db "SELECT * FROM Recruiters WHERE Status = 'Drafted' LIMIT 10;"
```

### Run SQL File
```bash
sqlite3 Data/jobmatcher.db < queries.sql
```

---

**Pro Tip**: Use SQLite Browser (https://sqlitebrowser.org) for a GUI interface instead of CLI.
