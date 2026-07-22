"use client";

import React, { useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Scan } from "lucide-react";

interface ScanInputProps {
  id: string;
  label: string;
  onScan: (value: string) => void;
  placeholder?: string;
}

export default function ScanInput({ id, label, onScan, placeholder }: ScanInputProps) {
  const t = useTranslations("Mobile.common");
  const [value, setValue] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);

  const focusInput = () => {
    if (inputRef.current) {
      inputRef.current.focus();
    }
  };

  useEffect(() => {
    focusInput();

    const handleBodyClick = () => {
      focusInput();
    };

    document.body.addEventListener("click", handleBodyClick);
    return () => {
      document.body.removeEventListener("click", handleBodyClick);
    };
  }, []);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!value.trim()) return;
    onScan(value.trim());
    setValue("");
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-2">
      <Label htmlFor={id} className="text-sm font-semibold text-slate-300">{label}</Label>
      <div className="flex gap-2">
        <div className="relative flex-1">
          <Input
            id={id}
            ref={inputRef}
            type="text"
            value={value}
            onChange={(e) => setValue(e.target.value)}
            placeholder={placeholder || t("scan.defaultPlaceholder")}
            className="bg-slate-800 border-slate-700 text-white font-mono text-lg focus-visible:ring-emerald-500 pr-10"
            autoComplete="off"
          />
          <Scan className="absolute right-3 top-3 h-4 w-4 text-slate-400 animate-pulse" />
        </div>
        <Button type="button" onClick={focusInput} size="icon" variant="outline" className="border-slate-700 text-slate-300">
          {t("scan.focus")}
        </Button>
      </div>
    </form>
  );
}
