using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.HRM;

public class SalarySlip : BaseEntity
{
    public int EmployeeId { get; private set; }
    public string PeriodYear { get; private set; } = default!;
    public byte PeriodMonth { get; private set; }
    public decimal BaseSalary { get; private set; }
    public decimal OvertimePay { get; private set; }
    public decimal Allowances { get; private set; }
    public decimal Commission { get; private set; }
    public decimal Bonuses { get; private set; }
    // PAY-C1-2 — اجزای تفکیکیِ حقوقِ ایرانی (مزایا) — هرکدام در ناخالص لحاظ می‌شوند.
    public decimal HousingAllowance { get; private set; }   // حق مسکن
    public decimal FoodAllowance { get; private set; }       // بن کارگری/خواربار
    public decimal ChildAllowance { get; private set; }      // حق اولاد
    public decimal SeniorityPay { get; private set; }        // حق سنوات
    public decimal NightShiftPay { get; private set; }       // شب‌کاری
    public decimal HolidayPay { get; private set; }          // جمعه/تعطیل‌کاری
    public decimal EmployerInsurance { get; private set; }   // سهمِ کارفرما ۲۳٪ (جدا از خالص — برای لیستِ بیمه)
    public decimal GrossSalary { get; private set; }
    public decimal InsuranceDeduct { get; private set; }
    public decimal TaxDeduct { get; private set; }
    public decimal OtherDeductions { get; private set; }
    public decimal NetSalary { get; private set; }
    public bool IsPaid { get; private set; }
    public string? PaidDate { get; private set; }
    public int? VoucherId { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.Now;

    private SalarySlip() { }

    public static SalarySlip Create(int employeeId, string periodYear, byte periodMonth,
        decimal baseSalary, decimal overtimePay = 0, decimal allowances = 0,
        decimal commission = 0, decimal bonuses = 0,
        decimal insuranceDeduct = 0, decimal taxDeduct = 0, decimal otherDeductions = 0,
        string? notes = null,
        // PAY-C1-2 — اجزای تفکیکیِ ایرانی (اختیاری؛ موتورِ کاملِ حقوق پرشان می‌کند).
        decimal housingAllowance = 0, decimal foodAllowance = 0, decimal childAllowance = 0,
        decimal seniorityPay = 0, decimal nightShiftPay = 0, decimal holidayPay = 0,
        decimal employerInsurance = 0)
    {
        // اجزای تفکیکی هم جزوِ ناخالص‌اند (سهمِ کارفرما جدا — در ناخالص/خالص نمی‌آید).
        var gross = baseSalary + overtimePay + allowances + commission + bonuses
                  + housingAllowance + foodAllowance + childAllowance + seniorityPay + nightShiftPay + holidayPay;
        var net = gross - insuranceDeduct - taxDeduct - otherDeductions;

        return new SalarySlip
        {
            EmployeeId = employeeId,
            PeriodYear = periodYear,
            PeriodMonth = periodMonth,
            BaseSalary = baseSalary,
            OvertimePay = overtimePay,
            Allowances = allowances,
            Commission = commission,
            Bonuses = bonuses,
            HousingAllowance = housingAllowance,
            FoodAllowance = foodAllowance,
            ChildAllowance = childAllowance,
            SeniorityPay = seniorityPay,
            NightShiftPay = nightShiftPay,
            HolidayPay = holidayPay,
            EmployerInsurance = employerInsurance,
            GrossSalary = gross,
            InsuranceDeduct = insuranceDeduct,
            TaxDeduct = taxDeduct,
            OtherDeductions = otherDeductions,
            NetSalary = net,
            Notes = notes
        };
    }

    public void MarkAsPaid(string paidDate, int? voucherId = null)
    {
        IsPaid = true;
        PaidDate = paidDate;
        VoucherId = voucherId;
    }
}
