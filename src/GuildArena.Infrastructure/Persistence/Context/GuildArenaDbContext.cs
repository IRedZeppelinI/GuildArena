using GuildArena.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace GuildArena.Infrastructure.Persistence.Context;

public class GuildArenaDbContext : IdentityDbContext<ApplicationUser>
{
    public GuildArenaDbContext(DbContextOptions<GuildArenaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<Hero> Heroes => Set<Hero>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<HeroPurchase> HeroPurchases => Set<HeroPurchase>();
    public DbSet<ActiveDungeonRun> ActiveDungeonRuns { get; set; }
    public DbSet<DungeonHeroState> DungeonHeroStates { get; set; }
    public DbSet<GuildDungeonRecord> GuildDungeonRecords { get; set; }
    public DbSet<ActiveQuest> ActiveQuests { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {        
        base.OnModelCreating(builder);

        // Aplica as  configurações 
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}