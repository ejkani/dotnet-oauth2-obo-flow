using System.Security.Claims;
using Microsoft.Identity.Web;

namespace ApiApp.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static bool IsAppOnly(this ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            var nameIdentifier = principal.FindFirst(ClaimConstants.NameIdentifierId)?.Value;
            var objectIdentifier = principal.FindFirst(ClaimConstants.ObjectId)?.Value;

            // Check if nameIdentifier equals objectIdentifier
            var isAppOnly = nameIdentifier != null && objectIdentifier != null && nameIdentifier == objectIdentifier;
            return isAppOnly;
        }

        public static string? GetAzureAppServicePrincipalObjectId(this ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            var objectId = principal.FindFirst(ClaimConstants.ObjectId)?.Value;

            return objectId;
        }

        public static string? GetAzureAppId(this ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            var appId = principal.FindFirst("appid")?.Value ?? principal.FindFirst("azp")?.Value;

            return appId;
        }
    }
}
