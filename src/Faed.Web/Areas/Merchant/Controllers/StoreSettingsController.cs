using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Common;
using Faed.Web.Services.Ordering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// Merchant fulfilment configuration: pickup locations and delivery zones a B2C order can
/// use (docs/03-BUSINESS-RULES.md §12, docs/07-UI-UX-SPEC.md §6 "Store Settings"). Without
/// at least one active option a merchant cannot receive orders.
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.ApprovedMerchant)]
public sealed class StoreSettingsController(IMerchantStoreService store) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await store.GetSettingsAsync(User.RequireUserId(), cancellationToken);
        return View(new StoreSettingsPageModel { Settings = settings });
    }

    [HttpPost]
    public async Task<IActionResult> SaveLocation(PickupLocationFormModel locationForm, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedisplayAsync(cancellationToken, m => m.LocationForm = locationForm);
        }

        var userId = User.RequireUserId();
        var result = locationForm.Id is { } id
            ? await store.UpdateLocationAsync(userId, id, locationForm.ToInput(), cancellationToken)
            : (await store.AddLocationAsync(userId, locationForm.ToInput(), cancellationToken));

        return await AfterMutationAsync(result, cancellationToken,
            locationForm.Id is null ? "Pickup location added." : "Pickup location updated.",
            m => m.LocationForm = locationForm);
    }

    [HttpPost]
    public async Task<IActionResult> SetLocationActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await store.SetLocationActiveAsync(User.RequireUserId(), id, isActive, cancellationToken);
        return await AfterMutationAsync(result, cancellationToken,
            isActive ? "Pickup location re-enabled." : "Pickup location disabled.", _ => { });
    }

    [HttpPost]
    public async Task<IActionResult> SaveZone(DeliveryZoneFormModel zoneForm, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedisplayAsync(cancellationToken, m => m.ZoneForm = zoneForm);
        }

        var userId = User.RequireUserId();
        var result = zoneForm.Id is { } id
            ? await store.UpdateDeliveryZoneAsync(userId, id, zoneForm.ToInput(), cancellationToken)
            : (await store.AddDeliveryZoneAsync(userId, zoneForm.ToInput(), cancellationToken));

        return await AfterMutationAsync(result, cancellationToken,
            zoneForm.Id is null ? "Delivery zone added." : "Delivery zone updated.",
            m => m.ZoneForm = zoneForm);
    }

    [HttpPost]
    public async Task<IActionResult> SetZoneActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await store.SetDeliveryZoneActiveAsync(User.RequireUserId(), id, isActive, cancellationToken);
        return await AfterMutationAsync(result, cancellationToken,
            isActive ? "Delivery zone re-enabled." : "Delivery zone disabled.", _ => { });
    }

    private async Task<IActionResult> AfterMutationAsync(
        Result result, CancellationToken cancellationToken, string successMessage,
        Action<StoreSettingsPageModel> adjust)
    {
        if (result.Succeeded)
        {
            TempData["StatusMessage"] = successMessage;
            return RedirectToAction(nameof(Index));
        }

        if (result.ErrorKind == ResultErrorKind.NotFound)
        {
            return NotFound();
        }

        ModelState.AddModelError(string.Empty, result.Error!);
        return await RedisplayAsync(cancellationToken, adjust);
    }

    private async Task<IActionResult> RedisplayAsync(
        CancellationToken cancellationToken, Action<StoreSettingsPageModel> adjust)
    {
        var settings = await store.GetSettingsAsync(User.RequireUserId(), cancellationToken);
        var model = new StoreSettingsPageModel { Settings = settings };
        adjust(model);
        return View(nameof(Index), model);
    }
}
