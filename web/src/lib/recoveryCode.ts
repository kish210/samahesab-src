/** کدِ بازیابیِ تصادفیِ ۱۶نویسه‌ای — همان الگویِ `RecoveryCodeGenerator`ِ دسکتاپ (حروفِ بزرگ+عدد،
 * بدونِ کاراکترهایِ مبهم مثلِ 0/O یا 1/I). مشترکِ SettingsPage و SetupWizardPage. */
export function generateRecoveryCode(): string {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let code = '';
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  for (let i = 0; i < 16; i++) {
    code += chars[bytes[i] % chars.length];
    if (i % 4 === 3 && i < 15) code += '-';
  }
  return code;
}
