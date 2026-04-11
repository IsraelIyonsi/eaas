namespace EaaS.Api.Features.Emails;

public sealed record ScheduleEmailResult(Guid EmailId, DateTime ScheduledAt, string Status);
