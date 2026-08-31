using Faed.Domain.Enums;

namespace Faed.Application.Merchants;

/// <summary>A private file opened for an authorized admin download.</summary>
public sealed record StoredFileContent(Stream Content, string ContentType, string OriginalFileName);

/// <summary>Merchant-supplied business details for an application (draft or submission).</summary>
public sealed record MerchantApplicationInput(
    string BusinessName,
    string? ContactEmail,
    string? ContactPhone);

/// <summary>A verification document the merchant wants to attach.</summary>
public sealed record AddVerificationDocumentInput(
    MerchantVerificationDocumentType DocumentType,
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long LengthBytes);

/// <summary>Document metadata as shown to the merchant or the reviewing admin.</summary>
public sealed record VerificationDocumentView(
    Guid Id,
    MerchantVerificationDocumentType DocumentType,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAtUtc);

/// <summary>The current merchant's own view of their application.</summary>
public sealed record MerchantApplicationView(
    Guid Id,
    string BusinessName,
    string? ContactEmail,
    string? ContactPhone,
    string PublicSlug,
    MerchantVerificationStatus Status,
    bool IsEditable,
    bool CanSell,
    DateTime? SubmittedAtUtc,
    DateTime? ReviewedAtUtc,
    string? RejectionReason,
    IReadOnlyList<VerificationDocumentView> Documents);

/// <summary>A row in the admin verification queue.</summary>
public sealed record MerchantQueueItem(
    Guid Id,
    string BusinessName,
    MerchantVerificationStatus Status,
    DateTime? SubmittedAtUtc,
    DateTime CreatedAtUtc,
    int ActiveDocumentCount);

/// <summary>Full application detail for the reviewing admin.</summary>
public sealed record MerchantReviewDetail(
    Guid Id,
    string UserId,
    string BusinessName,
    string? ContactEmail,
    string? ContactPhone,
    string PublicSlug,
    MerchantVerificationStatus Status,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? ReviewedAtUtc,
    string? ReviewedByAdminId,
    string? RejectionReason,
    IReadOnlyList<VerificationDocumentView> Documents);
