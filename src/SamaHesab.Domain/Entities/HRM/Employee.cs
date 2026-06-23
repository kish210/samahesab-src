using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.HRM;

public class Employee : AuditableEntity
{
    public int? BranchId { get; private set; }
    public string Code { get; private set; } = default!;
    public string NationalCode { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string? FatherName { get; private set; }
    public string? BirthDate { get; private set; }
    public string? BirthPlace { get; private set; }
    public string? Gender { get; private set; }
    public string? MaritalStatus { get; private set; }
    public string? MilitaryStatus { get; private set; }
    public string? Education { get; private set; }
    public string? Mobile { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public int? DepartmentId { get; private set; }
    public int? PositionId { get; private set; }
    public string HireDate { get; private set; } = default!;
    public string ContractType { get; private set; } = "دائم";
    public decimal BaseSalary { get; private set; }
    public string? BankAccount { get; private set; }
    public string? BankName { get; private set; }
    public string? ShebaNumber { get; private set; }
    public string? InsuranceNumber { get; private set; }
    public string? TaxCode { get; private set; }
    public byte[]? Photo { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? TerminationDate { get; private set; }
    public string? TerminationReason { get; private set; }
    public int? UserId { get; private set; }
    public int? AccountId { get; private set; }
    public string? Notes { get; private set; }
    // PAY-C1-1 — اقلامِ موردِ نیازِ حقوقِ کامل.
    public int ChildrenCount { get; private set; }        // تعدادِ فرزندِ مشمولِ حق اولاد (تا ۲)
    public int PriorServiceMonths { get; private set; }   // سابقهٔ پیش از استخدام (ماه) — برای پایهٔ سنوات

    public ICollection<AttendanceRecord> AttendanceRecords { get; private set; } = new List<AttendanceRecord>();
    public ICollection<SalarySlip> SalarySlips { get; private set; } = new List<SalarySlip>();

    private Employee() { }

    public static Employee Create(int companyId, int? branchId, string code, string nationalCode,
        string firstName, string lastName, string hireDate, decimal baseSalary,
        string contractType = "دائم")
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کد کارمند الزامی است.");
        if (string.IsNullOrWhiteSpace(nationalCode)) throw new ArgumentException("کد ملی الزامی است.");
        if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("نام الزامی است.");
        if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentException("نام خانوادگی الزامی است.");
        if (baseSalary < 0) throw new ArgumentException("حقوق پایه نمی‌تواند منفی باشد.");

        return new Employee
        {
            CompanyId = companyId,
            BranchId = branchId,
            Code = code,
            NationalCode = nationalCode,
            FirstName = firstName,
            LastName = lastName,
            HireDate = hireDate,
            BaseSalary = baseSalary,
            ContractType = contractType
        };
    }

    public string FullName => $"{FirstName} {LastName}";

    public void UpdatePersonalInfo(string? fatherName, string? birthDate, string? birthPlace,
        string? gender, string? maritalStatus, string? militaryStatus, string? education)
    {
        FatherName = fatherName;
        BirthDate = birthDate;
        BirthPlace = birthPlace;
        Gender = gender;
        MaritalStatus = maritalStatus;
        MilitaryStatus = militaryStatus;
        Education = education;
        UpdatedAt = DateTime.Now;
    }

    public void UpdateContactInfo(string? mobile, string? phone, string? email, string? address)
    {
        Mobile = mobile;
        Phone = phone;
        Email = email;
        Address = address;
        UpdatedAt = DateTime.Now;
    }

    public void UpdateBankInfo(string? bankName, string? bankAccount, string? shebaNumber)
    {
        BankName = bankName;
        BankAccount = bankAccount;
        ShebaNumber = shebaNumber;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>PAY-C1-1 — اقلامِ حقوقی: شمارهٔ بیمه، تعدادِ فرزندِ مشمول، سابقهٔ پیشین (ماه).</summary>
    public void UpdatePayrollInfo(string? insuranceNumber, int childrenCount, int priorServiceMonths)
    {
        InsuranceNumber = insuranceNumber;
        ChildrenCount = childrenCount < 0 ? 0 : childrenCount;
        PriorServiceMonths = priorServiceMonths < 0 ? 0 : priorServiceMonths;
        UpdatedAt = DateTime.Now;
    }

    public void UpdateSalary(decimal baseSalary)
    {
        BaseSalary = baseSalary;
        UpdatedAt = DateTime.Now;
    }

    public void UpdatePosition(int? departmentId, int? positionId)
    {
        DepartmentId = departmentId;
        PositionId = positionId;
        UpdatedAt = DateTime.Now;
    }

    public void Terminate(string terminationDate, string reason)
    {
        IsActive = false;
        TerminationDate = terminationDate;
        TerminationReason = reason;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>غیرفعال‌سازیِ نرم (وقتی کارمند سوابقِ فیش/تردد دارد و نباید سخت حذف شود).</summary>
    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.Now; }

    public void SetPhoto(byte[] photo) { Photo = photo; UpdatedAt = DateTime.Now; }
    public void LinkUser(int userId) { UserId = userId; UpdatedAt = DateTime.Now; }
}
