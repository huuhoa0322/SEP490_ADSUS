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
      // Supabase project dùng cho System Test (ADSUS_System_Test_V2) — ảnh siêu âm trả về
      // dạng signed URL của project nào thì host đó phải có ở đây, next/image mới chịu tải.
      {
        protocol: 'https',
        hostname: 'meicizoyaalxtuyxxguq.supabase.co',
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
