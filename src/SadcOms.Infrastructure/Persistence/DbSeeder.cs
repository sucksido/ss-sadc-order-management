using Microsoft.EntityFrameworkCore;
using SadcOms.Domain.Customers;
using SadcOms.Domain.Orders;

namespace SadcOms.Infrastructure.Persistence;

/// <summary>
/// Seeds a small, representative data set for local development and manual testing. Idempotent:
/// it does nothing if customers already exist. Not used in production environments.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Customers.AnyAsync(cancellationToken))
        {
            return;
        }

        var thandi = Customer.Create("Thandi Nkosi", "thandi@example.co.za", "ZA");
        var kgosi = Customer.Create("Kgosi Modise", "kgosi@example.co.bw", "BW");
        var tendai = Customer.Create("Tendai Moyo", "tendai@example.co.zw", "ZW");

        db.Customers.AddRange(thandi, kgosi, tendai);

        var order1 = Order.Create(thandi.Id, "ZA", "ZAR",
            [("SKU-TEE-001", 3, 149.99m), ("SKU-CAP-002", 2, 89.50m)]);

        var order2 = Order.Create(tendai.Id, "ZW", "USD",
            [("SKU-MUG-010", 5, 6.25m)]);

        db.Orders.AddRange(order1, order2);

        await db.SaveChangesAsync(cancellationToken);
    }
}
