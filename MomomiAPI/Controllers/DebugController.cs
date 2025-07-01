using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly ILogger<DebugController> _logger;

        public DebugController(ILogger<DebugController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Debug endpoint to inspect JWT token structure
        /// </summary>
        [HttpPost("inspect-token")]
        public IActionResult InspectToken([FromBody] TokenDebugRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                {
                    return BadRequest(new { message = "Token is required" });
                }

                var handler = new JwtSecurityTokenHandler();

                // Check if token is valid JWT format
                if (!handler.CanReadToken(request.Token))
                {
                    return BadRequest(new { message = "Invalid JWT token format" });
                }

                // Read token without validation
                var jsonToken = handler.ReadJwtToken(request.Token);

                var tokenInfo = new
                {
                    header = new
                    {
                        alg = jsonToken.Header.Alg,
                        typ = jsonToken.Header.Typ,
                        kid = jsonToken.Header.Kid
                    },
                    payload = new
                    {
                        iss = jsonToken.Issuer,
                        aud = jsonToken.Audiences.ToList(),
                        sub = jsonToken.Subject,
                        exp = jsonToken.ValidTo,
                        iat = jsonToken.IssuedAt,
                        claims = jsonToken.Claims.Select(c => new
                        {
                            type = c.Type,
                            value = c.Value
                        }).ToList()
                    },
                    validTo = jsonToken.ValidTo,
                    validFrom = jsonToken.ValidFrom,
                    isExpired = jsonToken.ValidTo < DateTime.UtcNow
                };

                return Ok(tokenInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inspecting token");
                return BadRequest(new { message = "Error parsing token", error = ex.Message });
            }
        }
    }

    public class TokenDebugRequest
    {
        public string Token { get; set; } = string.Empty;
    }
}