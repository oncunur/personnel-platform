namespace PersonnelPlatform.Api.Authorization;

public static class PermissionEndpointExtensions
{
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permissionCode) =>
        builder.RequireAuthorization($"{PermissionPolicyProvider.PolicyPrefix}{permissionCode}");
}
