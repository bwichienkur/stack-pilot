import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

const publicPaths = ["/login"];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  if (publicPaths.some((p) => pathname.startsWith(p))) {
    return NextResponse.next();
  }

  const hasAuth = request.cookies.get("stackpilot_auth_hint")?.value === "1";
  if (!hasAuth) {
    return NextResponse.redirect(new URL("/login", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico|api).*)"],
};
