using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.CRM;

/// <summary>
/// طرف‌حساب (Party) — موجودیتِ یکپارچهٔ مشتری/تأمین‌کننده (سبکِ ERP ایرانی).
/// یک شخص می‌تواند هم‌زمان مشتری و/یا تأمین‌کننده باشد (نقش‌ها: IsCustomer/IsSupplier).
/// جایگزینِ تدریجیِ Customer + Supplier (مهاجرتِ افزایشی؛ تا تکمیلِ cutover هر دو موجودند).
/// </summary>
public class Party : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string PartyType { get; private set; } = "حقیقی";   // حقیقی/حقوقی
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? CompanyName { get; private set; }
    public string? NationalCode { get; private set; }
    public string? EconomicCode { get; private set; }
    public int? AccountId { get; private set; }
    public string? Phone { get; private set; }
    public string? Mobile { get; private set; }
    public string? Email { get; private set; }
    public string? Province { get; private set; }
    public string? City { get; private set; }
    public string? Address { get; private set; }
    public string? PostalCode { get; private set; }
    public decimal CreditLimit { get; private set; }
    public int CreditDays { get; private set; }
    public string PriceLevel { get; private set; } = "خرده";
    public decimal Discount { get; private set; }
    public int LoyaltyPoints { get; private set; }
    public decimal Balance { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Notes { get; private set; }
    public string? ContactPerson { get; private set; }
    public string? Visitor { get; private set; }
    public int? GroupId { get; private set; }        // گروهِ مشتری (عادی/طلایی/عمده) — UX-CRM-FORM
    public string? BirthDate { get; private set; }   // تاریخِ تولد (شمسی، رشته‌ای مثلِ سایرِ تاریخ‌ها)
    public int? BranchId { get; private set; }        // P3 — شعبهٔ مالکِ طرف‌حساب (null = مشترکِ همهٔ شعب)

    // نقش‌ها (یک طرف‌حساب می‌تواند چند نقش داشته باشد: مشتری/تأمین‌کننده/کارمند)
    public bool IsCustomer { get; private set; }
    public bool IsSupplier { get; private set; }
    public bool IsEmployee { get; private set; }

    // ردیابیِ مبدأ در دورهٔ مهاجرت (برای idempotency و repoint کردنِ FKها)
    public int? LegacyCustomerId { get; private set; }
    public int? LegacySupplierId { get; private set; }
    public int? LegacyEmployeeId { get; private set; }

    private Party() { }

    public static Party Create(int companyId, string code, string partyType,
        string? firstName = null, string? lastName = null, string? companyName = null,
        bool isCustomer = false, bool isSupplier = false)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کد طرف‌حساب الزامی است.");
        return new Party
        {
            CompanyId = companyId, Code = code, PartyType = partyType,
            FirstName = firstName, LastName = lastName, CompanyName = companyName,
            IsCustomer = isCustomer, IsSupplier = isSupplier
        };
    }

    public string FullName => PartyType == "حقوقی"
        ? CompanyName ?? ""
        : $"{FirstName} {LastName}".Trim();

    public void SetRoles(bool isCustomer, bool isSupplier, bool isEmployee = false)
    { IsCustomer = isCustomer; IsSupplier = isSupplier; IsEmployee = isEmployee; SetAudit(null); }
    public void MarkCustomer() { IsCustomer = true; SetAudit(null); }
    public void MarkSupplier() { IsSupplier = true; SetAudit(null); }
    public void MarkEmployee() { IsEmployee = true; SetAudit(null); }
    public void UpdateProfile(string? nationalCode, string? mobile, string? phone, string? email,
        string? province, string? city, string? address)
    {
        NationalCode = nationalCode; Mobile = mobile; Phone = phone; Email = email;
        Province = province; City = city; Address = address; SetAudit(null);
    }
    public void SetTaxIds(string? nationalCode, string? economicCode, string? notes = null)
    { NationalCode = nationalCode; EconomicCode = economicCode; if (notes != null) Notes = notes; SetAudit(null); }
    public void SetCreditTerms(decimal creditLimit, int creditDays, string priceLevel, decimal discount)
    { CreditLimit = creditLimit; CreditDays = creditDays; if (!string.IsNullOrWhiteSpace(priceLevel)) PriceLevel = priceLevel; Discount = discount; SetAudit(null); }
    /// <summary>UX-CRM-EDIT — به‌روزرسانیِ مشخصاتِ پایهٔ ویرایش (نام/نوع/کدپستی/رابط/ویزیتور).</summary>
    public void EditCore(string partyType, string? firstName, string? lastName, string? companyName,
        string? postalCode, string? contactPerson, string? visitor,
        int? groupId = null, string? birthDate = null)
    {
        if (!string.IsNullOrWhiteSpace(partyType)) PartyType = partyType;
        FirstName = firstName; LastName = lastName; CompanyName = companyName;
        PostalCode = postalCode; ContactPerson = contactPerson; Visitor = visitor;
        GroupId = groupId; BirthDate = birthDate; SetAudit(null);
    }
    /// <summary>P3 — تعیینِ شعبهٔ مالکِ طرف‌حساب (null = مشترکِ همهٔ شعب؛ دیدنی برای همه).</summary>
    public void SetBranch(int? branchId) { BranchId = branchId; SetAudit(null); }
    public void UpdateBalance(decimal amount) { Balance = amount; SetAudit(null); }
    public void Deactivate() { IsActive = false; SetAudit(null); }
    public void Activate() { IsActive = true; SetAudit(null); }
    public void SetLegacy(int? customerId, int? supplierId) { LegacyCustomerId = customerId; LegacySupplierId = supplierId; }
}
