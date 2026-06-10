using Microsoft.EntityFrameworkCore;
using MeetingBackend.Entities;

namespace MeetingBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingParticipant> MeetingParticipants => Set<MeetingParticipant>();
    public DbSet<MeetingPoll> MeetingPolls => Set<MeetingPoll>();
    public DbSet<MeetingPollVote> MeetingPollVotes => Set<MeetingPollVote>();
    public DbSet<MeetingPollManager> MeetingPollManagers => Set<MeetingPollManager>();
    public DbSet<MeetingChatMessage> MeetingChatMessages => Set<MeetingChatMessage>();
    public DbSet<MeetingTranscriptEntry> MeetingTranscriptEntries => Set<MeetingTranscriptEntry>();
    public DbSet<MeetingDocument> MeetingDocuments => Set<MeetingDocument>();
    public DbSet<MeetingRecording> MeetingRecordings => Set<MeetingRecording>();
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
    public DbSet<MeetingInvitee> MeetingInvitees => Set<MeetingInvitee>();
    public DbSet<MeetingCoHost> MeetingCoHosts => Set<MeetingCoHost>();
    public DbSet<MeetingNotification> MeetingNotifications => Set<MeetingNotification>();
    public DbSet<MeetingMinutesSummary> MeetingMinutesSummaries => Set<MeetingMinutesSummary>();


    // ── Danh mục Quốc gia / Ngôn ngữ ────────────────────────────────────────
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<UserCountry> UserCountries => Set<UserCountry>();
    public DbSet<UserLanguage> UserLanguages => Set<UserLanguage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MeetingPoll>(e =>
        {
            e.HasIndex(x => new { x.MeetingId, x.PollId }).IsUnique();
            e.HasOne(x => x.Meeting)
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingPollVote>(e =>
        {
            e.HasIndex(x => new { x.MeetingPollId, x.VoterIdentity }).IsUnique();
            e.HasOne(x => x.MeetingPoll)
                .WithMany(p => p.Votes)
                .HasForeignKey(x => x.MeetingPollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingPollManager>(e =>
        {
            e.HasIndex(x => new { x.MeetingId, x.Username }).IsUnique();
            e.HasOne<Meeting>()
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingInvitee>(e =>
        {
            e.HasIndex(x => new { x.MeetingId, x.Username }).IsUnique();
            e.HasOne(x => x.Meeting)
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingCoHost>(e =>
        {
            e.HasIndex(x => new { x.MeetingId, x.HostUserId }).IsUnique();
            e.HasOne(x => x.Meeting)
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingChatMessage>(e =>
        {
            e.HasIndex(x => new { x.MeetingId, x.SentAtUtc });
            e.HasIndex(x => new { x.MeetingId, x.ClientMessageId }).IsUnique();
            e.HasOne<Meeting>()
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingTranscriptEntry>(e =>
        {
            e.HasIndex(x => new { x.MeetingId, x.AtUtc });
            e.HasOne<Meeting>()
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingRecording>(e =>
        {
            e.HasIndex(x => x.MeetingId);
            e.HasIndex(x => x.EgressId).IsUnique();
            e.HasOne(x => x.Meeting)
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingMinutesSummary>(e =>
        {
            e.HasKey(x => x.MeetingId);
            e.HasOne(x => x.Meeting)
                .WithOne()
                .HasForeignKey<MeetingMinutesSummary>(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrganizationUnit>(e =>
        {
            e.ToTable("OrganizationUnits");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.ParentId);
            e.HasIndex(x => x.Level);
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.Level).HasDefaultValue(1);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_OrganizationUnits_Level", "\"Level\" >= 1 AND \"Level\" <= 5");
                t.HasCheckConstraint("CK_OrganizationUnits_Parent_NotSelf", "\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\"");
            });

            e.HasOne<OrganizationUnit>()
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.OrganizationUnitId);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Users_AcademicRank", "\"AcademicRank\" IS NULL OR \"AcademicRank\" IN ('GS','PGS')");
                t.HasCheckConstraint("CK_Users_AcademicDegree", "\"AcademicDegree\" IS NULL OR \"AcademicDegree\" IN ('TS','ThS','CN','KS')");
            });
            e.HasOne<OrganizationUnit>()
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Countries ────────────────────────────────────────────────────────
        modelBuilder.Entity<Country>(e =>
        {
            e.ToTable("Countries");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.CountryName).HasMaxLength(100).IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasIndex(x => x.IsActive);
        });

        // ── Languages ────────────────────────────────────────────────────────
        modelBuilder.Entity<Language>(e =>
        {
            e.ToTable("Languages");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.LanguageName).HasMaxLength(100).IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasIndex(x => x.IsActive);
        });

        // ── UserCountries ─────────────────────────────────────────────────────
        modelBuilder.Entity<UserCountry>(e =>
        {
            e.ToTable("UserCountries");
            e.HasKey(x => new { x.UserId, x.CountryCode });
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Country)
                .WithMany(c => c.UserCountries)
                .HasForeignKey(x => x.CountryCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── UserLanguages ─────────────────────────────────────────────────────
        modelBuilder.Entity<UserLanguage>(e =>
        {
            e.ToTable("UserLanguages");
            e.HasKey(x => new { x.UserId, x.LanguageCode });
            e.Property(x => x.IsPrimary).HasDefaultValue(false);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Language)
                .WithMany(l => l.UserLanguages)
                .HasForeignKey(x => x.LanguageCode)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

