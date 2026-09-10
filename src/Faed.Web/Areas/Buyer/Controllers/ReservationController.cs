using Faed.Web.Areas.Buyer.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Common;
using Faed.Web.Services.Ordering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Buyer.Controllers;

/// <summary>
/// The single-listing reservation confirmation. The buyer holds one physical unit to go and
/// buy it in cash at the shop — no payment on the site, no cart. Buyer accounts and merchants
/// acting as consumers may reserve; administrators may not. An anonymous visitor signs in
/// first.
/// </summary>
[Area("Buyer")]
[Authorize(Policy = FaedPolicies.CanPlaceB2COrder)]
public sealed class ReservationController(IOrderService orders) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var result = await orders.GetReservationAsync(User.RequireUserId(), slug, cancellationToken);
        if (result.Failed)
        {
            if (result.ErrorKind == ResultErrorKind.NotFound)
            {
                return NotFound();
            }

            TempData["ErrorMessage"] = result.Error;
            return RedirectToAction("Details", "Listing", new { area = "", slug });
        }

        return View(new ReservationPageModel { Reservation = result.Value, Form = NewForm(result.Value) });
    }

    [HttpPost]
    public async Task<IActionResult> Index(ReservationFormModel form, CancellationToken cancellationToken)
    {
        var reservation = await orders.GetReservationAsync(User.RequireUserId(), form.ListingSlug, cancellationToken);
        if (reservation.Failed)
        {
            if (reservation.ErrorKind == ResultErrorKind.NotFound)
            {
                return NotFound();
            }

            TempData["ErrorMessage"] = reservation.Error;
            return RedirectToAction("Details", "Listing", new { area = "", slug = form.ListingSlug });
        }

        var view = reservation.Value;
        var sellable = view.Lines.Where(l => l.IsSellable).ToList();

        // A single-unit listing needs no variant choice, a single-location merchant no pickup
        // choice — fall back to the only option so the form has just the contact fields.
        if (form.VariantId == Guid.Empty && sellable.Count == 1)
        {
            form.VariantId = sellable[0].VariantId;
        }

        if (form.MerchantLocationId is null && view.PickupLocations.Count == 1)
        {
            form.MerchantLocationId = view.PickupLocations[0].Id;
        }

        if (!sellable.Any(l => l.VariantId == form.VariantId))
        {
            ModelState.AddModelError(nameof(form.VariantId), "Choose which one you want.");
        }

        if (form.MerchantLocationId is not { } locationId
            || view.PickupLocations.All(p => p.Id != locationId))
        {
            ModelState.AddModelError(nameof(form.MerchantLocationId), "Choose a pickup location.");
        }

        if (!ModelState.IsValid)
        {
            return View(new ReservationPageModel { Reservation = view, Form = form });
        }

        var placed = await orders.PlaceOrderAsync(User.RequireUserId(), new PlaceOrderInput(
            [new OrderLineInput(form.VariantId, 1)],
            OrderFulfillmentType.Pickup,
            form.MerchantLocationId,
            null,
            form.ContactName,
            form.ContactPhone,
            form.BuyerNote), cancellationToken);

        if (placed.Failed)
        {
            ModelState.AddModelError(string.Empty, placed.Error!);
            return View(new ReservationPageModel { Reservation = view, Form = form });
        }

        TempData["StatusMessage"] =
            "Reserved. The shop has 12 hours to confirm — you'll hear either way. Pay cash when you collect.";
        return RedirectToAction("Details", "Orders", new { area = "Buyer", id = placed.Value });
    }

    private static ReservationFormModel NewForm(ReservationView view) => new()
    {
        ListingSlug = view.ListingSlug,
        VariantId = view.Lines.Count(l => l.IsSellable) == 1
            ? view.Lines.First(l => l.IsSellable).VariantId
            : Guid.Empty,
        MerchantLocationId = view.PickupLocations.Count == 1 ? view.PickupLocations[0].Id : null,
    };
}
