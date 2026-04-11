namespace EaaS.Domain.Enums;

public enum AuditAction
{
    TenantCreated,
    TenantUpdated,
    TenantSuspended,
    TenantActivated,
    TenantDeactivated,
    AdminUserCreated,
    AdminUserUpdated,
    AdminUserDeleted,
    AdminLogin,
    AdminLoginFailed,
    SettingsUpdated
}
