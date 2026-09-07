namespace Meridian.IntegrationTests.TestSupport;

// A synthetic candidate expense for the batch-evaluation tests: only the fields
// the PDP's ("expense", "read") rule reads out of the SARC resource (owner and
// status). Never persisted to any database.
public sealed record CandidateExpense(Guid Id, string OwnerId, string Status);
