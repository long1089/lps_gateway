using System.Security.Claims;

namespace LpsGateway.Extensions
{
    public static class HttpContextExtension
    {
        //add extension methods for HttpContext here
        //get userid from claims
        public static int? GetUserId(this HttpContext httpContext)
        {
            var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return null;
        }

        //get username from claims
        public static string? GetUsername(this HttpContext httpContext)
        {
            var usernameClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            return usernameClaim?.Value;
        }

        //get user role from claims
        public static string? GetUserRole(this HttpContext httpContext)
        {
            var roleClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            return roleClaim?.Value;
        }

        //check if user is authenticated
        public static bool IsAuthenticated(this HttpContext httpContext)
        {
            return httpContext.User.Identity?.IsAuthenticated ?? false;
        }

        //get user ip address
        public static string? GetUserIpAddress(this HttpContext httpContext)
        {
            return httpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
