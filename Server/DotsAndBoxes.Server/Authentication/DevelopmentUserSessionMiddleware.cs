using System.Security.Claims;

namespace DotsAndBoxes.Server.Authentication
{
    public sealed class DevelopmentUserSessionMiddleware
    {
        private const string USER_ID_QUERY_NAME = "userId";

        private readonly RequestDelegate NEXT;
        private readonly IWebHostEnvironment ENVIRONMENT;

        public DevelopmentUserSessionMiddleware(
            RequestDelegate next ,
            IWebHostEnvironment environment)
        {
            NEXT = next;
            ENVIRONMENT = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if ( !ENVIRONMENT.IsDevelopment() ||
                context.User.Identity?.IsAuthenticated == true )
            {
                await NEXT(context);
                return;
            }

            string userIdText =
                context.Request.Query[USER_ID_QUERY_NAME].ToString();

            if ( Guid.TryParse(userIdText , out Guid userId) &&
                userId != Guid.Empty )
            {
                Claim[] claims =
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                };

                ClaimsIdentity identity =
                    new ClaimsIdentity(claims, "DevelopmentUserSession");

                context.User = new ClaimsPrincipal(identity);
            }

            await NEXT(context);
        }
    }
}