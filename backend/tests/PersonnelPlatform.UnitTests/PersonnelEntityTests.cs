using PersonnelPlatform.Domain.Personnel;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class PersonnelEntityTests
{
    [Fact]
    public void Employee_should_start_active_and_normalize_employee_number()
    {
        var employee = Employee.Create(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, " ab-001 ", "Ayşe", "Yılmaz", null, null, null, null, new DateOnly(2026, 8, 23), null, DateTimeOffset.UtcNow, null);
        Assert.Equal("ab-001", employee.EmployeeNo);
        Assert.Equal(EmployeeStatuses.Active, employee.Status);
    }

    [Fact]
    public void Employee_project_assignment_should_reject_invalid_allocation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EmployeeProjectAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), null, new DateOnly(2026, 8, 23), null, 101, DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public void Employee_project_assignment_should_reject_invalid_date_range()
    {
        Assert.Throws<ArgumentException>(() => EmployeeProjectAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), null, new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 22), 100, DateTimeOffset.UtcNow, null));
    }
}
