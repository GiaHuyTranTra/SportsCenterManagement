using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace SportsCenterManagement.Tests;

public class DataModelMappingTests
{
    [Fact]
    public void Model_ContainsLongSprint1StateColumns()
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseSqlServer(
                    "Server=localhost;Database=ModelOnly;Integrated Security=True;TrustServerCertificate=True")
                .Options;
        using SportsCenterManagementContext context =
            new SportsCenterManagementContext(options);

        Assert.NotNull(context.Model.FindEntityType(typeof(Account))?
            .FindProperty(nameof(Account.DeletedAt)));
        Assert.NotNull(context.Model.FindEntityType(typeof(MemberSubscription))?
            .FindProperty(nameof(MemberSubscription.IsSuspended)));
        Assert.NotNull(context.Model.FindEntityType(typeof(MemberSubscription))?
            .FindProperty(nameof(MemberSubscription.SuspensionReason)));
    }
}
