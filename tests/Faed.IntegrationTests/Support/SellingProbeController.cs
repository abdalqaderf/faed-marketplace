using Faed.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.IntegrationTests.Support;

/// <summary>
/// A test-only endpoint that stands in for the future listing-creation surface so the
/// <c>ApprovedMerchant</c> policy can be exercised through the real MVC pipeline
/// (docs/18-TRACEABILITY.md — "verified merchants only sell", authorization integration test).
/// Added as an application part only by the test factory.
/// </summary>
[ApiController]
[Route("_probe")]
public sealed class SellingProbeController : ControllerBase
{
    [HttpGet("selling")]
    [Authorize(Policy = FaedPolicies.ApprovedMerchant)]
    public IActionResult Selling() => Ok("selling-allowed");
}
