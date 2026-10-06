using Microsoft.EntityFrameworkCore;

namespace LinkedInJobMatcher.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
     : base(options)
        {
        }

        public DbSet<Post> Posts { get; set; }
        public DbSet<Recruiter> Recruiters { get; set; }
        public DbSet<PostEmail> PostEmails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
     base.OnModelCreating(modelBuilder);

     // Post configuration
     modelBuilder.Entity<Post>()
  .HasKey(p => p.PostId);

     modelBuilder.Entity<Post>()
  .HasIndex(p => p.Status);

     modelBuilder.Entity<Post>()
         .Property(p => p.Status)
         .HasConversion<string>();

     modelBuilder.Entity<Post>()
         .HasMany(p => p.PostEmails)
  .WithOne(pe => pe.Post)
  .HasForeignKey(pe => pe.PostId)
  .OnDelete(DeleteBehavior.Cascade);

     // Recruiter configuration
            modelBuilder.Entity<Recruiter>()
  .HasKey(r => r.Id);

     modelBuilder.Entity<Recruiter>()
  .HasIndex(r => r.Email)
  .IsUnique();

     modelBuilder.Entity<Recruiter>()
  .Property(r => r.Status)
  .HasConversion<string>();

     modelBuilder.Entity<Recruiter>()
  .HasMany(r => r.PostEmails)
         .WithOne(pe => pe.Recruiter)
  .HasForeignKey(pe => pe.RecruiterId)
         .OnDelete(DeleteBehavior.Cascade);

     // PostEmail configuration
     modelBuilder.Entity<PostEmail>()
         .HasKey(pe => new { pe.PostId, pe.RecruiterId });

     modelBuilder.Entity<PostEmail>()
  .HasOne(pe => pe.Post)
  .WithMany(p => p.PostEmails)
         .HasForeignKey(pe => pe.PostId)
         .OnDelete(DeleteBehavior.Cascade);

     modelBuilder.Entity<PostEmail>()
         .HasOne(pe => pe.Recruiter)
  .WithMany(r => r.PostEmails)
         .HasForeignKey(pe => pe.RecruiterId)
         .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
