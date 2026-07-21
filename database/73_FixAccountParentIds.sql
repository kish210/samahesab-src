-- 73_FixAccountParentIds.sql — رفعِ باگِ واقعیِ کشف‌شده حینِ ساختِ صفحهٔ «دفترِ حساب‌ها» در وب:
-- 07_DefaultChartOfAccounts.sql (بوت‌استرپِ اولیهٔ DB، فقط یک‌بار در عمرِ DB اجرا می‌شود) ParentId را
-- با یک INSERT...SELECTِ تک‌جمله‌ای پُر می‌کرد — زیرکوئریِ همبسته در همان statement فقط snapshotِ
-- پیش‌از-اجرایِ جدول را می‌بیند (نه ردیف‌هایِ همین statement)، پس ParentIdِ همهٔ حساب‌هایِ غیرِریشه
-- همیشه NULL می‌ماند (فقط Codeِ متنی سلسله‌مراتب را نشان می‌داد، نه FKِ واقعی). این یعنی درختِ حساب‌ها
-- در هر DBای که با ۰۷ (نه ۶۸_CompanyBaseChartTemplate که این باگ را نداشت) بوت‌استرپ شده، عملاً صاف
-- بود. رفع: یک UPDATEِ idempotent که ParentId را از رویِ Codeِ خودِ حساب (حذفِ آخرین سگمنتِ «-xxx»)
-- بازسازی می‌کند — امن برایِ اجرایِ مکرر (فقط ردیف‌هایِ ParentId=NULLِ غیرِریشه را لمس می‌کند).
UPDATE child
SET child.ParentId = parent.Id
FROM Acc.Accounts child
JOIN Acc.Accounts parent
  ON parent.CompanyId = child.CompanyId
 AND parent.Code = LEFT(child.Code, LEN(child.Code) - CHARINDEX('-', REVERSE(child.Code)))
WHERE child.ParentId IS NULL
  AND CHARINDEX('-', child.Code) > 0;
