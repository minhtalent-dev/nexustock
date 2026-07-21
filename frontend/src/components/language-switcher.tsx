'use client';

import { useLocale } from 'next-intl';
import { useRouter } from 'next/navigation';
import { LOCALE_COOKIE, type AppLocale } from '@/i18n/config';
import { Button } from '@/components/ui/button';
import clsx from 'clsx';

export function LanguageSwitcher({ className }: { className?: string }) {
  const locale = useLocale();
  const router = useRouter();

  function setLocale(next: AppLocale) {
    document.cookie = `${LOCALE_COOKIE}=${next};path=/;max-age=31536000;SameSite=Lax`;
    router.refresh();
  }

  return (
    <div
      data-testid="language-switcher"
      className={clsx('inline-flex items-center gap-1', className)}
      role="group"
      aria-label="Language"
    >
      <Button
        type="button"
        data-testid="language-option-vi"
        size="sm"
        variant={locale === 'vi' ? 'default' : 'ghost'}
        aria-pressed={locale === 'vi'}
        className="h-7 px-2 text-xs"
        onClick={() => setLocale('vi')}
      >
        VI
      </Button>
      <Button
        type="button"
        data-testid="language-option-en"
        size="sm"
        variant={locale === 'en' ? 'default' : 'ghost'}
        aria-pressed={locale === 'en'}
        className="h-7 px-2 text-xs"
        onClick={() => setLocale('en')}
      >
        EN
      </Button>
    </div>
  );
}
