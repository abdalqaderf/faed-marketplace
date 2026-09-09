using System.ComponentModel.DataAnnotations;
using Faed.Web.Services.Subscriptions;

namespace Faed.Web.Areas.Merchant.ViewModels;

public sealed class SubscriptionPageModel
{
    public required MerchantSubscriptionPageView Page { get; init; }

    public Guid? SelectedPlanId { get; set; }
}

public sealed class ChoosePlanFormModel
{
    [Required(ErrorMessage = "Choose a plan.")]
    public Guid SubscriptionPlanId { get; set; }
}
