/** Frontend parola doğrulama — backend kurallarının aynası. */

export interface PasswordError {
  tooShort: boolean;
  missingUpper: boolean;
  missingDigit: boolean;
  missingSpecial: boolean;
}

export function validatePassword(password: string): PasswordError {
  return {
    tooShort: password.length < 8,
    missingUpper: !/[A-Z]/.test(password),
    missingDigit: !/\d/.test(password),
    missingSpecial: !/[^A-Za-z0-9]/.test(password),
  };
}

export function hasPasswordError(err: PasswordError): boolean {
  return err.tooShort || err.missingUpper || err.missingDigit || err.missingSpecial;
}

/** Kullanıcıya gösterilecek eksik kural listesi. */
export function passwordHelperText(password: string): string {
  if (!password) return 'En az 8 karakter, 1 büyük harf, 1 rakam, 1 noktalama işareti';

  const err = validatePassword(password);
  const messages: string[] = [];

  if (err.tooShort) messages.push('en az 8 karakter');
  if (err.missingUpper) messages.push('en az 1 büyük harf');
  if (err.missingDigit) messages.push('en az 1 rakam');
  if (err.missingSpecial) messages.push('en az 1 noktalama işareti (!, @, #, . vb.)');

  if (messages.length === 0) return '✓ Parola gereksinimleri karşılandı';
  return 'Eksik: ' + messages.join(', ');
}
