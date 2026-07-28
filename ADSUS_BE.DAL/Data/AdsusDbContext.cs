using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Data
{
    public class AdsusDbContext : DbContext
    {
        public AdsusDbContext(DbContextOptions<AdsusDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdsusDbContext).Assembly);
        }
    }
}
