import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  images: {
    remotePatterns: [
      {
        protocol: 'https',
        hostname: 'miqarswgkuqvdmdnzrmi.supabase.co',
        port: '',
        pathname: '/**',
      },
    ],
  },
  async rewrites() {
    return [
      {
        source: '/booking',
        destination: '/dat-lich',
      },
    ];
  },
};

export default nextConfig;
