using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvestProMax.Infrastructure.Persistence;

public sealed class InvestProMaxDbContextDesignTimeFactory : IDesignTimeDbContextFactory<InvestProMaxDbContext>
{
    public InvestProMaxDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InvestProMaxDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=InvestProMax01DB;Trusted_Connection=True;");
        return new InvestProMaxDbContext(optionsBuilder.Options);
    }
}
