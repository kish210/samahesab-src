using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.CRM;

public class Customer : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string CustomerType { get; private set; } = "حقیقی";
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? CompanyName { get; private set; }
    public string? NationalCode { get; private set; }
    public string? EconomicCode { get; private set; }
    public string? RegisterNumber { get; private set; }
    public int? GroupId { get; private set; }
    public int? AccountId { get; private set; }
    public string? Phone { get; private set; }
    public string? Mobile { get; private set; }
    public string? Phone2 { get; private set; }
    public string? Email { get; private set; }
    public string? Province { get; private set; }
    public string? City { get; private set; }
    public string? Address { get; private set; }
    public string? PostalCode { get; private set; }
    public string? BirthDate { get; private set; }
    public decimal CreditLimit { get; private set; }
    public int CreditDays { get; private set; }
    public string PriceLevel { get; private set; } = "خرده";
    public decimal Discount { get; private set; }
    public int LoyaltyPoints { get; private set; }
    public decimal Balance { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Notes { get; private set; }

    public ICollection<CustomerContact> Contacts { get; private set; } = new List<CustomerContact>();

    private Customer() { }

    public static Customer Create(int companyId, string code, string customerType,
        string? firstName = null, string? lastName = null, string? companyName = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کد مشتری الزامی است.");
        if (customerType == "حقیقی" && string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("نام و نام خانوادگی الزامی است.");
        if (customerType == "حقوقی" && string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("نام شرکت الزامی است.");

        return new Customer
        {
            CompanyId = companyId,
            Code = code,
            CustomerType = customerType,
            FirstName = firstName,
            LastName = lastName,
            CompanyName = companyName
        };
    }

    public string FullName => CustomerType == "حقوقی"
        ? CompanyName ?? ""
        : $"{FirstName} {LastName}".Trim();

    public void UpdateContactInfo(string? phone, string? mobile, string? email,
        string? province, string? city, string? address, string? postalCode)
    {
        Phone = phone;
        Mobile = mobile;
        Email = email;
        Province = province;
        City = city;
        Address = address;
        PostalCode = postalCode;
        UpdatedAt = DateTime.Now;
    }

    public void UpdateCreditTerms(decimal creditLimit, int creditDays, string priceLevel, decimal discount)
    {
        CreditLimit = creditLimit;
        CreditDays = creditDays;
        PriceLevel = priceLevel;
        Discount = discount;
        UpdatedAt = DateTime.Now;
    }

    public void AddLoyaltyPoints(int points) { LoyaltyPoints += points; UpdatedAt = DateTime.Now; }
    public void UseLoyaltyPoints(int points)
    {
        if (LoyaltyPoints < points) throw new InvalidOperationException("امتیاز کافی ندارید.");
        LoyaltyPoints -= points;
        UpdatedAt = DateTime.Now;
    }

    public void UpdateBalance(decimal amount) { Balance = amount; UpdatedAt = DateTime.Now; }
    public bool HasAvailableCredit(decimal amount) => (Balance + amount) <= CreditLimit;
    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.Now; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.Now; }
    public void SetBirthDate(string birthDate) { BirthDate = birthDate; UpdatedAt = DateTime.Now; }
    public void SetAccountId(int accountId) { AccountId = accountId; UpdatedAt = DateTime.Now; }
    public void SetDetails(string? nationalCode, string? economicCode, int? groupId, string? notes)
    {
        NationalCode = nationalCode; EconomicCode = economicCode; GroupId = groupId; Notes = notes;
        UpdatedAt = DateTime.Now;
    }
    public void UpdateNames(string? firstName, string? lastName, string? companyName)
    {
        FirstName = firstName; LastName = lastName; CompanyName = companyName;
        UpdatedAt = DateTime.Now;
    }
}
