namespace Meridian.DataAccess.Models;

// One row per subject known to the PDP. Role AND Department both live here —
// deliberately not trusted from the caller's JWT/subject.properties, for the
// same reason role isn't: centralizing policy in the PDP means the PDP is the
// source of truth for "who this subject is," not the PEP's token claims.
public sealed class RoleAssignment
{
    public required string UserId { get; init; }
    public required string Role { get; init; }
    public required string Department { get; init; }
}
