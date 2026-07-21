import { getRequestConfig } from 'next-intl/server';
import { cookies } from 'next/headers';
import { defaultLocale, isAppLocale, LOCALE_COOKIE } from './config';
import { loadMessages } from './load-messages';

export default getRequestConfig(async () => {
  const store = await cookies();
  const raw = store.get(LOCALE_COOKIE)?.value;
  const locale = isAppLocale(raw) ? raw : defaultLocale;

  return {
    locale,
    messages: loadMessages(locale),
  };
});
