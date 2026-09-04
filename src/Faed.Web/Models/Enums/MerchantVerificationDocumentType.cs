namespace Faed.Web.Models.Enums;

/// <summary>
/// Type of business verification evidence a merchant attaches to an application.
/// The exact set of accepted Jordanian business documents is an open question;
/// These values are a safe, reversible default and
/// <see cref="Other"/> keeps the model flexible until the policy is fixed.
/// Personal / national identity documents are deliberately not offered: the privacy spec
/// forbids collecting national identifiers without a verified requirement
/// </summary>
public enum MerchantVerificationDocumentType
{
    CommercialRegistration = 0,
    TaxRegistration = 1,
    Other = 99,
}
