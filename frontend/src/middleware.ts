import { NextRequest, NextResponse } from 'next/server';
import { defaultLocale, isAppLocale, LOCALE_COOKIE } from '@/i18n/config';

export function middleware(request: NextRequest) {
  const response = NextResponse.next();
  const current = request.cookies.get(LOCALE_COOKIE)?.value;
  if (!isAppLocale(current)) {
    response.cookies.set(LOCALE_COOKIE, defaultLocale, {
      path: '/',
      maxAge: 60 * 60 * 24 * 365,
    });
  }
  return response;
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico|.*\\..*).*)'],
};
