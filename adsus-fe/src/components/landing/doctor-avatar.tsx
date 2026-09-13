"use client";

import Image from "next/image";
import { useState } from "react";

const INITIALS = "NVM";

interface DoctorAvatarProps {
  src: string;
  alt: string;
}

export function DoctorAvatar({ src, alt }: DoctorAvatarProps) {
  const [failed, setFailed] = useState(false);

  return (
    <div
      className="relative overflow-hidden rounded-full border-4"
      style={{ borderColor: "var(--lp-teal)" }}
    >
      {!failed ? (
        <Image
          src={src}
          alt={alt}
          width={96}
          height={96}
          className="object-cover"
          onError={() => setFailed(true)}
        />
      ) : (
        <div
          className="flex size-24 items-center justify-center text-2xl font-bold text-white"
          style={{ backgroundColor: "var(--lp-teal)" }}
        >
          {INITIALS}
        </div>
      )}
    </div>
  );
}
