namespace PersonnelPlatform.Application.Authorization;

public static class ScopeTypes
{
    public const string Global = "GLOBAL";
    public const string Company = "COMPANY";
    public const string Branch = "BRANCH";
    public const string Department = "DEPARTMENT";
    public const string Project = "PROJECT";
    public const string Camp = "CAMP";

    public static bool IsKnown(string scopeType) => scopeType is Global or Company or Branch or Department or Project or Camp;
}
