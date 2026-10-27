import type { InputHTMLAttributes } from "react";
import { cn } from "../../lib/utils";

interface InputProps
    extends InputHTMLAttributes<HTMLInputElement> {
    label: string;
}

export default function Input({
    label,
    className,
    ...props
}: InputProps) {
    return (
        <div className="flex flex-col gap-2">
            <label className="text-sm font-medium text-gray-700">
                {label}
            </label>

            <input
                {...props}
                className={cn(
                    "rounded-lg border border-gray-300 px-3 py-2 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500",
                    className
                )}
            />
        </div>
    );
}