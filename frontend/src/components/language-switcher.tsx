'use client';

import { useLocale } from 'next-intl';
import { useRouter } from 'next/navigation';
import { LOCALE_COOKIE, type AppLocale } from '@/i18n/config';
import { Button } from '@/components/ui/button';
import clsx from 'clsx';

export function LanguageSwitcher({
  className,
  size = 'compact',
}: {
  className?: string;
  /** compact = toolbar; comfortable = sidebar footer hit-target */
  size?: 'compact' | 'comfortable';
}) {
  const locale = useLocale();
  const router = useRouter();

  function setLocale(next: AppLocale) {
    document.cookie = `${LOCALE_COOKIE}=${next};path=/;max-age=31536000;SameSite=Lax`;
    router.refresh();
  }

  const btnClass =
    size === 'comfortable' ? 'h-9 min-w-10 flex-1 px-3 text-xs' : 'h-7 px-2 text-xs';

  return (
    <div
      data-testid="language-switcher"
      className={clsx(
        'inline-flex items-center gap-1',
        size === 'comfortable' && 'rounded-lg border border-border bg-muted/40 p-1',
        className
      )}
      role="group"
      aria-label="Language"
    >
      <Button
        type="button"
        data-testid="language-option-vi"
        size="sm"
        variant={locale === 'vi' ? 'default' : 'ghost'}
        aria-pressed={locale === 'vi'}
        className={btnClass}
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
        className={btnClass}
        onClick={() => setLocale('en')}
      >
        EN
      </Button>
    </div>
  );
}
