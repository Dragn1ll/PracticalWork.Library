using Hangfire.Dashboard;

namespace PracticalWork.Email.Web.Hangfire;

/// <summary>
/// Фильтр авторизации для Hangfire Dashboard
/// </summary>
public class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}