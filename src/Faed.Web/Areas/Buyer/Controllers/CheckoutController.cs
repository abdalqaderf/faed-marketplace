using Faed.Web.Areas.Buyer.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Common;
using Faed.Web.Services.Ordering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Buyer.Controllers;

/// <summary>
/// The single-listing B2C order builder and checkout (tasks/TASK-006-B2C-ORDERS.md). Any
/// authenticated non-administrator may buy (docs/16-PERMISSIONS-MATRIX.md "Create B2C
/// order" — Admin ❌); an anonymous visitor is sent to sign in first.
/// </summary>
[Area("Buyer")]
[Authorize(Policy = FaedPolicies.CanPlaceB2COrder)]
public sealed class CheckoutController(IOrderService orders) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var result = await orders.GetCheckoutAsync(User.RequireUserId(), slug, cancellationToken);
        if (result.Failed)
        {
            if (result.ErrorKind == ResultErrorKind.NotFound)
            {
                return NotFound();
            }

            TempData["ErrorMessage"] = result.Error;
            return RedirectToAction("Details", "Listing", new { area = "", slug });
        }

        return View(BuildPage(result.Value, NewForm(result.Value)));
    }

    [HttpPost]
    public async Task<IActionResult> Index(CheckoutFormModel form, CancellationToken cancellationToken)
    {
        var checkout = await orders.GetCheckoutAsync(User.RequireUserId(), form.ListingSlug, cancellationToken);
        if (checkout.Failed)
        {
            if (checkout.ErrorKind == ResultErrorKind.NotFound)
            {
                return NotFound();
            }

            TempData["ErrorMessage"] = checkout.Error;
            return RedirectToAction("Details", "Listing", new { area = "", slug = form.ListingSlug });
        }

        if (!ModelState.IsValid)
        {
            return View(BuildPage(checkout.Value, form));
        }

        var lines = form.Lines
            .Where(l => l.Quantity > 0)
            .Select(l => new OrderLineInput(l.VariantId, l.Quantity))
            .ToList();

        if (lines.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Choose a quantity for at least one variant.");
            return View(BuildPage(checkout.Value, form));
        }

        var placed = await orders.PlaceOrderAsync(User.RequireUserId(), new PlaceOrderInput(
            lines,
            form.FulfillmentType,
            form.FulfillmentType == OrderFulfillmentType.Pickup ? form.MerchantLocationId : null,
            form.FulfillmentType == OrderFulfillmentType.MerchantDelivery ? form.DeliveryZoneId : null,
            form.DeliveryAddressText,
            form.ContactName,
            form.ContactPhone,
            form.BuyerNote), cancellationToken);

        if (placed.Failed)
        {
            ModelState.AddModelError(string.Empty, placed.Error!);
            return View(BuildPage(checkout.Value, form));
        }

        TempData["StatusMessage"] =
            "Order placed. Your items are reserved while the merchant confirms it.";
        return RedirectToAction("Details", "Orders", new { area = "Buyer", id = placed.Value });
    }

    private static CheckoutFormModel NewForm(CheckoutView checkout) => new()
    {
        ListingSlug = checkout.ListingSlug,
        FulfillmentType = checkout.CanPickup ? OrderFulfillmentType.Pickup : OrderFulfillmentType.MerchantDelivery,
        Lines = checkout.Lines.Select(l => new CheckoutLineFormModel { VariantId = l.VariantId, Quantity = 0 }).ToList(),
    };

    private static CheckoutPageModel BuildPage(CheckoutView checkout, CheckoutFormModel form)
    {
        // Keep the posted quantities aligned with the current variant list (a variant could
        // have been deactivated between GET and POST).
        var byVariant = form.Lines.ToDictionary(l => l.VariantId, l => l.Quantity);
        form.ListingSlug = checkout.ListingSlug;
        form.Lines = checkout.Lines
            .Select(l => new CheckoutLineFormModel
            {
                VariantId = l.VariantId,
                Quantity = byVariant.GetValueOrDefault(l.VariantId),
            })
            .ToList();

        return new CheckoutPageModel { Checkout = checkout, Form = form };
    }
}
