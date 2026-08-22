using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PersonnelPlatform.Application.Authorization;

namespace PersonnelPlatform.Api.Authorization;

public sealed class PermissionAuthorizationHandler(AccessControlService accessControlService) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var subject = context.User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId)) return;
        if (await accessControlService.HasPermissionAsync(userId, requirement.PermissionCode, CancellationToken.None)) context.Succeed(requirement);
    }
}
