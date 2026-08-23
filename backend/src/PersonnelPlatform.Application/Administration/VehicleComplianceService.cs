using PersonnelPlatform.Application.Authorization;

namespace PersonnelPlatform.Application.Administration;

public sealed class VehicleComplianceService(
    IVehicleRepository repository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<VehicleResult<VehicleComplianceSummary>> UpdateAsync(
        Guid userId,
        Guid vehicleId,
        UpdateVehicleComplianceRequest request,
        CancellationToken cancellationToken)
    {
        var vehicle = await repository.FindVehicleAsync(vehicleId, cancellationToken);
        if (vehicle is null)
            return VehicleResult<VehicleComplianceSummary>.Failure("VEHICLE_NOT_FOUND", "Araç bulunamadı.");

        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, vehicle.CompanyId, cancellationToken))
            return VehicleResult<VehicleComplianceSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");

        if (vehicle.Version != request.Version)
            return VehicleResult<VehicleComplianceSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Araç başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");

        try
        {
            vehicle.UpdateComplianceDates(request.InsuranceValidUntil, request.InspectionValidUntil, timeProvider.GetUtcNow(), userId);
            await repository.SaveChangesAsync(cancellationToken);
            return VehicleResult<VehicleComplianceSummary>.Success(new(
                vehicle.Id,
                vehicle.InsuranceValidUntil,
                vehicle.InspectionValidUntil,
                vehicle.Version));
        }
        catch (ArgumentException)
        {
            return VehicleResult<VehicleComplianceSummary>.Failure("VEHICLE_COMPLIANCE_INVALID", "Sigorta veya muayene geçerlilik bilgileri geçersiz.");
        }
    }
}
