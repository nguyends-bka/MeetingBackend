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
    public DbSet<MeetingChatMessage> MeetingChatMessages => Set<MeetingChatMessage>();
    public DbSet<MeetingTranscriptEntry> MeetingTranscriptEntries => Set<MeetingTranscriptEntry>();

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
    }
}
