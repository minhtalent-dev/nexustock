import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";

const withNextIntl = createNextIntlPlugin("./src/i18n/request.ts");

const nextConfig: NextConfig = {
  allowedDevOrigins: ["172.18.208.1"],
  output: "standalone",
  // Tránh badge DevTools đè footer sidenav (mặc định bottom-left)
  devIndicators: {
    position: "bottom-right",
  },
};

export default withNextIntl(nextConfig);
