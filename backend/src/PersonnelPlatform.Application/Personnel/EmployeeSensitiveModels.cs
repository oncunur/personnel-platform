namespace PersonnelPlatform.Application.Personnel;

public sealed record UpsertEmployeeSensitiveProfileRequest(string? NationalId, string? Iban, int? Version);
public sealed record EmployeeSensitiveProfileSummary(Guid EmployeeId, string? NationalIdMasked, string? IbanMasked, string? NationalId, string? Iban, bool Revealed, int Version);
