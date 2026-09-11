using System.Text.Json;

namespace LinkedInJobMatcher.Services
{
    public class SentRecruitersStore
    {
        private readonly string _path;
        private Dictionary<string, DateTime> _sent = new();

        public SentRecruitersStore(string path)
        {
            _path = path;
            Load();
        }

        private void Load()
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                _sent = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json)
                    ?? new Dictionary<string, DateTime>();
            }
        }

        public bool HasSent(string email) =>
            _sent.ContainsKey(email.Trim().ToLowerInvariant());

        public void MarkSent(string email) =>
            _sent[email.Trim().ToLowerInvariant()] = DateTime.UtcNow;

        public async Task SaveAsync()
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_sent, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_path, json);
        }

        public int Count => _sent.Count;
    }
}