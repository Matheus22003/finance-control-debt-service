using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinanceControl.DebtService.Persistence;

public sealed class DebtDbContextFactory : IDesignTimeDbContextFactory<DebtDbContext>
{
    public DebtDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DebtDbContext>()
            .UseNpgsql("Host=localhost;Database=finance_control_debt;Username=debt_app;Password=design-time")
            .Options;

        return new DebtDbContext(options);
    }
}
